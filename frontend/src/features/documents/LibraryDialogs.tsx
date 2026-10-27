import { useState, type ReactNode } from 'react'
import { uploadToLibrary } from '../../api/library'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { errorMessage, isApiError } from '../../lib/errors'
import type { LibraryFolder } from '../../types/document'
import { FileUploadFields } from './FileUploadFields'
import { useFileUpload } from './useFileUpload'
import { childrenOf, LIBRARY_ROOT, placeName, trailOf } from './libraryTree'

/** Field-level answers of the name endpoints: blank (MSG188), bad document name (MSG347), too long (MSG348). */
const FIELD_CODES = ['MSG188', 'MSG347', 'MSG348']

/**
 * A new folder, or a new name for a folder or a document (UC-72a, UC-72b, UC-71b). A document keeps its
 * extension: only the part before it is edited, the extension is shown after the field (MSG347 otherwise).
 */
export function NameDialog({
  title,
  intro,
  label,
  initial = '',
  extension = '',
  maxLength,
  submitLabel,
  onSubmit,
  onClose,
}: {
  title: string
  intro: ReactNode
  label: string
  initial?: string
  /** ".pdf" or ".docx" for a document. */
  extension?: string
  maxLength: number
  submitLabel: string
  /** Throw the API error to keep the dialog open. */
  onSubmit: (name: string) => Promise<void>
  onClose: () => void
}) {
  const [value, setValue] = useState(initial)
  const [fieldError, setFieldError] = useState<string | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const blank = value.trim() === ''

  async function submit() {
    if (blank || pending) return
    setPending(true)
    setFieldError(null)
    setFailure(null)
    try {
      await onSubmit(value.trim() + extension)
    } catch (err) {
      setPending(false)
      if (FIELD_CODES.some((code) => isApiError(err, 400, code))) setFieldError(errorMessage(err))
      else setFailure(errorMessage(err))
    }
  }

  return (
    <Dialog
      title={title}
      onClose={pending ? () => {} : onClose}
      footer={
        <>
          <button type="button" className="btn btn-ghost" disabled={pending} onClick={onClose}>
            Huỷ
          </button>
          <button
            type="submit"
            form="library-name-form"
            className="btn btn-primary"
            disabled={blank || pending || value.trim() + extension === initial + extension}
          >
            {pending ? 'Đang lưu…' : submitLabel}
          </button>
        </>
      }
    >
      <form
        id="library-name-form"
        className="dialog-form"
        onSubmit={(event) => {
          event.preventDefault()
          void submit()
        }}
      >
        <p className="dialog-text">{intro}</p>
        <label className="field">
          <span className="field-required">{label}</span>
          <div className="input-group">
            <input
              value={value}
              // The extension printed after the field is not part of the field's name.
              aria-label={label}
              maxLength={maxLength - extension.length}
              aria-invalid={fieldError ? true : undefined}
              onChange={(event) => setValue(event.target.value)}
              // A name is replaced more often than edited: start with it selected.
              onFocus={(event) => event.target.select()}
              autoFocus
            />
            {extension && <span className="name-ext">{extension}</span>}
          </div>
          {fieldError && <small className="field-error">{fieldError}</small>}
        </label>
        {failure && (
          <p className="alert alert-error dialog-error" role="alert">
            {failure}
          </p>
        )}
      </form>
    </Dialog>
  )
}

/**
 * "Chuyển đến…" (UC-71c, UC-72c): browse the library one level at a time and move into the place shown.
 * A folder cannot go into itself or below it (BR-222): those rows are not offered.
 */
export function MoveDialog({
  folders,
  name,
  from,
  excluded,
  onMove,
  onClose,
}: {
  folders: LibraryFolder[]
  name: string
  /** Where it is now. */
  from: string | null
  /** The folder being moved and everything below it. */
  excluded?: Set<string>
  onMove: (folderId: string | null) => Promise<void>
  onClose: () => void
}) {
  const [place, setPlace] = useState<string | null>(from)
  const [failure, setFailure] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const rows = childrenOf(folders, place).filter((f) => !excluded?.has(f.id))
  const here = place === from
  const target = placeName(folders, place)

  async function move() {
    if (here || pending) return
    setPending(true)
    setFailure(null)
    try {
      await onMove(place)
    } catch (err) {
      setPending(false)
      setFailure(errorMessage(err))
    }
  }

  return (
    <Dialog
      title="Chuyển đến…"
      onClose={pending ? () => {} : onClose}
      footer={
        <>
          <button type="button" className="btn btn-ghost" disabled={pending} onClick={onClose}>
            Huỷ
          </button>
          <button type="button" className="btn btn-primary" disabled={here || pending} onClick={() => void move()}>
            {pending ? 'Đang chuyển…' : `Chuyển vào “${target}”`}
          </button>
        </>
      }
    >
      <p className="dialog-text">
        Chọn thư mục để chuyển <strong>{name}</strong> vào.
      </p>
      <div className="picker">
        <nav className="picker-trail" aria-label="Thư mục đích">
          <button type="button" className="picker-crumb" onClick={() => setPlace(null)} disabled={place === null}>
            <Icon name="archive" size={16} />
            {LIBRARY_ROOT}
          </button>
          {trailOf(folders, place).map((f) => (
            <span key={f.id} className="picker-crumb-wrap">
              <Icon name="chevronRight" size={14} />
              <button type="button" className="picker-crumb" onClick={() => setPlace(f.id)} disabled={f.id === place}>
                {f.name}
              </button>
            </span>
          ))}
        </nav>
        <ul className="picker-list" aria-label="Thư mục con">
          {rows.length === 0 ? (
            <li className="documents-empty">Không có thư mục con.</li>
          ) : (
            rows.map((f) => (
              <li key={f.id}>
                <button type="button" className="picker-row picker-folder" onClick={() => setPlace(f.id)}>
                  <Icon name="folder" size={18} />
                  <span className="picker-name">{f.name}</span>
                  <Icon name="chevronRight" size={16} />
                </button>
              </li>
            ))
          )}
        </ul>
      </div>
      {here && <p className="dialog-text dialog-note">{name} đang ở thư mục này.</p>}
      {failure && (
        <div className="alert alert-error" role="alert">
          <p>{failure}</p>
        </div>
      )}
    </Dialog>
  )
}

/**
 * UC-36: files from the computer into the open folder, all or none. What the library still holds is checked
 * before sending (MSG346); `onStale` when the folder went away meanwhile (MSG179).
 */
export function LibraryUploadDialog({
  folderId,
  folders,
  roomLeft,
  onClose,
  onUploaded,
  onStale,
}: {
  folderId: string | null
  folders: LibraryFolder[]
  roomLeft: number
  onClose: () => void
  onUploaded: (message: string) => void
  onStale: (message: string) => void
}) {
  const [discarding, setDiscarding] = useState(false)
  const upload = useFileUpload({
    upload: (files, options) => uploadToLibrary(folderId, files, options),
    isStale: (err) => isApiError(err, 404, 'MSG179'),
    roomLeft,
    onUploaded,
    onStale,
  })

  function close() {
    if (upload.allSent) return
    if (upload.picked.length > 0) return setDiscarding(true)
    onClose()
  }

  return (
    <Dialog
      title="Tải tài liệu lên"
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-ghost" disabled={upload.allSent} onClick={close}>
            Huỷ
          </button>
          <button
            type="button"
            className="btn btn-primary"
            disabled={!upload.canSubmit}
            onClick={() => void upload.submit()}
          >
            {upload.uploading
              ? 'Đang tải lên…'
              : upload.picked.length > 0
                ? `Tải lên ${upload.picked.length} tài liệu`
                : 'Tải lên'}
          </button>
        </>
      }
    >
      <div className="upload-body">
        <p className="dialog-text">
          Thư mục đích: <strong>{placeName(folders, folderId)}</strong>
        </p>
        <FileUploadFields upload={upload} roomLeft={roomLeft} />
      </div>
      {discarding && (
        <ConfirmDialog
          title="Bỏ tệp đã chọn"
          confirmLabel="Bỏ tệp đã chọn"
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
            ? 'Lần tải đang chạy sẽ dừng lại, không tệp nào được thêm vào kho.'
            : `${upload.picked.length} tệp đã chọn sẽ không được tải lên.`}
        </ConfirmDialog>
      )}
    </Dialog>
  )
}
