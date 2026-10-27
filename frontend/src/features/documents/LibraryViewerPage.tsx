import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { getLibrary, getLibraryContent } from '../../api/library'
import { Icon } from '../../components/Icon'
import { NotFound } from '../../components/NotFound'
import { errorMessage, isApiError } from '../../lib/errors'
import { routes } from '../../lib/routes'
import type { LibraryDocument } from '../../types/document'
import { useAccessLost } from '../auth/useAccessLost'
import { isPdf, saveBlob } from './documentFile'
import { FileTypeTag } from './DocumentTags'
import { DocxView, PdfView } from './renderers'
import { ViewerZoom } from './ViewerZoom'
import { ZOOMS } from './zoom'

type State =
  | { kind: 'loading' }
  | { kind: 'loaded'; doc: LibraryDocument; blob: Blob }
  | { kind: 'error'; message: string }
  | { kind: 'gone' }

/**
 * A document of one's own library, full screen, drawn as a meeting document is (SCRUM-111). Its name and type
 * come from the folder it is listed in (`?folder=`), its bytes from the content endpoint; someone else's or a
 * deleted document answers 404 MSG175, which shows the shared NotFound.
 */
export function LibraryViewerPage() {
  const { documentId = '' } = useParams()
  const [params] = useSearchParams()
  const folderId = params.get('folder') || null
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const [zoom, setZoom] = useState(2)
  const [page, setPage] = useState<{ current: number; total: number } | null>(null)

  useEffect(() => {
    let live = true
    Promise.all([getLibrary(folderId), getLibraryContent(documentId)])
      .then(([view, blob]) => {
        if (!live) return
        const doc = view.documents.find((d) => d.id === documentId)
        setState(doc ? { kind: 'loaded', doc, blob } : { kind: 'gone' })
      })
      .catch((err) => {
        if (!live || handleAccessLost(err)) return
        setState(isApiError(err, 404) ? { kind: 'gone' } : { kind: 'error', message: errorMessage(err) })
      })
    return () => {
      live = false
    }
  }, [documentId, folderId, attempt, handleAccessLost])

  if (state.kind === 'gone') return <NotFound />
  const back = `${routes.documents}${folderId ? `?folder=${folderId}` : ''}`

  return (
    <div className="viewer">
      <header className="viewer-head">
        <Link className="back-link viewer-back" to={back}>
          <Icon name="chevronLeft" size={16} />
          Kho tài liệu
        </Link>
        {state.kind === 'loaded' && (
          <>
            <FileTypeTag pdf={isPdf(state.doc)} />
            <h1 className="viewer-title" data-tooltip={state.doc.fileName}>
              {state.doc.fileName}
            </h1>
            <span className="viewer-break" aria-hidden="true" />
            <span className="viewer-spacer" />
            {isPdf(state.doc) && page && (
              <span className="viewer-page" aria-live="polite">
                Trang {page.current} / {page.total}
              </span>
            )}
            <ViewerZoom zoom={zoom} onZoom={setZoom} />
            {/* The bytes are already here: saving them needs no second request. */}
            <button type="button" className="btn btn-outline" onClick={() => saveBlob(state.blob, state.doc.fileName)}>
              <Icon name="download" size={16} />
              <span className="viewer-download-label">Tải về</span>
            </button>
          </>
        )}
      </header>

      <main className="viewer-stage">
        {state.kind === 'error' ? (
          <div className="viewer-message">
            <div className="alert alert-error" role="alert">
              <p>{state.message}</p>
              <button
                type="button"
                className="btn btn-outline btn-sm"
                onClick={() => {
                  setState({ kind: 'loading' })
                  setAttempt((n) => n + 1)
                }}
              >
                <Icon name="refresh" size={16} />
                Thử lại
              </button>
            </div>
          </div>
        ) : state.kind === 'loaded' ? (
          isPdf(state.doc) ? (
            <PdfView blob={state.blob} zoom={ZOOMS[zoom]} onPage={setPage} />
          ) : (
            <DocxView blob={state.blob} zoom={ZOOMS[zoom]} />
          )
        ) : (
          <div className="viewer-message" aria-busy="true">
            Đang tải tài liệu…
          </div>
        )}
      </main>
    </div>
  )
}
