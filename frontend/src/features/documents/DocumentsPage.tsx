import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import {
  createFolder,
  deleteFolder,
  deleteLibraryDocument,
  getLibrary,
  getLibraryContent,
  moveFolder,
  moveLibraryDocument,
  renameFolder,
  renameLibraryDocument,
} from '../../api/library'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { Select } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDate } from '../../lib/format'
import { routes } from '../../lib/routes'
import { useSearchBox } from '../../lib/useSearchBox'
import type { LibraryDocument, LibraryFolder, LibraryView } from '../../types/document'
import { useAccessLost } from '../auth/useAccessLost'
import { formatBytes, isPdf, saveBlob } from './documentFile'
import { FileTypeTag } from './DocumentTags'
import { LibraryUploadDialog, MoveDialog, NameDialog } from './LibraryDialogs'
import { childrenOf, LIBRARY_ROOT, placeName, subtreeOf, trailOf } from './libraryTree'

type TypeFilter = '' | 'pdf' | 'docx'

const TYPE_OPTIONS = [
  { value: '', label: 'Mọi loại tệp' },
  { value: 'pdf', label: 'PDF' },
  { value: 'docx', label: 'DOCX' },
] satisfies { value: TypeFilter; label: string }[]

/** BR-191: a folder at the fifth level holds no further folder. */
const MAX_DEPTH = 5
const COLUMN_COUNT = 5

type Dialog =
  | { kind: 'upload' }
  | { kind: 'newFolder' }
  | { kind: 'rename'; folder: LibraryFolder }
  | { kind: 'rename'; document: LibraryDocument }
  | { kind: 'move'; folder: LibraryFolder }
  | { kind: 'move'; document: LibraryDocument }
  | { kind: 'delete'; folder: LibraryFolder }
  | { kind: 'delete'; document: LibraryDocument }

const extensionOf = (name: string) => /\.(pdf|docx)$/i.exec(name)?.[0] ?? ''

/**
 * Each user's own document library (SCRUM-110, SCRUM-111; UC-34, UC-36, UC-71a–c, UC-72a–d), laid out as
 * AI-eCabinet's "Tài liệu của tôi": one folder at a time, its place in the URL, its sub-folders and documents in
 * one table. Search and the type filter look at the open folder only (UC-34 AF-1) and are done by the server for
 * documents; sub-folder names are matched here, since the server lists every folder anyway.
 */
export function DocumentsPage() {
  const handleAccessLost = useAccessLost()
  const toast = useToast()
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const folderId = params.get('folder') || null
  const q = params.get('q') ?? ''
  const rawType = params.get('type')
  const type: TypeFilter = rawType === 'pdf' || rawType === 'docx' ? rawType : ''
  const [view, setView] = useState<LibraryView | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  const [dialog, setDialog] = useState<Dialog | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  const update = useCallback(
    (patch: { folder?: string | null; q?: string; type?: TypeFilter }, replace = true) => {
      const next = new URLSearchParams(params)
      for (const [key, value] of Object.entries(patch)) {
        if (value) next.set(key, value)
        else next.delete(key)
      }
      setParams(next, { replace })
    },
    [params, setParams],
  )
  const [search, setSearch] = useSearchBox(q, (value) => update({ q: value.trim() }))

  useEffect(() => {
    let live = true
    getLibrary(folderId, q, type)
      .then((data) => {
        if (!live) return
        setLoadError(null)
        setView(data)
      })
      .catch((err) => {
        if (!live || handleAccessLost(err)) return
        if (folderId && isApiError(err, 404, 'MSG175')) {
          // Deleted in another tab, or a link to someone else's folder: back to the root, not a dead end.
          toast.show(errorMessage(err), 'error')
          navigate(routes.documents, { replace: true })
        } else setLoadError(errorMessage(err))
      })
    return () => {
      live = false
    }
  }, [folderId, q, type, attempt, handleAccessLost, navigate, toast])

  const reload = () => {
    setLoadError(null)
    setAttempt((n) => n + 1)
  }
  /** MSG175/MSG179: what the user acted on is gone; say so and show the library as it is. */
  const stale = (err: unknown) => {
    if (!isApiError(err, 404, 'MSG175') && !isApiError(err, 404, 'MSG179')) return false
    setDialog(null)
    toast.show(errorMessage(err), 'error')
    reload()
    return true
  }
  const done = (message: string) => {
    setDialog(null)
    toast.show(message)
    reload()
  }

  const folders = view?.folders ?? []
  const trail = trailOf(folders, folderId)
  const here = trail.at(-1)
  const filtered = q !== '' || type !== ''
  const needle = q.trim().toLocaleLowerCase('vi')
  // A type filter is about files: folders step aside. A search matches folder names as well.
  const subfolders = type
    ? []
    : childrenOf(folders, folderId).filter((f) => f.name.toLocaleLowerCase('vi').includes(needle))
  const loading = view === null || view.folderId !== folderId
  const rows = loading ? null : { subfolders, documents: view.documents }
  const usedRatio = view ? view.usedBytes / view.quotaBytes : 0
  const atMaxDepth = trail.length >= MAX_DEPTH

  async function download(doc: LibraryDocument) {
    setBusyId(doc.id)
    try {
      saveBlob(await getLibraryContent(doc.id, true), doc.fileName)
    } catch (err) {
      if (!handleAccessLost(err) && !stale(err)) toast.show(errorMessage(err), 'error')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <>
      {/* At the root the page title already names the place; inside a folder the trail leads back. */}
      {folderId && (
        <nav className="library-trail" aria-label="Vị trí trong kho">
          <Link to={routes.documents} className="library-crumb">
            <Icon name="archive" size={16} />
            <span className="library-crumb-root">{LIBRARY_ROOT}</span>
          </Link>
          {trail.map((f, i) => (
            <span key={f.id} className="library-crumb-wrap">
              <Icon name="chevronRight" size={14} />
              {i === trail.length - 1 ? (
                <span className="library-crumb" aria-current="page">
                  {f.name}
                </span>
              ) : (
                <Link to={`${routes.documents}?folder=${f.id}`} className="library-crumb">
                  {f.name}
                </Link>
              )}
            </span>
          ))}
        </nav>
      )}

      <PageHeader
        title={here?.name ?? LIBRARY_ROOT}
        action={
          <div className="page-header-actions library-actions">
            <button
              type="button"
              className="btn btn-outline btn-lg"
              disabled={!view || atMaxDepth}
              data-tooltip={atMaxDepth ? 'Thư mục chỉ lồng tối đa 5 cấp' : undefined}
              onClick={() => setDialog({ kind: 'newFolder' })}
            >
              <Icon name="folderPlus" size={18} />
              Thư mục mới
            </button>
            <button
              type="button"
              className="btn btn-primary btn-lg btn-cta"
              disabled={!view}
              onClick={() => setDialog({ kind: 'upload' })}
            >
              <Icon name="upload" size={18} />
              <span className="library-action-full">Tải tài liệu lên</span>
              <span className="library-action-short">Tải lên</span>
            </button>
          </div>
        }
      />

      <div className="list-panel list-fill">
        <div className="toolbar">
          <label className="search search-flex">
            <Icon name="search" size={16} />
            <input
              type="search"
              placeholder="Tìm trong thư mục này…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Tìm theo tên tài liệu hoặc thư mục, trong thư mục đang mở"
            />
          </label>
          <Select
            value={type}
            options={TYPE_OPTIONS}
            onChange={(value) => update({ type: value })}
            label="Lọc theo loại tệp"
          />
          {view && (
            <span
              className={usedRatio > 1 ? 'library-quota is-over' : 'library-quota'}
              data-tooltip="Dung lượng kho tài liệu của bạn"
            >
              <span className="library-quota-bar" aria-hidden="true">
                <i style={{ width: `${Math.min(1, usedRatio) * 100}%` }} />
              </span>
              <span>
                {formatBytes(view.usedBytes)} / {formatBytes(view.quotaBytes)}
              </span>
            </span>
          )}
        </div>

        <div className="table-card table-fill">
          <table className="data-table library-table">
            <thead>
              <tr>
                <th>Tên</th>
                <th className="col-size">Kích thước</th>
                <th className="col-usage">Đang dùng</th>
                <th className="col-date">Ngày tạo</th>
                <th className="col-action">Thao tác</th>
              </tr>
            </thead>
            <tbody aria-busy={loading}>
              {loadError ? (
                <tr>
                  <td colSpan={COLUMN_COUNT} className="cell-empty">
                    <p className="cell-error">{loadError}</p>
                    <button type="button" className="btn btn-outline btn-sm" onClick={reload}>
                      <Icon name="refresh" size={16} />
                      Thử lại
                    </button>
                  </td>
                </tr>
              ) : !rows ? (
                Array.from({ length: 4 }, (_, i) => (
                  <tr key={i} className="row-skeleton" aria-hidden="true">
                    {Array.from({ length: COLUMN_COUNT }, (_, j) => (
                      <td key={j}>
                        <span className="skeleton" />
                      </td>
                    ))}
                  </tr>
                ))
              ) : rows.subfolders.length === 0 && rows.documents.length === 0 ? (
                <tr>
                  <td colSpan={COLUMN_COUNT} className="cell-empty">
                    {filtered ? (
                      <>
                        <p>Không có tài liệu hoặc thư mục nào khớp bộ lọc.</p>
                        <button
                          type="button"
                          className="btn btn-outline btn-sm"
                          onClick={() => {
                            setSearch('')
                            update({ q: '', type: '' })
                          }}
                        >
                          Xoá bộ lọc
                        </button>
                      </>
                    ) : (
                      <p>{folderId ? 'Thư mục này chưa có gì.' : 'Kho tài liệu của bạn chưa có gì.'}</p>
                    )}
                  </td>
                </tr>
              ) : (
                <>
                  {rows.subfolders.map((f) => (
                    <tr
                      key={f.id}
                      className="row-link"
                      onClick={() => update({ folder: f.id, q: '', type: '' }, false)}
                    >
                      <td className="cell-title">
                        <span className="library-name">
                          <Icon name="folder" size={22} />
                          <span className="library-name-col">
                            <Link
                              to={`${routes.documents}?folder=${f.id}`}
                              onClick={(e) => e.stopPropagation()}
                              className="library-name-text"
                            >
                              {f.name}
                            </Link>
                            <span className="cell-sub library-sub">Thư mục</span>
                          </span>
                        </span>
                      </td>
                      <td className="cell-muted col-size">Thư mục</td>
                      <td className="col-usage" />
                      <td className="cell-muted col-date">{formatDate(f.createdAt)}</td>
                      <td className="col-action" onClick={(e) => e.stopPropagation()}>
                        <span className="row-actions">
                          <RowAction
                            icon="pencil"
                            label="Đổi tên"
                            name={f.name}
                            onClick={() => setDialog({ kind: 'rename', folder: f })}
                          />
                          <RowAction
                            icon="folderInput"
                            label="Chuyển đến…"
                            name={f.name}
                            onClick={() => setDialog({ kind: 'move', folder: f })}
                          />
                          <RowAction
                            icon="trash"
                            label="Xoá"
                            name={f.name}
                            onClick={() => setDialog({ kind: 'delete', folder: f })}
                          />
                        </span>
                      </td>
                    </tr>
                  ))}
                  {rows.documents.map((doc) => {
                    const open = routes.libraryDocument(doc.id, folderId)
                    return (
                      <tr key={doc.id} className="row-link" onClick={() => navigate(open)}>
                        <td className="cell-title">
                          <span className="library-name">
                            <FileTypeTag pdf={isPdf(doc)} size={28} />
                            <span className="library-name-col">
                              <Link to={open} onClick={(e) => e.stopPropagation()} className="library-name-text">
                                {doc.fileName}
                              </Link>
                              {/* On a phone the size column goes; its facts move under the name. */}
                              <span className="cell-sub library-sub">
                                {[doc.pageCount && `${doc.pageCount} trang`, formatBytes(doc.byteSize)]
                                  .filter(Boolean)
                                  .join(', ')}
                                {doc.meetingCount > 0 && `, ${doc.meetingCount} cuộc họp`}
                              </span>
                            </span>
                          </span>
                        </td>
                        <td className="cell-muted col-size">
                          {[doc.pageCount && `${doc.pageCount} trang`, formatBytes(doc.byteSize)]
                            .filter(Boolean)
                            .join(', ')}
                        </td>
                        <td className="col-usage">
                          {doc.meetingCount > 0 && (
                            <span data-tooltip="Số cuộc họp đang dùng tài liệu này">{doc.meetingCount} cuộc họp</span>
                          )}
                        </td>
                        <td className="cell-muted col-date">{formatDate(doc.createdAt)}</td>
                        <td className="col-action" onClick={(e) => e.stopPropagation()}>
                          <span className="row-actions">
                            {/* On a phone the row keeps three actions; the viewer has "Tải về". */}
                            <RowAction
                              className="library-download"
                              icon="download"
                              label="Tải về"
                              name={doc.fileName}
                              disabled={busyId === doc.id}
                              onClick={() => void download(doc)}
                            />
                            <RowAction
                              icon="pencil"
                              label="Đổi tên"
                              name={doc.fileName}
                              onClick={() => setDialog({ kind: 'rename', document: doc })}
                            />
                            <RowAction
                              icon="folderInput"
                              label="Chuyển đến…"
                              name={doc.fileName}
                              onClick={() => setDialog({ kind: 'move', document: doc })}
                            />
                            <RowAction
                              icon="trash"
                              label="Xoá"
                              name={doc.fileName}
                              onClick={() => setDialog({ kind: 'delete', document: doc })}
                            />
                          </span>
                        </td>
                      </tr>
                    )
                  })}
                </>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {view && dialog?.kind === 'upload' && (
        <LibraryUploadDialog
          folderId={folderId}
          folders={folders}
          roomLeft={view.quotaBytes - view.usedBytes}
          onClose={() => setDialog(null)}
          onUploaded={done}
          onStale={(message) => {
            setDialog(null)
            toast.show(message, 'error')
            reload()
          }}
        />
      )}

      {dialog?.kind === 'newFolder' && (
        <NameDialog
          title="Thư mục mới"
          intro={
            <>
              Tạo thư mục trong <strong>{placeName(folders, folderId)}</strong>.
            </>
          }
          label="Tên thư mục"
          maxLength={400}
          submitLabel="Tạo thư mục"
          onSubmit={async (name) => {
            try {
              done((await createFolder(name, folderId)).message)
            } catch (err) {
              if (!stale(err)) throw err
            }
          }}
          onClose={() => setDialog(null)}
        />
      )}

      {dialog?.kind === 'rename' && (
        <RenameDialog dialog={dialog} onDone={done} onStale={stale} onClose={() => setDialog(null)} />
      )}

      {dialog?.kind === 'move' && (
        <MoveDialog
          folders={folders}
          name={'folder' in dialog ? dialog.folder.name : dialog.document.fileName}
          from={'folder' in dialog ? dialog.folder.parentId : dialog.document.folderId}
          excluded={'folder' in dialog ? subtreeOf(folders, dialog.folder.id) : undefined}
          onMove={async (target) => {
            try {
              const saved =
                'folder' in dialog
                  ? await moveFolder(dialog.folder.id, target)
                  : await moveLibraryDocument(dialog.document.id, target)
              done(saved.message)
            } catch (err) {
              if (!stale(err)) throw err
            }
          }}
          onClose={() => setDialog(null)}
        />
      )}

      {dialog?.kind === 'delete' && (
        <DeleteDialog dialog={dialog} folders={folders} onDone={done} onStale={stale} onClose={() => setDialog(null)} />
      )}
    </>
  )
}

function RowAction({
  icon,
  label,
  name,
  className,
  disabled,
  onClick,
}: {
  icon: 'download' | 'pencil' | 'folderInput' | 'trash'
  label: string
  name: string
  className?: string
  disabled?: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      className={className ? `icon-button icon-button-sm ${className}` : 'icon-button icon-button-sm'}
      aria-label={`${label.replace('…', '')} ${name}`}
      data-tooltip={label}
      disabled={disabled}
      onClick={onClick}
    >
      <Icon name={icon} size={16} />
    </button>
  )
}

function RenameDialog({
  dialog,
  onDone,
  onStale,
  onClose,
}: {
  dialog: Extract<Dialog, { kind: 'rename' }>
  onDone: (message: string) => void
  onStale: (err: unknown) => boolean
  onClose: () => void
}) {
  const folder = 'folder' in dialog ? dialog.folder : null
  const document = 'document' in dialog ? dialog.document : null
  const name = folder ? folder.name : document!.fileName
  const extension = folder ? '' : extensionOf(name)
  return (
    <NameDialog
      title="Đổi tên"
      intro={
        <>
          Đặt tên mới cho <strong>{name}</strong>.{!folder && ' Cuộc họp đang dùng tài liệu này vẫn giữ tên cũ.'}
        </>
      }
      label={folder ? 'Tên thư mục' : 'Tên tài liệu'}
      initial={extension ? name.slice(0, -extension.length) : name}
      extension={extension}
      maxLength={folder ? 400 : 255}
      submitLabel="Lưu"
      onSubmit={async (next) => {
        try {
          const saved = folder ? await renameFolder(folder.id, next) : await renameLibraryDocument(document!.id, next)
          onDone(saved.message)
        } catch (err) {
          if (!onStale(err)) throw err
        }
      }}
      onClose={onClose}
    />
  )
}

function DeleteDialog({
  dialog,
  folders,
  onDone,
  onStale,
  onClose,
}: {
  dialog: Extract<Dialog, { kind: 'delete' }>
  folders: LibraryFolder[]
  onDone: (message: string) => void
  onStale: (err: unknown) => boolean
  onClose: () => void
}) {
  const folder = 'folder' in dialog ? dialog.folder : null
  const document = 'document' in dialog ? dialog.document : null
  // Sub-folders are known here; documents inside another folder are not, so the server has the last word (MSG196).
  const hasSubfolders = folder ? folders.some((f) => f.parentId === folder.id) : false
  return (
    <ConfirmDialog
      title={folder ? 'Xoá thư mục?' : 'Xoá tài liệu?'}
      confirmLabel={folder ? 'Xoá thư mục' : 'Xoá tài liệu'}
      pendingLabel="Đang xoá…"
      cancelLabel={folder ? 'Giữ thư mục' : 'Giữ tài liệu'}
      tone="danger"
      blocker={hasSubfolders ? 'Chỉ xoá được thư mục rỗng.' : undefined}
      onConfirm={async () => {
        try {
          const result = folder ? await deleteFolder(folder.id) : await deleteLibraryDocument(document!.id)
          onDone(result.message)
        } catch (err) {
          if (!onStale(err)) throw err
        }
      }}
      onClose={onClose}
    >
      {folder ? (
        <>
          Thư mục <strong>{folder.name}</strong> sẽ bị xoá khỏi Kho tài liệu của bạn.
        </>
      ) : (
        <>
          Xoá <strong>{document!.fileName}</strong> khỏi Kho tài liệu của bạn.
          {document!.meetingCount > 0 &&
            ` Tài liệu đang dùng trong ${document!.meetingCount} cuộc họp; các cuộc họp đó vẫn đọc được.`}
        </>
      )}
    </ConfirmDialog>
  )
}
