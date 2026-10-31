import { useCallback, useEffect, useRef, useState, type RefObject } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { getMeetingDocumentContent } from '../../api/documents'
import { Icon } from '../../components/Icon'
import { NotFound } from '../../components/NotFound'
import { errorMessage, isApiError } from '../../lib/errors'
import { routes } from '../../lib/routes'
import type { MeetingDocument } from '../../types/document'
import type { Meeting } from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'
import { CommentsPanel } from './CommentsPanel'
import { SensitivityBadge } from '../meetings/StatusBadge'
import { useMeeting } from '../meetings/useMeeting'
import { ReadConfirmButton, type ReadProgress } from '../votes/ReadConfirmButton'
import { isPdf } from './documentFile'
import { FileTypeTag, OnlineOnly } from './DocumentTags'
import { DocxView, PdfView } from './renderers'
import { ViewerZoom } from './ViewerZoom'
import { ZOOMS } from './zoom'
import { useDocumentDownload } from './useDocumentDownload'
import { useMeetingDocuments } from './useMeetingDocuments'

type Content =
  | { kind: 'loading' }
  | { kind: 'loaded'; blob: Blob }
  | { kind: 'error'; message: string }
  /** MSG156: removed from the meeting, or no longer readable. */
  | { kind: 'gone' }

/**
 * UC-32c Preview Meeting Document (SCRUM-21, wireframe 7.5 / 7.5b), full screen. The server sends
 * the original bytes (BR-183, BR-216) and the browser draws them: a PDF page by page with pdf.js, a
 * DOCX as one flowing text with docx-preview. Every open of a Confidential or Secret meeting's
 * document is audited by the server (BR-318).
 *
 * Opened from the call room it lives in its own tab (BR-315), and "‹ Tài liệu" closes that tab.
 * Comments (UC-35, SCRUM-23) sit beside the document; "Góp ý" shows or hides them.
 */
export function DocumentViewerPage() {
  const { meetingId = '', documentId = '' } = useParams()
  const meeting = useMeeting(meetingId)
  const documents = useMeetingDocuments(meetingId)
  const content = useContent(meetingId, documentId)

  if (meeting.state.kind === 'notFound' || documents.state.kind === 'gone' || content.state.kind === 'gone') {
    return <NotFound />
  }
  const list = documents.state.kind === 'loaded' || documents.state.kind === 'error' ? documents.state.data : null
  const doc =
    list && [...list.agendaItems.flatMap((i) => i.documents), ...list.general].find((d) => d.id === documentId)
  // Listed without it: removed from the meeting while open, or never part of it.
  if (list && !doc) return <NotFound />

  const failure =
    meeting.state.kind === 'error' ? meeting.state : documents.state.kind === 'error' && !list ? documents.state : null

  return (
    <div className="viewer">
      {failure ? (
        <div className="viewer-message">
          <div className="alert alert-error" role="alert">
            <p>{failure.message}</p>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => (meeting.state.kind === 'error' ? meeting.retry() : void documents.reload())}
            >
              <Icon name="refresh" size={16} />
              Thử lại
            </button>
          </div>
        </div>
      ) : meeting.state.kind === 'loaded' && doc ? (
        <Viewer
          meeting={meeting.state.meeting}
          doc={doc}
          content={content}
          // MSG159: the meeting became Secret. MSG156: the document is gone, and the re-read list says so.
          onStale={() => {
            void meeting.reload()
            void documents.reload()
          }}
        />
      ) : (
        <div className="viewer-message" aria-busy="true">
          Đang tải tài liệu…
        </div>
      )}
    </div>
  )
}

function Viewer({
  meeting,
  doc,
  content,
  onStale,
}: {
  meeting: Meeting
  doc: MeetingDocument
  content: ReturnType<typeof useContent>
  onStale: () => void
}) {
  const [zoom, setZoom] = useState(2)
  const [page, setPage] = useState<{ current: number; total: number } | null>(null)
  // Open beside a wide document; on a narrow screen the panel would cover it, so it waits to be asked for.
  const [commentsOpen, setCommentsOpen] = useState(() => window.innerWidth >= 1100)
  const [commentCount, setCommentCount] = useState(doc.commentCount)
  const { download, busyId } = useDocumentDownload(meeting.id, onStale)
  const pdf = isPdf(doc)
  const canDownload = meeting.sensitivity !== 'Secret'
  // Opened from a vote still waiting on this reader (UC-74): confirm once the last page is reached.
  const [params] = useSearchParams()
  const reading = params.get('read') === '1'
  const stage = useRef<HTMLElement>(null)
  const progress = useReachedEnd(stage, reading && content.state.kind === 'loaded')

  return (
    <>
      <header className="viewer-head">
        <BackToDocuments meetingId={meeting.id} fromVote={reading} />
        <FileTypeTag pdf={pdf} />
        <h1 className="viewer-title" data-tooltip={doc.fileName}>
          {doc.fileName}
        </h1>
        {/* On a phone the name gets the first line to itself, the controls go under it. */}
        <span className="viewer-break" aria-hidden="true" />
        {meeting.sensitivity !== 'Normal' && <SensitivityBadge sensitivity={meeting.sensitivity} />}
        <span className="viewer-meeting">{meeting.title}</span>
        <span className="viewer-spacer" />
        {pdf && page && (
          <span className="viewer-page" aria-live="polite">
            Trang {page.current} / {page.total}
          </span>
        )}
        <ViewerZoom zoom={zoom} onZoom={setZoom} />
        {reading && <ReadConfirmButton meetingId={meeting.id} meetingDocumentId={doc.id} progress={progress} />}
        <button
          type="button"
          className={commentsOpen ? 'btn btn-outline is-active' : 'btn btn-outline'}
          aria-pressed={commentsOpen}
          onClick={() => setCommentsOpen(!commentsOpen)}
        >
          <Icon name="messageSquare" size={16} />
          <span className="viewer-download-label">Góp ý</span>
          {commentCount > 0 && <span className="viewer-count">{commentCount}</span>}
        </button>
        {canDownload ? (
          <button
            type="button"
            className="btn btn-outline"
            disabled={busyId === doc.id}
            onClick={() => void download(doc)}
          >
            <Icon name="download" size={16} />
            <span className="viewer-download-label">Tải về</span>
          </button>
        ) : (
          <OnlineOnly />
        )}
      </header>

      <div className="viewer-body">
        <main className="viewer-stage" ref={stage}>
          {content.state.kind === 'error' ? (
            <div className="viewer-message">
              <div className="alert alert-error" role="alert">
                <p>{content.state.message}</p>
                <button type="button" className="btn btn-outline btn-sm" onClick={content.retry}>
                  <Icon name="refresh" size={16} />
                  Thử lại
                </button>
              </div>
            </div>
          ) : content.state.kind === 'loaded' ? (
            pdf ? (
              <PdfView blob={content.state.blob} zoom={ZOOMS[zoom]} onPage={setPage} />
            ) : (
              <DocxView blob={content.state.blob} zoom={ZOOMS[zoom]} />
            )
          ) : (
            <div className="viewer-message" aria-busy="true">
              Đang tải tài liệu…
            </div>
          )}
        </main>
        {commentsOpen && (
          <CommentsPanel
            meetingId={meeting.id}
            meetingDocumentId={doc.id}
            pdf={pdf}
            currentPage={page?.current ?? null}
            closedNote={
              meeting.status === 'Cancelled'
                ? 'Cuộc họp đã huỷ nên không góp ý thêm được.'
                : 'Cuộc họp đã kết thúc nên không góp ý thêm được.'
            }
            onCount={setCommentCount}
            onGone={onStale}
            onClose={() => setCommentsOpen(false)}
          />
        )}
      </div>
    </>
  )
}

/**
 * `reached` once the reader has scrolled to the end of what is drawn (BR-290) — the last PDF page or the
 * end of a DOCX — or the whole document fits on screen. Nothing drawn yet is never the end. `unreadable`
 * when the renderer could not draw the file, so the reader is told instead of waiting on a locked button.
 * Either one stays. Checked on scroll and every second, since pages keep drawing after the file loads.
 */
function useReachedEnd(stage: RefObject<HTMLElement | null>, active: boolean) {
  const [progress, setProgress] = useState<ReadProgress>('reading')
  useEffect(() => {
    const el = stage.current
    if (!active || progress !== 'reading' || !el) return
    const check = () => {
      const scroller = el.querySelector<HTMLElement>('.viewer-scroll')
      if (scroller?.querySelector('.viewer-message .alert-error')) return setProgress('unreadable')
      if (!scroller?.querySelector('.pdf-page, .docx-sheet > div > *')) return
      if (scroller.scrollTop + scroller.clientHeight >= scroller.scrollHeight - 8) setProgress('reached')
    }
    // Scroll does not bubble: listen in the capture phase for the renderer's own scroll box.
    el.addEventListener('scroll', check, { capture: true, passive: true })
    const timer = window.setInterval(check, 1000)
    return () => {
      el.removeEventListener('scroll', check, { capture: true })
      window.clearInterval(timer)
    }
  }, [stage, active, progress])
  return progress
}

/**
 * "‹ Tài liệu": the tab the call room opened (`?from=room`, nothing before it) closes, so the room's
 * tab comes back; otherwise back to the meeting's Tài liệu tab — or its Biểu quyết tab when the
 * document was opened to be read for a vote. A browser that refuses to close the tab navigates instead.
 */
function BackToDocuments({ meetingId, fromVote }: { meetingId: string; fromVote: boolean }) {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const to = `${routes.meeting(meetingId)}?tab=${fromVote ? 'votes' : 'documents'}`
  return (
    <Link
      className="back-link viewer-back"
      to={to}
      onClick={(event) => {
        if (params.get('from') !== 'room' || window.history.length > 1) return
        event.preventDefault()
        window.close()
        setTimeout(() => navigate(to))
      }}
    >
      <Icon name="chevronLeft" size={16} />
      {fromVote ? 'Biểu quyết' : 'Tài liệu'}
    </Link>
  )
}

/** The file's bytes, read once per open; MSG158 is temporary, so "Thử lại" reads again. */
function useContent(meetingId: string, documentId: string) {
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<Content>({ kind: 'loading' })
  const generation = useRef(0)

  const load = useCallback(async () => {
    const current = ++generation.current
    try {
      const blob = await getMeetingDocumentContent(meetingId, documentId)
      if (current === generation.current) setState({ kind: 'loaded', blob })
    } catch (err) {
      if (current !== generation.current || handleAccessLost(err)) return
      setState(isApiError(err, 404) ? { kind: 'gone' } : { kind: 'error', message: errorMessage(err) })
    }
  }, [meetingId, documentId, handleAccessLost])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  const retry = useCallback(() => {
    setState({ kind: 'loading' })
    void load()
  }, [load])

  return { state, retry }
}
