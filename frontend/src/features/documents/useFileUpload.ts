import { useRef, useState, type DragEvent } from 'react'
import { useUploads } from '../uploads/uploadsContext'
import type { UploadRequest } from '../uploads/uploadStore'
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

/** One picked file, for the request that sends it. */
export interface PickedFile {
  file: File
  pdf: boolean
  /** Counted by pdf.js as soon as the file is picked; the server only stores it (SCRUM-107). */
  pages: Promise<number | null>
}

interface Picked extends PickedFile {
  key: number
}

/**
 * Picking files for the upload queue: the meeting upload (UC-33a AF-1), the upload into an agenda item
 * (UC-33b AF-1) and the library upload (UC-36) share it. Each file goes as its own request, so one bad
 * file does not hold back the others, and a failed one is tried again on its own (PO 27/10/2026). The
 * dialog closes as soon as the files are queued; the tray follows them.
 * `roomLeft` is what the library still holds, so a pick past the quota is refused before it is sent.
 */
export function useFileUpload({
  toRequest,
  roomLeft,
  onQueued,
}: {
  toRequest: (picked: PickedFile) => UploadRequest
  roomLeft?: number
  onQueued: (count: number) => void
}) {
  const uploads = useUploads()
  const [picked, setPicked] = useState<Picked[]>([])
  const [rejected, setRejected] = useState<string[]>([])
  const [dragging, setDragging] = useState(false)
  const nextKey = useRef(0)

  const total = picked.reduce((n, p) => n + p.file.size, 0)
  const overLimit = picked.length > MAX_FILES || total > MAX_TOTAL_BYTES
  const overQuota = roomLeft !== undefined && total > roomLeft
  const canSubmit = picked.length > 0 && !overLimit && !overQuota

  function add(files: FileList | null) {
    if (!files) return
    const ok: Picked[] = []
    const bad: string[] = []
    for (const file of Array.from(files)) {
      const name = file.name.toLowerCase()
      const pdf = name.endsWith('.pdf')
      if ((!pdf && !name.endsWith('.docx')) || file.size === 0 || file.size > MAX_FILE_BYTES) bad.push(file.name)
      else ok.push({ key: nextKey.current++, file, pdf, pages: pdf ? countPdfPages(file) : Promise.resolve(null) })
    }
    setRejected(bad)
    setPicked((p) => [...p, ...ok])
  }

  function submit() {
    if (!canSubmit) return
    uploads.enqueue(picked.map(toRequest))
    onQueued(picked.length)
  }

  function onDrop(event: DragEvent) {
    event.preventDefault()
    setDragging(false)
    add(event.dataTransfer.files)
  }

  return {
    picked,
    setPicked,
    rejected,
    dragging,
    setDragging,
    total,
    overLimit,
    overQuota,
    add,
    submit,
    onDrop,
    canSubmit,
  }
}

export type FileUpload = ReturnType<typeof useFileUpload>
