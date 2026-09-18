import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { NotFound } from '../../../components/NotFound'
import { formatDateTime } from '../../../lib/format'
import { routes } from '../../../lib/routes'
import { MEETING_MODE_LABEL, SENSITIVITY_LABEL } from '../../../types/meeting'
import { SAMPLE_HISTORY, SAMPLE_MEETINGS } from '../sampleData'
import { StatusBadge } from '../StatusBadge'

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

export function MeetingDetailPage() {
  const { meetingId } = useParams()
  const [tab, setTab] = useState<TabId>('overview')

  const meeting = SAMPLE_MEETINGS.find((m) => m.id === meetingId)

  // An unknown id renders the same 404 as an unknown route. Once the backend answers, a meeting the
  // caller may not see must also 404 rather than 403, so its existence stays hidden.
  if (!meeting) return <NotFound />

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
        {restricted && (
          <p className="sensitivity-flag">Cuộc họp {SENSITIVITY_LABEL[meeting.sensitivity].toLowerCase()}</p>
        )}
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
                <span>Mức độ</span>
                <strong>{SENSITIVITY_LABEL[meeting.sensitivity]}</strong>
              </div>
              <div className="field-item">
                <span>Thời gian bắt đầu</span>
                <strong>{formatDateTime(meeting.startsAt)}</strong>
              </div>
              <div className="field-item">
                <span>Thời gian kết thúc</span>
                <strong>{formatDateTime(meeting.endsAt)}</strong>
              </div>
              <div className="field-item">
                <span>Địa điểm</span>
                <strong>{meeting.location}</strong>
              </div>
            </div>
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
          <ol className="timeline">
            {SAMPLE_HISTORY.map((entry) => (
              <li key={entry.at}>
                <div className="timeline-head">
                  <StatusBadge status={entry.status} label={entry.label} />
                  <span className="timeline-actor">{entry.actor}</span>
                </div>
                <time dateTime={entry.at}>{formatDateTime(entry.at)}</time>
              </li>
            ))}
          </ol>
          <p className="mock-note">
            Mẫu cố định — sẽ thay bằng <code>GET /api/meetings/{'{id}'}/history</code>.
          </p>
        </aside>
      </div>
    </>
  )
}
