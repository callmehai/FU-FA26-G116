import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom'
import { getMeetingCall } from '../../../api/calls'
import { BackLink } from '../../../components/BackLink'
import { Icon } from '../../../components/Icon'
import { NotFound } from '../../../components/NotFound'
import { useToast } from '../../../components/toastContext'
import { isApiError } from '../../../lib/errors'
import { formatDateTime, formatSchedule } from '../../../lib/format'
import { useBackTarget, type BackTarget } from '../../../lib/backTarget'
import { routes } from '../../../lib/routes'
import {
  EDITABLE_STATUSES,
  MEETING_MODE_LABEL,
  RESCHEDULABLE_STATUSES,
  type Meeting,
  type MeetingApprover,
} from '../../../types/meeting'
import { placeLabel } from '../place'
import { useAccessLost } from '../../auth/useAccessLost'
import { useAuth } from '../../auth/useAuth'
import { useNotifications } from '../../notifications/notificationsContext'
import { ParticipantsPanel } from '../../participants/ParticipantsPanel'
import { RsvpButtons, RsvpChip } from '../../participants/Rsvp'
import { SensitivityBadge, StatusBadge } from '../StatusBadge'
import { useMeeting } from '../useMeeting'
import { EditMeetingDialog } from '../MeetingDialogs'
import { EDIT_PARAM, useEditMeetingLink } from '../meetingDialogLink'
import { useMeetingEvents } from '../useMeetingEvents'
import { useMeetingApprovers } from '../useMeetingApprovers'
import { MeetingHistory } from '../workflow/MeetingHistory'
import { lifecycleAction } from '../workflow/lifecycle'
import { MeetingLifecycleActions } from '../workflow/MeetingLifecycleActions'
import { MeetingWorkflowBar, type WorkflowRole } from '../workflow/MeetingWorkflowBar'
import { useMeetingHistory } from '../workflow/useMeetingHistory'
import type { WorkflowOutcome } from '../workflow/workflowOutcome'

/** The detail screen is split by lifecycle concern rather than by data table. */
const TABS = [
  { id: 'overview', label: 'Thông tin chung' },
  { id: 'attendees', label: 'Thành phần tham dự' },
  { id: 'agenda', label: 'Nội dung cuộc họp' },
  { id: 'documents', label: 'Tài liệu' },
  { id: 'recordings', label: 'Bản ghi' },
  { id: 'minutes', label: 'Biên bản' },
] as const

type TabId = (typeof TABS)[number]['id']

/** What each tab is waiting on, surfaced in the UI so the backlog is visible while building. */
const TAB_BLOCKED_ON: Record<Exclude<TabId, 'overview' | 'attendees'>, string> = {
  agenda: 'GET /api/meetings/{id}/agenda',
  documents: 'GET /api/meetings/{id}/documents',
  recordings: 'GET /api/meetings/{id}/recordings',
  minutes: 'GET /api/meetings/{id}/minutes',
}

/** The hub event that tells an open page the meeting was ended (SCRUM-69). */
const MEETING_ENDED = ['meetingEnded'] as const

/** The approval workflow ends at publishing; after that the bar has nothing left to say. */
const REVIEW_STATUSES = ['WaitingApproval', 'Rejected', 'Approved'] as const

/**
 * UC-16 View Meeting Details, reading GET /api/meetings/{id} (SCRUM-17), with the approval
 * workflow and the lifecycle history of SCRUM-19. A meeting the caller has no relationship with
 * answers 404 exactly like an unknown id, and renders the same NotFound.
 */
export function MeetingDetailPage() {
  const { meetingId } = useParams()
  const { state, retry, reload, replace } = useMeeting(meetingId)
  // Back to wherever the meeting was opened from (the calendar on its week and view, the list with
  // its filters); the plain list when it was opened from a notification or a typed URL.
  const back = useBackTarget({ to: routes.meetings, label: 'Cuộc họp' })

  if (state.kind === 'notFound') return <NotFound />

  if (state.kind !== 'loaded') {
    return (
      <>
        <BackLink to={back.to}>{back.label}</BackLink>
        {state.kind === 'loading' ? (
          <DetailSkeleton />
        ) : (
          <div className="alert alert-error page-alert" role="alert">
            <p>{state.message}</p>
            <button type="button" className="btn btn-outline btn-sm" onClick={retry}>
              <Icon name="refresh" size={16} />
              Thử lại
            </button>
          </div>
        )}
      </>
    )
  }

  // Keyed by id: another meeting opened from a notification starts with a fresh history.
  return <MeetingDetail key={state.meeting.id} meeting={state.meeting} back={back} reload={reload} replace={replace} />
}

function MeetingDetail({
  meeting,
  back,
  reload,
  replace,
}: {
  meeting: Meeting
  back: BackTarget
  reload: () => Promise<void>
  replace: (meeting: Meeting) => void
}) {
  const { user, can, reloadSession } = useAuth()
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [tab, setTab] = useState<TabId>('overview')
  const history = useMeetingHistory(meeting.id)
  // UC-15 opens as a dialog on this page (`?edit=1`), keeping the back link's target.
  const editLink = useEditMeetingLink()
  const [params] = useSearchParams()

  const isOrganizer = meeting.createdById === user?.id && can('MeetingCreator')
  const isApprover =
    meeting.assignedApproverId === user?.id && meeting.createdById !== user?.id && can('MeetingApprover')
  const role: WorkflowRole | null =
    isOrganizer && (meeting.status === 'Draft' || REVIEW_STATUSES.some((s) => s === meeting.status))
      ? 'organizer'
      : isApprover && REVIEW_STATUSES.some((s) => s === meeting.status)
        ? 'approver'
        : null
  // Only the organizer may list approvers; they need it, while the meeting is under review, to name
  // the approver and to see when the one they picked can no longer decide.
  const approvers = useMeetingApprovers(role === 'organizer')

  // Hidden, not disabled, when it does not apply: a greyed-out button for something the user can
  // never do here is noise. The backend re-checks ownership and status on save regardless.
  const canEdit = isOrganizer && EDITABLE_STATUSES.includes(meeting.status)
  // UC-15 AF-1: after publishing, the organizer may still move the time, place and link.
  const canReschedule = isOrganizer && RESCHEDULABLE_STATUSES.includes(meeting.status)
  const roomOpen = useCallRoomOpen(meeting)
  // SCRUM-69: the Chair starts a published meeting and ends a running one.
  const canRun = lifecycleAction(meeting) !== null

  /** Display names the page already knows: whoever acted in the history, and the approver list. */
  const names = useMemo(() => {
    const map = new Map<string, string>()
    for (const approver of approvers.list ?? []) map.set(approver.id, approver.fullName)
    for (const item of history.items) map.set(item.actorId, item.actorName)
    return map
  }, [approvers.list, history.items])
  const nameOf = useCallback((id: string) => (id === user?.id ? 'bạn' : (names.get(id) ?? null)), [names, user?.id])

  // A roster change can move the Chair, the caller's role or whether they are asked to answer.
  useMeetingEvents(meeting.id, () => void reload())

  const { reload: reloadHistory, append: appendHistory } = history

  // Someone else ending the meeting arrives on the hub; starting it publishes nothing there, so the
  // notification it sends to everyone but the Chair is the signal. Either way, re-read the page.
  const reloadAll = useCallback(() => {
    void reload()
    void reloadHistory()
  }, [reload, reloadHistory])
  useMeetingEvents(meeting.id, reloadAll, MEETING_ENDED)
  const { lastArrival } = useNotifications()
  const seenArrival = useRef(lastArrival)
  useEffect(() => {
    if (!lastArrival || lastArrival === seenArrival.current) return
    seenArrival.current = lastArrival
    if (lastArrival.meetingId?.toLowerCase() === meeting.id.toLowerCase()) reloadAll()
  }, [lastArrival, meeting.id, reloadAll])
  const onOutcome = useCallback(
    (outcome: WorkflowOutcome) => {
      switch (outcome.kind) {
        case 'done':
          toast.show(outcome.response.message)
          replace(outcome.response.meeting)
          appendHistory(outcome.response.history)
          return
        case 'conflict':
        case 'forbidden':
          // The action no longer applies: say why, then show what is true now.
          toast.show(outcome.message, 'error')
          if (outcome.kind === 'forbidden') void reloadSession()
          void reload()
          void reloadHistory()
          return
        case 'gone':
          void reload()
          return
        case 'accessLost':
          handleAccessLost(outcome.error)
      }
    },
    [toast, replace, appendHistory, reloadSession, reload, reloadHistory, handleAccessLost],
  )

  // Opening a notification about this same meeting navigates to the URL already on screen, which
  // does not remount the page: re-read what the notification says has changed. A different meeting
  // in the URL remounts this component anyway, so leave that one to the new mount.
  const { key: locationKey } = useLocation()
  const { meetingId } = useParams()
  const shownKey = useRef(locationKey)
  useEffect(() => {
    if (shownKey.current === locationKey || meetingId?.toLowerCase() !== meeting.id.toLowerCase()) return
    shownKey.current = locationKey
    void reload()
    void reloadHistory()
  }, [locationKey, meetingId, meeting.id, reload, reloadHistory])

  // The history is read separately; it can be the first to learn the meeting is gone.
  const historyFailure = history.failure
  useEffect(() => {
    if (isApiError(historyFailure, 404)) void reload()
    else if (isApiError(historyFailure, 403)) handleAccessLost(historyFailure)
  }, [historyFailure, reload, handleAccessLost])

  return (
    <>
      <BackLink to={back.to}>{back.label}</BackLink>

      <div className="record-head">
        <div className="record-head-main">
          <div className="record-title">
            <h1>{meeting.title}</h1>
            <StatusBadge status={meeting.status} />
            {meeting.sensitivity !== 'Normal' && <SensitivityBadge sensitivity={meeting.sensitivity} />}
            <RsvpChip response={meeting.myResponse} />
          </div>
          <p className="record-facts">
            <span>
              <Icon name="clock" size={16} />
              {formatSchedule(meeting.plannedStartTime, meeting.plannedEndTime)}
            </span>
            <span>
              <Icon name={meeting.mode === 'InPerson' ? 'mapPin' : 'video'} size={16} />
              {placeLabel(meeting)}
            </span>
          </p>
        </div>
        {(canEdit || canReschedule || roomOpen || canRun) && (
          <div className="page-header-actions">
            {roomOpen && (
              // The meeting is running: green like its "Đang diễn ra" state, so it stands apart from blue.
              <Link className="btn btn-live btn-lg btn-cta" to={routes.meetingRoom(meeting.id)}>
                <Icon name="video" size={18} />
                Vào phòng họp
              </Link>
            )}
            {canRun && <MeetingLifecycleActions meeting={meeting} onOutcome={onOutcome} />}
            {(canEdit || canReschedule) && (
              <Link className="btn btn-outline btn-lg" to={editLink.to} state={editLink.state}>
                <Icon name={canEdit ? 'pencil' : 'calendar'} size={16} />
                {canEdit ? 'Chỉnh sửa' : 'Dời lịch'}
              </Link>
            )}
          </div>
        )}
      </div>

      {role && (
        <MeetingWorkflowBar
          meeting={meeting}
          role={role}
          history={history.items}
          approvers={role === 'organizer' ? approvers : null}
          nameOf={nameOf}
          onOutcome={onOutcome}
        />
      )}

      {meeting.canRespond && (
        <section className="workflow-bar workflow-blue workflow-bar-compact" aria-labelledby="rsvp-title">
          <span className="workflow-icon" aria-hidden="true">
            <Icon name="calendar" size={18} />
          </span>
          <div className="workflow-body">
            <h2 id="rsvp-title">Bạn có tham dự cuộc họp này?</h2>
          </div>
          <div className="workflow-actions">
            <RsvpButtons meetingId={meeting.id} labelledBy="rsvp-title" onAnswered={() => void reload()} />
          </div>
        </section>
      )}

      <div className="tabs" role="tablist" aria-label="Chi tiết cuộc họp">
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

      <div className="detail-grid">
        <section>
          {tab === 'overview' ? (
            <Overview
              meeting={meeting}
              approver={isOrganizer || isApprover ? approverLabel(meeting, user?.id, nameOf, approvers) : null}
            />
          ) : tab === 'attendees' ? (
            <ParticipantsPanel meetingId={meeting.id} onChanged={() => void reload()} />
          ) : (
            <div className="card card-pending tab-pending">
              <p>Tab này chưa có nghiệp vụ.</p>
              <p>
                Sẽ hiện thực khi backend có <code>{TAB_BLOCKED_ON[tab]}</code>.
              </p>
            </div>
          )}
        </section>

        {params.has(EDIT_PARAM) && (canEdit || canReschedule) && (
          <EditMeetingDialog
            meeting={meeting}
            onSaved={replace}
            onStale={() => {
              void reload()
              void reloadHistory()
            }}
          />
        )}

        <MeetingHistory
          history={history}
          onRetry={() => void history.reload()}
          onLoadMore={() => void history.loadMore()}
          nameOf={nameOf}
        />
      </div>
    </>
  )
}

/**
 * The "Người duyệt" value, for the organizer and the approver only. A draft has none yet: it is
 * picked when the meeting is sent for approval.
 */
function approverLabel(
  meeting: Meeting,
  userId: string | undefined,
  nameOf: (id: string) => string | null,
  approvers: { list: MeetingApprover[] | null; error: string | null },
): { text: string; muted: boolean } {
  const id = meeting.assignedApproverId
  if (!id) return { text: meeting.status === 'Draft' ? 'Chọn khi gửi duyệt' : 'Chưa chọn', muted: true }
  if (id === userId) return { text: 'Bạn', muted: false }
  const name = nameOf(id)
  // Missing from the organizer's list of eligible approvers: deactivated or no longer an approver.
  const lost = approvers.list !== null && !approvers.list.some((a) => a.id === id)
  if (name) return { text: lost ? `${name} (không còn quyền duyệt)` : name, muted: false }
  if (approvers.list) return { text: 'Không còn quyền duyệt', muted: true }
  return { text: approvers.error ? 'Không tải được tên' : 'Đang tải…', muted: true }
}

function Overview({ meeting, approver }: { meeting: Meeting; approver: { text: string; muted: boolean } | null }) {
  return (
    <div className="card field-grid">
      <div className="field-item">
        <span>Trạng thái</span>
        <StatusBadge status={meeting.status} />
      </div>
      <div className="field-item">
        <span>Mức độ</span>
        <SensitivityBadge sensitivity={meeting.sensitivity} />
      </div>
      <div className="field-item span-all">
        <span>Thời gian</span>
        <strong>{formatSchedule(meeting.plannedStartTime, meeting.plannedEndTime)}</strong>
      </div>
      {meeting.actualStartedAt && (
        <div className="field-item span-all">
          <span>Thời gian thực tế</span>
          <strong>
            {meeting.actualEndedAt
              ? formatSchedule(meeting.actualStartedAt, meeting.actualEndedAt)
              : `Bắt đầu ${formatDateTime(meeting.actualStartedAt)}`}
          </strong>
        </div>
      )}
      <div className="field-item">
        <span>Hình thức</span>
        <strong>{MEETING_MODE_LABEL[meeting.mode]}</strong>
      </div>
      {meeting.mode !== 'Online' && (
        <div className="field-item">
          <span>{meeting.mode === 'Hybrid' ? 'Địa điểm họp trực tiếp' : 'Địa điểm'}</span>
          {meeting.location ? <strong>{meeting.location}</strong> : <em className="value-empty">Chưa có</em>}
        </div>
      )}
      {meeting.mode !== 'InPerson' && (
        <div className="field-item">
          <span>Link trực tuyến</span>
          {meeting.onlineLink ? (
            <strong className="value-text">{meeting.onlineLink}</strong>
          ) : (
            <strong>Phòng họp trực tuyến TrustCabinet</strong>
          )}
        </div>
      )}
      <div className="field-item field-row-start">
        <span>Chủ tọa</span>
        {meeting.chair ? <strong>{meeting.chair.fullName}</strong> : <em className="value-empty">Chưa có</em>}
      </div>
      <div className="field-item">
        <span>Thư ký</span>
        {meeting.secretaries.length > 0 ? (
          <strong>{meeting.secretaries.map((s) => s.fullName).join(', ')}</strong>
        ) : (
          <em className="value-empty">Chưa có</em>
        )}
      </div>
      {approver && (
        <div className="field-item">
          <span>Người duyệt</span>
          {approver.muted ? <em className="value-empty">{approver.text}</em> : <strong>{approver.text}</strong>}
        </div>
      )}
      <div className="field-item">
        <span>Tạo lúc</span>
        <strong>{formatDateTime(meeting.createdAt)}</strong>
      </div>
      <div className="field-item span-all field-divided">
        <span>Mô tả</span>
        {meeting.description ? (
          <p className="value-text">{meeting.description}</p>
        ) : (
          <em className="value-empty">Chưa có mô tả</em>
        )}
      </div>
    </div>
  )
}

/** Shaped like the loaded page — header, tabs, the overview card — so nothing jumps when it arrives. */
function DetailSkeleton() {
  return (
    <div className="detail-skeleton" aria-busy="true" aria-label="Đang tải thông tin cuộc họp">
      <div className="record-head">
        <div className="record-head-main">
          <span className="skeleton detail-skeleton-title" />
          <span className="skeleton detail-skeleton-facts" />
        </div>
      </div>
      <div className="tabs" aria-hidden="true">
        {[96, 72, 112, 72].map((w, i) => (
          <span key={i} className="detail-skeleton-tab">
            <span className="skeleton" style={{ width: w }} />
          </span>
        ))}
      </div>
      <div className="detail-grid">
        <div className="card field-grid">
          {Array.from({ length: 6 }, (_, i) => (
            <div key={i} className="field-item detail-skeleton-field">
              <span className="skeleton" />
              <span className="skeleton" />
            </div>
          ))}
        </div>
        <div className="card detail-skeleton-aside">
          <span className="skeleton" />
          <span className="skeleton" />
          <span className="skeleton" />
        </div>
      </div>
    </div>
  )
}

/**
 * Whether to offer "join the call room" (SCRUM-41). Only an online or hybrid meeting in progress
 * has a room, so nothing is asked otherwise; and the backend answers 404 to someone who is not
 * invited (e.g. the organizer who is not a participant), which simply hides the button.
 */
function useCallRoomOpen(meeting: Meeting) {
  const [open, setOpen] = useState(false)
  const eligible = meeting.mode !== 'InPerson' && meeting.status === 'InProgress'

  useEffect(() => {
    if (!eligible) return
    let cancelled = false
    getMeetingCall(meeting.id)
      .then((call) => !cancelled && setOpen(call.status === 'Open'))
      .catch(() => !cancelled && setOpen(false))
    return () => {
      cancelled = true
    }
  }, [eligible, meeting.id])

  return eligible && open
}
