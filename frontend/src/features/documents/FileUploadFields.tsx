import { Icon } from '../../components/Icon'
import { formatBytes } from './documentFile'
import { FileTypeTag } from './DocumentTags'
import { INVALID_FILES, OVER_LIMIT, type FileUpload } from './useFileUpload'

/** The dropzone, the picked files with their progress, and what went wrong. */
export function FileUploadFields({ upload, roomLeft }: { upload: FileUpload; roomLeft?: number }) {
  const { picked, rejected, failure, uploading, dragging, total, overLimit, overQuota, progress } = upload
  return (
    <>
      <label
        className={`dropzone${dragging ? ' is-dragging' : ''}${uploading ? ' is-disabled' : ''}`}
        onDragOver={(event) => {
          event.preventDefault()
          upload.setDragging(true)
        }}
        onDragLeave={() => upload.setDragging(false)}
        onDrop={upload.onDrop}
      >
        <input
          type="file"
          multiple
          accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
          disabled={uploading}
          onChange={(event) => {
            upload.add(event.target.files)
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
      {!overLimit && overQuota && (
        <div className="alert alert-error" role="alert">
          {/* MSG346's situation and wording, caught before sending up to 200 MB for nothing. */}
          <p>Kho tài liệu của bạn chỉ chứa tối đa 1 GB.</p>
          <p className="alert-hint">
            Đang chọn {formatBytes(total)}, kho còn {formatBytes(Math.max(0, roomLeft ?? 0))}.
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
                  onClick={() => upload.setPicked((all) => all.filter((x) => x.key !== p.key))}
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
    </>
  )
}
