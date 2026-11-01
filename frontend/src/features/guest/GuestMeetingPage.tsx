import { useCallback, useEffect, useState } from 'react'
import { useOutletContext, useParams, useSearchParams } from 'react-router-dom'
import { endGuestSession } from '../../api/guest'
import { getMyInvitationResponse } from '../../api/participants'
import { BrandMark } from '../../components/BrandMark'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatSchedule } from '../../lib/format'
import type { InvitationResponseResult } from '../../types/participant'
import { AgendaPanel } from '../agenda/AgendaPanel'
import { DocumentsPanel } from '../documents/DocumentsPanel'
import { DocumentViewer } from '../documents/DocumentViewerPage'
import { StatusBadge } from '../meetings/StatusBadge'
import { useMeetingEvents } from '../meetings/useMeetingEvents'
import { ParticipantsPanel } from '../participants/ParticipantsPanel'
import { RsvpButtons, RsvpChip } from '../participants/Rsvp'
import { VotingPanel } from '../votes/VotingPanel'
import { useGuest } from './guestContext'
import type { GuestOutletContext } from './GuestPage'
import { dropGuestSession, readGuestEntry } from './guestStore'

const TABS = [
  { id: 'agenda', label: 'Nội dung cuộc họp' },
  { id: 'documents', label: 'Tài liệu' },
  { id: 'votes', label: 'Biểu quyết' },
  { id: 'attendees', label: 'Thành phần tham dự' },
] as const

type TabId = (typeof TABS)[number]['id']

/**
 * The invited meeting as a verified guest sees it (UC-04 step 6, SCRUM-121): what a Member reads —
 * agenda, documents (SCRUM-123), votes, roster — and the invitation to answer (6.4, UC-29). The shared
 * panels take what the guest may do from the server, which grants a guest nothing to manage. No sidebar
 * and no internal menu (BR-156): the bar holds the logo, who the guest said they are, and "Thoát".
 */
export function GuestMeetingPage() {
  const guest = useGuest()!
  const meeting = guest.session.meeting
  const [params] = useSearchParams()
  // `?tab=` is where the document viewer's back link lands.
  const [tab, setTab] = useState<TabId>(() => TABS.find((t) => t.id === params.get('tab'))?.id ?? 'agenda')
  const invitation = useMyInvitation(meeting.id)

  return (
    <div className="guest-page">
      <GuestBar />
      <main className="guest-main">
        <div className="record-head">
          <div className="record-head-main">
            <div className="record-title">
              <h1>{meeting.title}</h1>
              <StatusBadge status={meeting.status} />
              <RsvpChip response={invitation.current?.response ?? null} />
            </div>
            <p className="record-facts">
              <span>
                <Icon name="clock" size={16} />
                {formatSchedule(meeting.plannedStartTime, meeting.plannedEndTime)}
              </span>
            </p>
          </div>
        </div>

        {invitation.current?.canRespond && (
          <section className="workflow-bar workflow-blue workflow-bar-compact" aria-labelledby="rsvp-title">
            <span className="workflow-icon" aria-hidden="true">
              <Icon name="calendar" size={18} />
            </span>
            <div className="workflow-body">
              <h2 id="rsvp-title">Bạn có tham dự cuộc họp này?</h2>
            </div>
            <div className="workflow-actions">
              <RsvpButtons meetingId={meeting.id} labelledBy="rsvp-title" onAnswered={invitation.reload} />
            </div>
          </section>
        )}

        <div className="tabs" role="tablist" aria-label="Cuộc họp">
          {TABS.map((t) => (
            <button
              key={t.id}
              type="button"
              role="tab"
              aria-selected={tab === t.id}
              className={tab === t.id ? 'tab tab-active' : 'tab'}
              onClick={() => setTab(t.id)}
            >
              {t.label}
            </button>
          ))}
        </div>

        <section>
          {tab === 'agenda' ? (
            <AgendaPanel meetingId={meeting.id} variant="page" />
          ) : tab === 'documents' ? (
            <DocumentsPanel meeting={meeting} variant="page" onMeetingStale={guest.recheck} />
          ) : tab === 'votes' ? (
            <VotingPanel meetingId={meeting.id} variant="page" />
          ) : (
            <ParticipantsPanel meetingId={meeting.id} onChanged={invitation.reload} />
          )}
        </section>
      </main>
    </div>
  )
}

/** UC-32c for a guest (SCRUM-123): the shared viewer, fed by the guest's session instead of the meeting API. */
export function GuestDocumentPage() {
  const guest = useGuest()!
  const { documentId = '' } = useParams()
  const meetingId = guest.session.meeting.id
  return (
    <DocumentViewer
      meetingId={meetingId}
      documentId={documentId}
      meeting={{
        state: { kind: 'loaded', meeting: guest.session.meeting },
        retry: guest.recheck,
        reload: guest.recheck,
      }}
    />
  )
}

/** The logo, and the guest as they named themselves — self-declared, unlike the verified email in the tooltip. */
function GuestBar() {
  const guest = useGuest()!
  const toast = useToast()
  const { signedOut } = useOutletContext<GuestOutletContext>()
  const [leaving, setLeaving] = useState(false)
  const { session, invitationId } = guest
  const name = session.displayName ?? session.invitedName

  async function leave() {
    const token = readGuestEntry(invitationId).accessToken
    setLeaving(true)
    try {
      if (token) await endGuestSession(token)
      dropGuestSession(invitationId)
      signedOut()
    } catch (err) {
      setLeaving(false)
      toast.show(errorMessage(err), 'error')
    }
  }

  return (
    <header className="guest-bar">
      <span className="guest-brand">
        <BrandMark size={28} />
        <span className="guest-brand-text">TrustCabinet</span>
      </span>
      <span
        className="guest-who"
        tabIndex={0}
        data-tooltip={`${session.email} đã xác minh · tên${session.jobTitle ? ' và chức vụ' : ''} do bạn tự khai`}
      >
        <Icon name="user" size={16} />
        <span className="guest-who-name">
          {name}
          {session.jobTitle && <small>{session.jobTitle}</small>}
        </span>
        <span className="badge badge-slate">Khách</span>
      </span>
      <button
        type="button"
        className="btn btn-ghost"
        aria-label="Thoát"
        disabled={leaving}
        onClick={() => void leave()}
      >
        <Icon name="logOut" size={16} />
        <span className="guest-leave-label">Thoát</span>
      </button>
    </header>
  )
}

/** The guest's own invitation (UC-29): the answer given and whether the question still stands. */
function useMyInvitation(meetingId: string) {
  const [current, setCurrent] = useState<InvitationResponseResult['response'] | null>(null)
  const reload = useCallback(() => {
    getMyInvitationResponse(meetingId).then(setCurrent, () => undefined)
  }, [meetingId])
  useEffect(() => {
    void Promise.resolve().then(reload)
  }, [reload])
  useMeetingEvents(meetingId, reload, ['participantsChanged'])
  return { current, reload }
}
