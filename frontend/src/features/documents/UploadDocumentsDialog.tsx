import { useRef, useState, type DragEvent } from 'react'
import { uploadMeetingDocuments } from '../../api/documents'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { errorMessage, isApiError } from '../../lib/errors'
import type { Meeting } from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'
import { formatBytes } from './documentFile'
import { FileTypeTag } from './DocumentTags'
import { countPdfPages } from './pdf'

// BR-214, BR-319 — the server's DocumentFileRules. Checked here first: a body past the limit is cut
// off while the browser is still sending, and the browser then sees a network error, not a code.
const MB = 1024 * 1024
const MAX_FILE_BYTES = 50 * MB
const MAX_FILES = 10
const MAX_TOTAL_BYTES = 200 * MB

/** MSG164 and MSG288's wording: the same situations, caught before sending. */
const INVALID_FILES = 'Hãy chọn tệp PDF hoặc DOCX hợp lệ, mỗi tệp không quá 50 MB.'
const OVER_LIMIT = 'Mỗi lần tải tối đa 10 tệp, tổng dung lượng không quá 200 MB.'

const folderDate = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  timeZone: 'Asia/Ho_Chi_Minh',
})

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
 * UC-33a AF-1 — upload from the computer (SCRUM-107, wireframe 7.4b). All files or none (BR-194):
 * one request, one progress bar per file read off the bytes sent so far. The files also land in the
 * uploader's library, in the meeting's folder (BR-192). "Từ kho của tôi" is SCRUM-58.
 *
 * `onStale` closes the dialog when the answer means the upload no longer applies to this meeting:
 * no longer Chair or Secretary (MSG162), the meeting has ended (MSG172) or can no longer be read
 * (MSG174) — the page re-reads the meeting.
 */
export function UploadDocumentsDialog({
  meeting,
  onClose,
  onUploaded,
  onStale,
}: {
  meeting: Meeting
  onClose: () => void
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
  /** Asking before the picked files are thrown away. */
  const [discarding, setDiscarding] = useState(false)
  const nextKey = useRef(0)
  const abort = useRef<AbortController | null>(null)

  const uploading = sent !== null
  const total = picked.reduce((n, p) => n + p.file.size, 0)
  const overLimit = picked.length > MAX_FILES || total > MAX_TOTAL_BYTES

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
    if (picked.length === 0 || overLimit || uploading) return
    setFailure(null)
    setRejected([])
    setSent(0)
    abort.current = new AbortController()
    try {
      const files = await Promise.all(picked.map(async (p) => ({ file: p.file, pageCount: await p.pages })))
      const response = await uploadMeetingDocuments(meeting.id, files, {
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
      } else if (isApiError(err, 403, 'MSG162') || isApiError(err, 409, 'MSG172') || isApiError(err, 404, 'MSG174')) {
        onStale(errorMessage(err))
      } else if (!handleAccessLost(err)) {
        // MSG288, MSG166, a network error: nothing was added, the selection stays for another try.
        setFailure(errorMessage(err))
      }
    }
  }

  function close() {
    if (allSent) return
    // Picked files are the user's work: ask before dropping them (frontend-ui-design §3).
    if (picked.length > 0) return setDiscarding(true)
    onClose()
  }

  function discard() {
    abort.current?.abort()
    onClose()
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

  return (
    <Dialog
      title="Thêm tài liệu"
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-ghost" disabled={allSent} onClick={close}>
            Huỷ
          </button>
          <button
            type="button"
            className="btn btn-primary"
            disabled={picked.length === 0 || overLimit || uploading}
            onClick={() => void submit()}
          >
            {uploading ? 'Đang tải lên…' : picked.length > 0 ? `Thêm ${picked.length} tài liệu` : 'Thêm tài liệu'}
          </button>
        </>
      }
    >
      <div className="upload-body">
        <label
          className={`dropzone${dragging ? ' is-dragging' : ''}${uploading ? ' is-disabled' : ''}`}
          onDragOver={(event) => {
            event.preventDefault()
            setDragging(true)
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={onDrop}
        >
          <input
            type="file"
            multiple
            accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            disabled={uploading}
            onChange={(event) => {
              add(event.target.files)
              // Picking the same file again after removing it must still fire a change.
              event.target.value = ''
            }}
          />
          <Icon name="upload" size={24} />
          <strong>Kéo tệp vào đây hoặc bấm để chọn</strong>
          <span>PDF, DOCX, mỗi tệp tối đa 50 MB, mỗi lần tối đa 10 tệp, 200 MB</span>
        </label>

        {rejected.length > 0 && (
          <div className="alert alert-error" role="alert">
            <p>{INVALID_FILES}</p>
            <p className="alert-hint">Không thêm: {rejected.join(', ')}</p>
          </div>
        )}
        {overLimit && (
          <div className="alert alert-error" role="alert">
            <p>{OVER_LIMIT}</p>
            <p className="alert-hint">
              Đang chọn {picked.length} tệp, {formatBytes(total)}.
            </p>
          </div>
        )}

        {picked.length > 0 && (
          <ul className="upload-list" aria-label="Tệp đã chọn">
            {picked.map((p, i) => (
              <li key={p.key} className={p.error ? 'upload-row upload-row-error' : 'upload-row'}>
                <FileTypeTag pdf={p.pdf} />
                <span className="upload-row-name">
                  <strong>{p.file.name}</strong>
                  <small>{p.error ?? formatBytes(p.file.size)}</small>
                </span>
                {progress[i] !== null ? (
                  <span
                    className="upload-progress"
                    role="progressbar"
                    aria-label={`Tiến độ tải ${p.file.name}`}
                    aria-valuenow={Math.round(progress[i]! * 100)}
                  >
                    <i style={{ width: `${progress[i]! * 100}%` }} />
                  </span>
                ) : (
                  <button
                    type="button"
                    className="icon-button icon-button-sm"
                    aria-label={`Bỏ ${p.file.name}`}
                    data-tooltip="Bỏ tệp này"
                    onClick={() => setPicked((all) => all.filter((x) => x.key !== p.key))}
                  >
                    <Icon name="x" size={16} />
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}

        {failure && (
          <div className="alert alert-error" role="alert">
            <p>{failure}</p>
          </div>
        )}

        <p className="alert alert-info">
          Tệp cũng được lưu vào Kho tài liệu của bạn, thư mục “{meeting.title} –{' '}
          {folderDate.format(new Date(meeting.plannedStartTime))}”. Ai xem được cuộc họp đều đọc được tài liệu đã thêm.
        </p>
      </div>
      {discarding && (
        <ConfirmDialog
          title="Bỏ tệp đã chọn"
          confirmLabel="Bỏ tệp đã chọn"
          pendingLabel="Đang bỏ…"
          cancelLabel={uploading ? 'Tiếp tục tải lên' : 'Tiếp tục chọn'}
          tone="danger"
          onConfirm={async () => discard()}
          onClose={() => setDiscarding(false)}
        >
          {uploading
            ? 'Lần tải đang chạy sẽ dừng lại, không tệp nào được thêm vào cuộc họp.'
            : `${picked.length} tệp đã chọn sẽ không được thêm vào cuộc họp.`}
        </ConfirmDialog>
      )}
    </Dialog>
  )
}
