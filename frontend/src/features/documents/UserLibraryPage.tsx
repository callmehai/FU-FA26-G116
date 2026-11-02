import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { getUserLibrary, getUserLibraryContent } from '../../api/library'
import { getUser } from '../../api/users'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDate } from '../../lib/format'
import { routes } from '../../lib/routes'
import type { LibraryDocument, LibraryView } from '../../types/document'
import type { UserAccount } from '../../types/user'
import { useAccessLost } from '../auth/useAccessLost'
import { formatBytes, isPdf, saveBlob } from './documentFile'
import { FileTypeTag } from './DocumentTags'
import { childrenOf, trailOf } from './libraryTree'

const COLUMN_COUNT = 5

/**
 * A deactivated person's library, read by an administrator to recover their documents (SCRUM-113, UC-34b): the
 * personal library's table without anything that changes it. Every listing and every file opened is audited by the
 * server, which also leaves out whatever belongs to a Confidential or Secret meeting. An account that is active again,
 * or gone, answers 404 MSG175: back to the user list.
 */
export function UserLibraryPage() {
  const { userId = '' } = useParams()
  const [params] = useSearchParams()
  const folderId = params.get('folder') || null
  const navigate = useNavigate()
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [owner, setOwner] = useState<UserAccount | null>(null)
  const [view, setView] = useState<LibraryView | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  const [busyId, setBusyId] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    // Only the name for the title. An account that is gone is the library's 404 below, said once.
    getUser(userId)
      .then((account) => live && setOwner(account))
      .catch((err) => {
        if (live) handleAccessLost(err)
      })
    return () => {
      live = false
    }
  }, [userId, handleAccessLost])

  useEffect(() => {
    let live = true
    getUserLibrary(userId, folderId)
      .then((data) => {
        if (!live) return
        setLoadError(null)
        setView(data)
      })
      .catch((err) => {
        if (!live || handleAccessLost(err)) return
        if (!isApiError(err, 404, 'MSG175')) setLoadError(errorMessage(err))
        else if (folderId) {
          // A folder that is gone, or one of a restricted meeting: the root of the same library.
          toast.show(errorMessage(err), 'error')
          navigate(routes.userLibrary(userId), { replace: true })
        } else {
          toast.show(
            'Tài khoản này đang hoạt động hoặc không còn tồn tại: kho tài liệu chỉ chủ tài khoản xem được.',
            'error',
          )
          navigate(routes.adminUsers, { replace: true })
        }
      })
    return () => {
      live = false
    }
  }, [userId, folderId, attempt, handleAccessLost, navigate, toast])

  const reload = () => {
    setLoadError(null)
    setAttempt((n) => n + 1)
  }

  const folders = view?.folders ?? []
  const trail = trailOf(folders, folderId)
  const here = trail.at(-1)
  const loading = view === null || view.folderId !== folderId
  const rows = loading ? null : { subfolders: childrenOf(folders, folderId), documents: view.documents }
  const ownerName = owner?.fullName
  const rootLabel = ownerName ? `Kho của ${ownerName}` : 'Kho tài liệu'

  async function download(doc: LibraryDocument) {
    setBusyId(doc.id)
    try {
      saveBlob(await getUserLibraryContent(userId, doc.id, true), doc.fileName)
    } catch (err) {
      if (handleAccessLost(err)) return
      toast.show(errorMessage(err), 'error')
      if (isApiError(err, 404)) reload()
    } finally {
      setBusyId(null)
    }
  }

  return (
    <>
      <nav className="library-trail" aria-label="Vị trí trong kho">
        <Link to={routes.adminUsers} className="library-crumb">
          <Icon name="users" size={16} />
          <span className="library-crumb-root">Người dùng</span>
        </Link>
        {/* At the root the page title names the library; inside a folder the trail leads back to it. */}
        {folderId && (
          <span className="library-crumb-wrap">
            <Icon name="chevronRight" size={14} />
            <Link to={routes.userLibrary(userId)} className="library-crumb">
              {rootLabel}
            </Link>
          </span>
        )}
        {trail.map((f, i) => (
          <span key={f.id} className="library-crumb-wrap">
            <Icon name="chevronRight" size={14} />
            {i === trail.length - 1 ? (
              <span className="library-crumb" aria-current="page">
                {f.name}
              </span>
            ) : (
              <Link to={routes.userLibrary(userId, f.id)} className="library-crumb">
                {f.name}
              </Link>
            )}
          </span>
        ))}
      </nav>

      <PageHeader
        title={here?.name ?? (ownerName ? `Kho tài liệu của ${ownerName}` : 'Kho tài liệu')}
        description="Chỉ xem. Mỗi lần mở thư mục hoặc tài liệu đều được ghi vào nhật ký."
      />

      <div className="list-panel list-page list-fill">
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
                    <p>{folderId ? 'Thư mục này chưa có gì.' : 'Kho tài liệu này chưa có gì.'}</p>
                  </td>
                </tr>
              ) : (
                <>
                  {rows.subfolders.map((f) => {
                    const open = routes.userLibrary(userId, f.id)
                    return (
                      <tr key={f.id} className="row-link" onClick={() => navigate(open)}>
                        <td className="cell-title">
                          <span className="library-name">
                            <Icon name="folder" size={22} />
                            <span className="library-name-col">
                              <Link to={open} onClick={(e) => e.stopPropagation()} className="library-name-text">
                                {f.name}
                              </Link>
                              <span className="cell-sub library-sub">Thư mục</span>
                            </span>
                          </span>
                        </td>
                        <td className="cell-muted col-size">Thư mục</td>
                        <td className="col-usage" />
                        <td className="cell-muted col-date">{formatDate(f.createdAt)}</td>
                        <td className="col-action" />
                      </tr>
                    )
                  })}
                  {rows.documents.map((doc) => {
                    const open = routes.userLibraryDocument(userId, doc.id, folderId)
                    const facts = [doc.pageCount && `${doc.pageCount} trang`, formatBytes(doc.byteSize)]
                      .filter(Boolean)
                      .join(', ')
                    return (
                      <tr key={doc.id} className="row-link" onClick={() => navigate(open)}>
                        <td className="cell-title">
                          <span className="library-name">
                            <FileTypeTag pdf={isPdf(doc)} size={28} />
                            <span className="library-name-col">
                              <Link to={open} onClick={(e) => e.stopPropagation()} className="library-name-text">
                                {doc.fileName}
                              </Link>
                              <span className="cell-sub library-sub">{facts}</span>
                            </span>
                          </span>
                        </td>
                        <td className="cell-muted col-size">{facts}</td>
                        <td className="col-usage">
                          {doc.meetingCount > 0 && (
                            <span data-tooltip="Số cuộc họp bạn xem được đang dùng tài liệu này">
                              {doc.meetingCount} cuộc họp
                            </span>
                          )}
                        </td>
                        <td className="cell-muted col-date">{formatDate(doc.createdAt)}</td>
                        <td className="col-action" onClick={(e) => e.stopPropagation()}>
                          <span className="row-actions">
                            <button
                              type="button"
                              className="icon-button icon-button-sm"
                              aria-label={`Tải về ${doc.fileName}`}
                              data-tooltip="Tải về"
                              disabled={busyId === doc.id}
                              onClick={() => void download(doc)}
                            >
                              <Icon name="download" size={16} />
                            </button>
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
    </>
  )
}
