import { useRef, useState, type DragEvent } from 'react'
import { errorMessage, isApiError } from '../../lib/errors'
import { useAccessLost } from '../auth/useAccessLost'
import { countPdfPages } from './pdf'

// BR-214, BR-319 — the server's DocumentFileRules. Checked here first: a body past the limit is cut
// off while the browser is still sending, and the browser then sees a network error, not a code.
const MB = 1024 * 1024
const MAX_FILE_BYTES = 50 * MB
const MAX_FILES = 10
const MAX_TOTAL_BYTES = 200 * MB

/** MSG164 and MSG288's wording: the same situations, caught before sending. */
export const INVALID_FILES = 'Hãy chọn tệp PDF hoặc DOCX hợp lệ, mỗi tệp không quá 50 MB.'
export const OVER_LIMIT = 'Mỗi lần tải tối đa 10 tệp, tổng dung lượng không quá 200 MB.'

export type UploadFiles = { file: File; pageCount: number | null }[]
export type UploadOptions = { onProgress?: (loaded: number, total: number) => void; signal?: AbortSignal }

interface Picked {
  key: number
  file: File
  pdf: boolean
  /** Counted by pdf.js as soon as the file is picked; the server only stores it (SCRUM-107). */
  pages: Promise<number | null>
  /** The server's reason for this file, from `errors["files[i]"]`. */
  error?: string
}

/**
 * Picking files and sending them in one request, all or none (BR-194): the meeting upload (UC-33a AF-1),
 * the upload into an agenda item (UC-33b AF-1) and the library upload (UC-36) share it. `isStale` names the
 * answers that mean the upload no longer applies (role lost, meeting ended…): those go to `onStale`.
 * `roomLeft` is what the library still holds, so a batch past the quota is refused before it is sent.
 */
export function useFileUpload({
  upload,
  isStale,
  roomLeft,
  onUploaded,
  onStale,
}: {
  upload: (files: UploadFiles, options: UploadOptions) => Promise<{ message: string }>
  isStale: (err: unknown) => boolean
  roomLeft?: number
  onUploaded: (message: string) => void
  onStale: (message: string) => void
}) {
  const handleAccessLost = useAccessLost()
  const [picked, setPicked] = useState<Picked[]>([])
  const [rejected, setRejected] = useState<string[]>([])
  const [failure, setFailure] = useState<string | null>(null)
  /** Bytes sent, while the request runs. */
  const [sent, setSent] = useState<number | null>(null)
  /** The whole body is out: the server may already be committing, so cancelling could not undo it. */
  const [allSent, setAllSent] = useState(false)
  const [dragging, setDragging] = useState(false)
  const nextKey = useRef(0)
  const abort = useRef<AbortController | null>(null)

  const uploading = sent !== null
  const total = picked.reduce((n, p) => n + p.file.size, 0)
  const overLimit = picked.length > MAX_FILES || total > MAX_TOTAL_BYTES
  const overQuota = roomLeft !== undefined && total > roomLeft

  function add(files: FileList | null) {
    if (!files || uploading) return
    const ok: Picked[] = []
    const bad: string[] = []
    for (const file of Array.from(files)) {
      const name = file.name.toLowerCase()
      const pdf = name.endsWith('.pdf')
      if ((!pdf && !name.endsWith('.docx')) || file.size === 0 || file.size > MAX_FILE_BYTES) bad.push(file.name)
      else ok.push({ key: nextKey.current++, file, pdf, pages: pdf ? countPdfPages(file) : Promise.resolve(null) })
    }
    setRejected(bad)
    setFailure(null)
    setPicked((p) => [...p.map(({ error: _error, ...rest }) => rest), ...ok])
  }

  async function submit() {
    if (picked.length === 0 || overLimit || overQuota || uploading) return
    setFailure(null)
    setRejected([])
    setSent(0)
    abort.current = new AbortController()
    try {
      const files = await Promise.all(picked.map(async (p) => ({ file: p.file, pageCount: await p.pages })))
      const response = await upload(files, {
        onProgress: (loaded, size) => {
          setSent(loaded)
          setAllSent(loaded >= size)
        },
        signal: abort.current.signal,
      })
      onUploaded(response.message)
    } catch (err) {
      setSent(null)
      setAllSent(false)
      if (err instanceof DOMException && err.name === 'AbortError') return
      if (isApiError(err, 400, 'MSG164')) {
        // Keyed `files[i]` in the order sent; `files` alone is about the whole request.
        const errors = err.body?.errors ?? {}
        setPicked((p) => p.map((item, i) => ({ ...item, error: errors[`files[${i}]`] })))
        setFailure(err.body?.message ?? INVALID_FILES)
      } else if (isStale(err)) {
        onStale(errorMessage(err))
      } else if (!handleAccessLost(err)) {
        // MSG288, MSG346, a save error, a network error: nothing was added, the selection stays for another try.
        setFailure(errorMessage(err))
      }
    }
  }

  function onDrop(event: DragEvent) {
    event.preventDefault()
    setDragging(false)
    add(event.dataTransfer.files)
  }

  // How far each file is, from the bytes sent: the multipart body carries them in order.
  const progress = picked.map((p, i) => {
    if (sent === null) return null
    const start = picked.slice(0, i).reduce((n, q) => n + q.file.size, 0)
    return Math.min(1, Math.max(0, (sent - start) / p.file.size))
  })

  return {
    picked,
    setPicked,
    rejected,
    failure,
    uploading,
    allSent,
    dragging,
    setDragging,
    total,
    overLimit,
    overQuota,
    progress,
    add,
    submit,
    onDrop,
    canSubmit: picked.length > 0 && !overLimit && !overQuota && !uploading,
    abort: () => abort.current?.abort(),
  }
}

export type FileUpload = ReturnType<typeof useFileUpload>
