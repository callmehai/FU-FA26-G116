import { lazy, Suspense, useCallback, useLayoutEffect, useRef, type ReactNode } from 'react'
import { useLocation, useNavigate, type Location } from 'react-router-dom'
import {
  DocumentOverlayContext,
  VIEWER_CLOSED_EVENT,
  VIEWER_STATE_KEY,
  type ViewerTarget,
} from './viewerOverlay'

// pdf.js and docx-preview load with the viewer, not with every page that lists a document.
const MeetingDocumentViewer = lazy(() =>
  import('./DocumentViewerPage').then((m) => ({ default: m.MeetingDocumentViewer })),
)
const LibraryViewer = lazy(() => import('./LibraryViewerPage').then((m) => ({ default: m.LibraryViewer })))

type ViewerState = Record<string, unknown> & { [VIEWER_STATE_KEY]?: ViewerTarget }

const viewerOf = (location: Location) => (location.state as ViewerState | null)?.[VIEWER_STATE_KEY]

/**
 * Reading a document opens it over the page it was picked from (Hải 19/11/2026: "mở panel để đọc tài liệu thôi
 * ko mở tab mới"), so the call room keeps its call and a list keeps its place, filters and open folds. The open
 * document lives in the history entry's state: Back closes it, as it closed the viewer's own page before, and
 * leaving the page closes it with the page. The full-screen routes stay for a link opened in a new tab.
 */
export function DocumentOverlayProvider({ children }: { children: ReactNode }) {
  const location = useLocation()
  const navigate = useNavigate()
  const target = viewerOf(location)
  // Stable, so the panels that hold it do not re-render on every navigation.
  const here = useRef(location)
  useLayoutEffect(() => {
    here.current = location
  }, [location])

  const open = useCallback(
    (next: ViewerTarget) => {
      const { pathname, search, hash, state } = here.current
      navigate({ pathname, search, hash }, { state: { ...(state as ViewerState | null), [VIEWER_STATE_KEY]: next } })
    },
    [navigate],
  )

  const close = useCallback(() => {
    const { pathname, search, hash, state } = here.current
    const rest = { ...(state as ViewerState | null) }
    delete rest[VIEWER_STATE_KEY]
    // The entry under this one is the same page without the viewer; a reloaded tab may have none.
    if (((window.history.state as { idx?: number } | null)?.idx ?? 0) > 0) navigate(-1)
    else navigate({ pathname, search, hash }, { replace: true, state: rest })
    window.dispatchEvent(new Event(VIEWER_CLOSED_EVENT))
  }, [navigate])

  return (
    <DocumentOverlayContext value={open}>
      {children}
      {target && (
        <ViewerDialog key={`${target.kind}:${target.documentId}`} onClose={close}>
          <Suspense
            fallback={
              <div className="viewer">
                <div className="viewer-message" aria-busy="true">
                  Đang tải tài liệu…
                </div>
              </div>
            }
          >
            {target.kind === 'meeting' ? (
              <MeetingDocumentViewer
                meetingId={target.meetingId}
                documentId={target.documentId}
                reading={!!target.read}
                onClose={close}
              />
            ) : (
              <LibraryViewer
                documentId={target.documentId}
                folderId={target.folderId}
                userId={target.userId}
                onClose={close}
              />
            )}
          </Suspense>
        </ViewerDialog>
      )}
    </DocumentOverlayContext>
  )
}

/** A modal over the page: the native dialog traps focus and closes on Escape; the page behind stays as it was. */
function ViewerDialog({ onClose, children }: { onClose: () => void; children: ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null)
  useLayoutEffect(() => {
    const dialog = ref.current
    if (dialog && !dialog.open) dialog.showModal()
    return () => dialog?.close()
  }, [])
  return (
    <dialog
      ref={ref}
      className="viewer-dialog"
      aria-label="Xem tài liệu"
      onCancel={(event) => {
        if (event.target !== event.currentTarget) return
        event.preventDefault()
        onClose()
      }}
    >
      {children}
    </dialog>
  )
}
