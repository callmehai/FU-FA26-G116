import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { getMeetingDocumentContent } from '../../api/documents'
import { Icon } from '../../components/Icon'
import { NotFound } from '../../components/NotFound'
import { errorMessage, isApiError } from '../../lib/errors'
import { routes } from '../../lib/routes'
import type { MeetingDocument } from '../../types/document'
import type { Meeting } from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'
import { SensitivityBadge } from '../meetings/StatusBadge'
import { useMeeting } from '../meetings/useMeeting'
import { isPdf } from './documentFile'
import { FileTypeTag, OnlineOnly } from './DocumentTags'
import { DocxView, PdfView } from './renderers'
import { useDocumentDownload } from './useDocumentDownload'
import { useMeetingDocuments } from './useMeetingDocuments'

/** 100% fits the page to the window, up to a comfortable reading width. */
const ZOOMS = [0.5, 0.75, 1, 1.25, 1.5, 2]

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
 * Comments (UC-35, SCRUM-23) come later.
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
        <Viewer meeting={meeting.state.meeting} doc={doc} content={content} onStale={meeting.reload} />
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
  const { download, busyId } = useDocumentDownload(meeting.id, onStale)
  const pdf = isPdf(doc)
  const canDownload = meeting.sensitivity !== 'Secret'

  return (
    <>
      <header className="viewer-head">
        <BackToDocuments meetingId={meeting.id} />
        <FileTypeTag pdf={pdf} />
        <h1 className="viewer-title" data-tooltip={doc.fileName}>
          {doc.fileName}
        </h1>
        {meeting.sensitivity !== 'Normal' && <SensitivityBadge sensitivity={meeting.sensitivity} />}
        <span className="viewer-meeting">{meeting.title}</span>
        <span className="viewer-spacer" />
        {pdf && page && (
          <span className="viewer-page" aria-live="polite">
            Trang {page.current} / {page.total}
          </span>
        )}
        <div className="viewer-zoom" role="group" aria-label="Thu phóng">
          <button
            type="button"
            className="icon-button icon-button-sm"
            aria-label="Thu nhỏ"
            data-tooltip="Thu nhỏ"
            disabled={zoom === 0}
            onClick={() => setZoom(zoom - 1)}
          >
            <Icon name="zoomOut" size={16} />
          </button>
          <span className="viewer-zoom-value">{ZOOMS[zoom] * 100}%</span>
          <button
            type="button"
            className="icon-button icon-button-sm"
            aria-label="Phóng to"
            data-tooltip="Phóng to"
            disabled={zoom === ZOOMS.length - 1}
            onClick={() => setZoom(zoom + 1)}
          >
            <Icon name="zoomIn" size={16} />
          </button>
        </div>
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

      <main className="viewer-stage">
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
    </>
  )
}

/**
 * "‹ Tài liệu": a tab of its own (opened from the call room) closes, so the room's tab comes back;
 * otherwise back to the meeting's Tài liệu tab. A browser that refuses to close the tab navigates.
 */
function BackToDocuments({ meetingId }: { meetingId: string }) {
  const navigate = useNavigate()
  const to = `${routes.meeting(meetingId)}?tab=documents`
  return (
    <Link
      className="back-link viewer-back"
      to={to}
      onClick={(event) => {
        if (window.history.length > 1) return
        event.preventDefault()
        window.close()
        setTimeout(() => navigate(to))
      }}
    >
      <Icon name="chevronLeft" size={16} />
      Tài liệu
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
