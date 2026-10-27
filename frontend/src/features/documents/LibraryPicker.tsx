import { useEffect, useState } from 'react'
import { getLibrary } from '../../api/library'
import { Icon } from '../../components/Icon'
import { errorMessage } from '../../lib/errors'
import type { LibraryDocument, LibraryView } from '../../types/document'
import { useAccessLost } from '../auth/useAccessLost'
import { formatBytes, isPdf } from './documentFile'
import { FileTypeTag } from './DocumentTags'
import { childrenOf, LIBRARY_ROOT, trailOf } from './libraryTree'

/**
 * Choosing documents from one's own library (UC-33a, UC-33b AF-2): one folder at a time, as the library page
 * reads, with a checkbox per document. The choice survives moving between folders. `reloadKey` re-reads the
 * place after the server said a chosen document is gone (MSG249).
 */
export function LibraryPicker({
  selected,
  onToggle,
  reloadKey = 0,
}: {
  selected: Map<string, LibraryDocument>
  onToggle: (doc: LibraryDocument) => void
  reloadKey?: number
}) {
  const handleAccessLost = useAccessLost()
  const [folderId, setFolderId] = useState<string | null>(null)
  const [view, setView] = useState<LibraryView | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let live = true
    getLibrary(folderId)
      .then((data) => {
        if (!live) return
        setError(null)
        setView(data)
      })
      .catch((err) => {
        if (!live || handleAccessLost(err)) return
        // The folder vanished in another tab (MSG175): back to the root rather than a dead end.
        if (folderId !== null) setFolderId(null)
        else setError(errorMessage(err))
      })
    return () => {
      live = false
    }
  }, [folderId, attempt, reloadKey, handleAccessLost])

  const trail = view ? trailOf(view.folders, folderId) : []
  const folders = view ? childrenOf(view.folders, folderId) : []
  const loading = view === null || view.folderId !== folderId

  return (
    <div className="picker">
      <nav className="picker-trail" aria-label="Vị trí trong kho">
        <button type="button" className="picker-crumb" onClick={() => setFolderId(null)} disabled={folderId === null}>
          <Icon name="archive" size={16} />
          {LIBRARY_ROOT}
        </button>
        {trail.map((f) => (
          <span key={f.id} className="picker-crumb-wrap">
            <Icon name="chevronRight" size={14} />
            <button
              type="button"
              className="picker-crumb"
              onClick={() => setFolderId(f.id)}
              disabled={f.id === folderId}
            >
              {f.name}
            </button>
          </span>
        ))}
      </nav>

      {error ? (
        <div className="alert alert-error" role="alert">
          <p>{error}</p>
          <button
            type="button"
            className="btn btn-outline btn-sm"
            onClick={() => {
              setError(null)
              setAttempt((n) => n + 1)
            }}
          >
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      ) : (
        <ul className="picker-list" aria-busy={loading} aria-label="Thư mục và tài liệu">
          {!view ? (
            <li className="picker-row">
              <span className="skeleton documents-skeleton" />
            </li>
          ) : (
            <>
              {folders.map((f) => (
                <li key={f.id}>
                  <button type="button" className="picker-row picker-folder" onClick={() => setFolderId(f.id)}>
                    <Icon name="folder" size={18} />
                    <span className="picker-name">{f.name}</span>
                    <Icon name="chevronRight" size={16} />
                  </button>
                </li>
              ))}
              {!loading &&
                view.documents.map((doc) => (
                  <li key={doc.id}>
                    <label className="picker-row">
                      <input type="checkbox" checked={selected.has(doc.id)} onChange={() => onToggle(doc)} />
                      <FileTypeTag pdf={isPdf(doc)} />
                      <span className="picker-name">
                        <strong>{doc.fileName}</strong>
                        <small>
                          {[doc.pageCount && `${doc.pageCount} trang`, formatBytes(doc.byteSize)]
                            .filter(Boolean)
                            .join(', ')}
                        </small>
                      </span>
                    </label>
                  </li>
                ))}
              {!loading && folders.length === 0 && view.documents.length === 0 && (
                <li className="documents-empty">
                  {folderId ? 'Thư mục này chưa có gì.' : 'Kho tài liệu của bạn chưa có gì.'}
                </li>
              )}
            </>
          )}
        </ul>
      )}
    </div>
  )
}
