import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Icon } from '../../../components/Icon'
import { NotFound } from '../../../components/NotFound'
import { useToast } from '../../../components/toastContext'
import { isApiError } from '../../../lib/errors'
import { formatDateTime } from '../../../lib/format'
import { routes } from '../../../lib/routes'
import {
  EDITABLE_STATUSES,
  MEETING_MODE_LABEL,
  SENSITIVITY_LABEL,
  type Meeting,
  type MeetingApprover,
} from '../../../types/meeting'
import { useAccessLost } from '../../auth/useAccessLost'
import { useAuth } from '../../auth/useAuth'
import { StatusBadge } from '../StatusBadge'
import { useMeeting } from '../useMeeting'
import { useMeetingApprovers } from '../useMeetingApprovers'
import { MeetingHistory } from '../workflow/MeetingHistory'
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
const TAB_BLOCKED_ON: Record<Exclude<TabId, 'overview'>, string> = {
  attendees: 'GET /api/meetings/{id}/attendees',
  agenda: 'GET /api/meetings/{id}/agenda',
  documents: 'GET /api/meetings/{id}/documents',
  recordings: 'GET /api/meetings/{id}/recordings',
  minutes: 'GET /api/meetings/{id}/minutes',
}

/** The approval workflow ends at publishing; after that the bar has nothing left to say. */
const REVIEW_STATUSES = ['WaitingApproval', 'Rejected', 'Approved'] as const

/**
 * UC-13 View Meeting Details, reading GET /api/meetings/{id} (SCRUM-17), with the approval
 * workflow and the lifecycle history of SCRUM-19. A meeting the caller has no relationship with
 * answers 404 exactly like an unknown id, and renders the same NotFound.
 */
export function MeetingDetailPage() {
  const { meetingId } = useParams()
  const { state, retry, reload, replace } = useMeeting(meetingId)

  if (state.kind === 'notFound') return <NotFound />

  if (state.kind !== 'loaded') {
    return (
      <>
        <nav className="breadcrumb" aria-label="Đường dẫn">
          <Link to={routes.meetings}>Cuộc họp</Link>
        </nav>
        {state.kind === 'loading' ? (
          <p className="page-loading">Đang tải thông tin cuộc họp…</p>
        ) : (
          <div className="alert alert-error page-alert" role="alert">
            <p>{state.message}</p>
            <button type="button" className="btn btn-outline btn-sm" onClick={retry}>
              Thử lại
            </button>
          </div>
        )}
      </>
    )
  }

  // Keyed by id: another meeting opened from a notification starts with a fresh history.
  return <MeetingDetail key={state.meeting.id} meeting={state.meeting} reload={reload} replace={replace} />
}

function MeetingDetail({
  meeting,
  reload,
  replace,
}: {
  meeting: Meeting
  reload: () => Promise<void>
  replace: (meeting: Meeting) => void
}) {
  const { user, can, reloadSession } = useAuth()
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [tab, setTab] = useState<TabId>('overview')
  const history = useMeetingHistory(meeting.id)

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
  const restricted = meeting.sensitivity === 'Secret' || meeting.sensitivity === 'Confidential'

  /** Display names the page already knows: whoever acted in the history, and the approver list. */
  const names = useMemo(() => {
    const map = new Map<string, string>()
    for (const approver of approvers.list ?? []) map.set(approver.id, approver.fullName)
    for (const item of history.items) map.set(item.actorId, item.actorName)
    return map
  }, [approvers.list, history.items])
  const nameOf = useCallback((id: string) => (id === user?.id ? 'bạn' : (names.get(id) ?? null)), [names, user?.id])

  const { reload: reloadHistory, append: appendHistory } = history
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

  // The history is read separately; it can be the first to learn the meeting is gone.
  const historyFailure = history.failure
  useEffect(() => {
    if (isApiError(historyFailure, 404)) void reload()
    else if (isApiError(historyFailure, 403)) handleAccessLost(historyFailure)
  }, [historyFailure, reload, handleAccessLost])

  return (
    <>
      <nav className="breadcrumb" aria-label="Đường dẫn">
        <Link to={routes.meetings}>Cuộc họp</Link>
        <span aria-hidden="true">›</span>
        <span>{meeting.title}</span>
      </nav>

      <div className="page-header">
        <div>
          <h1>{meeting.title}</h1>
          <p className="page-header-desc">Tạo lúc: {formatDateTime(meeting.createdAt)}</p>
        </div>
        <div className="page-header-actions">
          {restricted && (
            <p className="sensitivity-flag">Cuộc họp {SENSITIVITY_LABEL[meeting.sensitivity].toLowerCase()}</p>
          )}
          {canEdit && (
            <Link className="btn btn-outline" to={routes.meetingEdit(meeting.id)}>
              <Icon name="pencil" size={16} />
              Chỉnh sửa
            </Link>
          )}
        </div>
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

      <div className="tabs" role="tablist">
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
          ) : (
            <div className="card card-pending">
              <p>
                Tab này chưa có nghiệp vụ. Sẽ hiện thực khi backend có <code>{TAB_BLOCKED_ON[tab]}</code>.
              </p>
            </div>
          )}
        </section>

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
        <span>Hình thức</span>
        <strong>{MEETING_MODE_LABEL[meeting.mode]}</strong>
      </div>
      <div className="field-item">
        <span>Mức độ mật</span>
        <strong>{SENSITIVITY_LABEL[meeting.sensitivity]}</strong>
      </div>
      <div className="field-item">
        <span>Thời gian bắt đầu</span>
        <strong>{formatDateTime(meeting.startsAt)}</strong>
      </div>
      <div className="field-item">
        <span>Thời gian kết thúc dự kiến</span>
        {meeting.endsAt ? (
          <strong>{formatDateTime(meeting.endsAt)}</strong>
        ) : (
          <em className="value-empty">Chưa xác định</em>
        )}
      </div>
      <div className="field-item">
        <span>{meeting.mode === 'Hybrid' ? 'Địa điểm họp trực tiếp' : 'Địa điểm'}</span>
        {meeting.mode === 'Online' ? (
          <strong>Phòng gọi trực tuyến</strong>
        ) : meeting.location ? (
          <strong>{meeting.location}</strong>
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
      <div className="field-item span-all">
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
