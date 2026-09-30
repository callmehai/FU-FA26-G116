import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Icon } from '../../../components/Icon'
import { NotFound } from '../../../components/NotFound'
import { formatDateTime } from '../../../lib/format'
import { routes } from '../../../lib/routes'
import { EDITABLE_STATUSES, MEETING_MODE_LABEL, SENSITIVITY_LABEL, type Meeting } from '../../../types/meeting'
import { useAuth } from '../../auth/useAuth'
import { StatusBadge } from '../StatusBadge'
import { useMeeting } from '../useMeeting'
import { useMeetingApprovers } from '../useMeetingApprovers'

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

/**
 * UC-13 View Meeting Details, reading GET /api/meetings/{id} (SCRUM-17). A meeting the caller has
 * no relationship with answers 404 exactly like an unknown id, and renders the same NotFound.
 */
export function MeetingDetailPage() {
  const { meetingId } = useParams()
  const { state, retry } = useMeeting(meetingId)

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

  return <MeetingDetail meeting={state.meeting} />
}

function MeetingDetail({ meeting }: { meeting: Meeting }) {
  const { user, can } = useAuth()
  const [tab, setTab] = useState<TabId>('overview')

  const isOrganizer = meeting.createdById === user?.id && can('MeetingCreator')
  // Hidden, not disabled, when it does not apply: a greyed-out button for something the user can
  // never do here is noise. The backend re-checks ownership and status on save regardless.
  const canEdit = isOrganizer && EDITABLE_STATUSES.includes(meeting.status)
  const restricted = meeting.sensitivity === 'Secret' || meeting.sensitivity === 'Confidential'

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
            <Overview meeting={meeting} showApprover={isOrganizer} />
          ) : (
            <div className="card card-pending">
              <p>
                Tab này chưa có nghiệp vụ. Sẽ hiện thực khi backend có <code>{TAB_BLOCKED_ON[tab]}</code>.
              </p>
            </div>
          )}
        </section>

        <aside className="card">
          <h2 className="card-title">Lịch sử trạng thái</h2>
          <p className="card-pending">
            Chưa hiển thị được: backend chưa có <code>GET /api/meetings/{'{id}'}/history</code>.
          </p>
        </aside>
      </div>
    </>
  )
}

function Overview({ meeting, showApprover }: { meeting: Meeting; showApprover: boolean }) {
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
      {showApprover && <ApproverValue approverId={meeting.assignedApproverId} />}
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

/**
 * The response carries only the approver's id. The organizer may list approvers, so their name is
 * looked up there; nobody else is shown this field.
 */
function ApproverValue({ approverId }: { approverId: string | null }) {
  const approvers = useMeetingApprovers(approverId !== null)
  const name = approvers.list?.find((a) => a.id === approverId)?.fullName

  return (
    <div className="field-item">
      <span>Người duyệt</span>
      {approverId === null ? (
        <em className="value-empty">Chưa chọn</em>
      ) : name ? (
        <strong>{name}</strong>
      ) : approvers.list ? (
        <em className="value-empty">Không còn quyền duyệt</em>
      ) : approvers.error ? (
        <em className="value-empty">Không tải được tên</em>
      ) : (
        <em className="value-empty">Đang tải…</em>
      )}
    </div>
  )
}
