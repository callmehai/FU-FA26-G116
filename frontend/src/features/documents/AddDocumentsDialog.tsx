import { useState } from 'react'
import {
  addFromLibrary,
  linkFromLibrary,
  linkToAgendaItem,
  uploadMeetingDocuments,
  uploadToAgendaItem,
} from '../../api/documents'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { SegmentedControl } from '../../components/SegmentedControl'
import { errorMessage, isApiError } from '../../lib/errors'
import type { LibraryDocument, MeetingDocument } from '../../types/document'
import type { Meeting } from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'
import { isPdf } from './documentFile'
import { FileTypeTag } from './DocumentTags'
import { FileUploadFields } from './FileUploadFields'
import { useFileUpload } from './useFileUpload'
import { LibraryPicker } from './LibraryPicker'

type Source = 'computer' | 'library' | 'meeting'

const folderDate = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  timeZone: 'Asia/Ho_Chi_Minh',
})

/**
 * Answers that mean the change no longer applies to this meeting: the role is gone, the meeting has ended or
 * was cancelled, it can no longer be read, the item or a document went away. The page re-reads the meeting.
 */
function isStale(err: unknown) {
  return (
    isApiError(err, 404, 'MSG174') ||
    isApiError(err, 409, 'MSG349') ||
    // The general group (UC-33a).
    isApiError(err, 403, 'MSG162') ||
    isApiError(err, 409, 'MSG172') ||
    // An agenda item (UC-33b).
    isApiError(err, 403, 'MSG240') ||
    isApiError(err, 409, 'MSG247') ||
    isApiError(err, 404, 'MSG242') ||
    isApiError(err, 404, 'MSG170')
  )
}

/**
 * Adding documents to a meeting (SCRUM-107, SCRUM-58): to the general group (UC-33a) from the computer or the
 * library, or to one agenda item (UC-33b) from the meeting's own documents, the library or the computer.
 * All or none (BR-194, BR-304). Uploads also land in the uploader's library, in the meeting's folder (BR-192).
 */
export function AddDocumentsDialog({
  meeting,
  item,
  meetingDocuments = [],
  onClose,
  onAdded,
  onStale,
}: {
  meeting: Meeting
  /** The agenda item to link to; the general group when absent. */
  item?: { id: string; number: number; title: string; documentIds: string[] }
  /** Every document of the meeting, for "Trong cuộc họp". */
  meetingDocuments?: MeetingDocument[]
  onClose: () => void
  onAdded: (message: string) => void
  onStale: (message: string) => void
}) {
  const handleAccessLost = useAccessLost()
  const linkable = item ? meetingDocuments.filter((d) => !item.documentIds.includes(d.id)) : []
  const sources = [
    ...(item ? [{ value: 'meeting' as const, label: 'Trong cuộc họp', short: 'Cuộc họp' }] : []),
    { value: 'computer' as const, label: 'Tải từ máy', short: 'Máy' },
    { value: 'library' as const, label: 'Từ kho của tôi', short: 'Kho' },
  ] satisfies { value: Source; label: string; short: string }[]
  const [source, setSource] = useState<Source>(item && linkable.length > 0 ? 'meeting' : 'computer')
  const [fromLibrary, setFromLibrary] = useState(new Map<string, LibraryDocument>())
  const [fromMeeting, setFromMeeting] = useState(new Set<string>())
  const [libraryReload, setLibraryReload] = useState(0)
  const [pending, setPending] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [discarding, setDiscarding] = useState(false)

  const upload = useFileUpload({
    upload: (files, options) =>
      item
        ? uploadToAgendaItem(meeting.id, item.id, files, options)
        : uploadMeetingDocuments(meeting.id, files, options),
    isStale,
    onUploaded: onAdded,
    onStale,
  })

  const chosen =
    source === 'computer' ? upload.picked.length : source === 'library' ? fromLibrary.size : fromMeeting.size
  const busy = upload.uploading || pending
  const dirty = upload.picked.length > 0 || fromLibrary.size > 0 || fromMeeting.size > 0

  async function addChosen() {
    if (chosen === 0 || busy) return
    setPending(true)
    setFailure(null)
    try {
      const ids = source === 'library' ? [...fromLibrary.keys()] : [...fromMeeting]
      const response =
        source === 'meeting'
          ? await linkToAgendaItem(meeting.id, item!.id, ids)
          : item
            ? await linkFromLibrary(meeting.id, item.id, ids)
            : await addFromLibrary(meeting.id, ids)
      onAdded(response.message)
    } catch (err) {
      setPending(false)
      if (isStale(err)) return onStale(errorMessage(err))
      if (handleAccessLost(err)) return
      // MSG249: a chosen document left the library meanwhile; show the place as it is now.
      if (isApiError(err, 400, 'MSG249')) {
        setFromLibrary(new Map())
        setLibraryReload((n) => n + 1)
      }
      // MSG165 already in the meeting, MSG243 already on this item, a save error: the choice stays.
      setFailure(errorMessage(err))
    }
  }

  function close() {
    if (upload.allSent || pending) return
    // What the user picked is their work: ask before dropping it (frontend-ui-design §3).
    if (dirty) return setDiscarding(true)
    onClose()
  }

  const n = chosen > 0 ? `${chosen} ` : ''
  const submitLabel = upload.uploading
    ? 'Đang tải lên…'
    : pending
      ? 'Đang thêm…'
      : source === 'meeting'
        ? `Gắn ${n}tài liệu`
        : `Thêm ${n}tài liệu`

  return (
    <Dialog
      title={item ? `Thêm tài liệu cho mục ${item.number}` : 'Thêm tài liệu chung'}
      size="lg"
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-ghost" disabled={upload.allSent || pending} onClick={close}>
            Huỷ
          </button>
          <button
            type="button"
            className="btn btn-primary"
            disabled={source === 'computer' ? !upload.canSubmit : chosen === 0 || busy}
            onClick={() => void (source === 'computer' ? upload.submit() : addChosen())}
          >
            {submitLabel}
          </button>
        </>
      }
    >
      <div className="upload-body">
        {item && <p className="dialog-text add-docs-item">{item.title}</p>}
        <SegmentedControl
          label="Lấy tài liệu từ"
          value={source}
          options={sources}
          counts={{ library: fromLibrary.size || undefined, meeting: fromMeeting.size || undefined }}
          onChange={(next) => {
            if (busy) return
            setFailure(null)
            setSource(next)
          }}
        />

        {source === 'computer' && <FileUploadFields upload={upload} />}

        {source === 'library' && (
          <LibraryPicker
            selected={fromLibrary}
            reloadKey={libraryReload}
            onToggle={(doc) =>
              setFromLibrary((all) => {
                const next = new Map(all)
                if (!next.delete(doc.id)) next.set(doc.id, doc)
                return next
              })
            }
          />
        )}

        {source === 'meeting' &&
          (linkable.length === 0 ? (
            <p className="documents-empty">Mọi tài liệu của cuộc họp đã gắn với mục này.</p>
          ) : (
            <ul className="picker-list" aria-label="Tài liệu của cuộc họp">
              {linkable.map((doc) => (
                <li key={doc.id}>
                  <label className="picker-row">
                    <input
                      type="checkbox"
                      checked={fromMeeting.has(doc.id)}
                      onChange={() =>
                        setFromMeeting((all) => {
                          const next = new Set(all)
                          if (!next.delete(doc.id)) next.add(doc.id)
                          return next
                        })
                      }
                    />
                    <FileTypeTag pdf={isPdf(doc)} />
                    <span className="picker-name">
                      <strong>{doc.fileName}</strong>
                      {doc.pageCount ? <small>{doc.pageCount} trang</small> : null}
                    </span>
                  </label>
                </li>
              ))}
            </ul>
          ))}

        {failure && (
          <div className="alert alert-error" role="alert">
            <p>{failure}</p>
          </div>
        )}

        <p className="alert alert-info">
          {source === 'computer' ? (
            <>
              Tệp cũng được lưu vào Kho tài liệu của bạn, thư mục “{meeting.title} –{' '}
              {folderDate.format(new Date(meeting.plannedStartTime))}”.
            </>
          ) : source === 'library' ? (
            'Cuộc họp dùng tên hiện tại của tài liệu; đổi tên trong kho sau đó không đổi tên trong cuộc họp.'
          ) : (
            'Tài liệu vẫn ở các mục đang gắn, chỉ thêm vào mục này.'
          )}{' '}
          {source !== 'meeting' && 'Ai xem được cuộc họp đều đọc được tài liệu đã thêm.'}
        </p>
      </div>
      {discarding && (
        <ConfirmDialog
          title="Bỏ tài liệu đã chọn"
          confirmLabel="Bỏ tài liệu đã chọn"
          pendingLabel="Đang bỏ…"
          cancelLabel={upload.uploading ? 'Tiếp tục tải lên' : 'Tiếp tục chọn'}
          tone="danger"
          onConfirm={async () => {
            upload.abort()
            onClose()
          }}
          onClose={() => setDiscarding(false)}
        >
          {upload.uploading
            ? 'Lần tải đang chạy sẽ dừng lại, không tệp nào được thêm vào cuộc họp.'
            : 'Tài liệu đã chọn sẽ không được thêm vào cuộc họp.'}
        </ConfirmDialog>
      )}
    </Dialog>
  )
}
