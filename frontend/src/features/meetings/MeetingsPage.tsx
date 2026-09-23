import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Badge } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { Select } from '../../components/Select'
import { formatDateTime } from '../../lib/format'
import { routes } from '../../lib/routes'
import {
  MEETING_STATUSES,
  MEETING_STATUS_LABEL,
  SENSITIVITIES,
  SENSITIVITY_LABEL,
  type MeetingStatus,
  type Sensitivity,
} from '../../types/meeting'
import { useAuth } from '../auth/useAuth'
import { SAMPLE_MEETINGS } from './sampleData'
import { StatusBadge } from './StatusBadge'

/** A meeting's sensitivity is only called out once it rises above routine. */
const SENSITIVITY_TONE = { Normal: 'slate', Internal: 'slate', Confidential: 'amber', Secret: 'lacquer' } as const

// An empty value means "no filter", which is why these lists lead with an all-pass entry.
const STATUS_OPTIONS = [
  { value: '' as const, label: 'Mọi trạng thái' },
  ...MEETING_STATUSES.map((s) => ({ value: s, label: MEETING_STATUS_LABEL[s] })),
]

const SENSITIVITY_OPTIONS = [
  { value: '' as const, label: 'Mọi mức độ' },
  ...SENSITIVITIES.map((s) => ({ value: s, label: SENSITIVITY_LABEL[s] })),
]

export function MeetingsPage() {
  const { can } = useAuth()
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState<MeetingStatus | ''>('')
  const [sensitivity, setSensitivity] = useState<Sensitivity | ''>('')

  // Filtering happens client-side against the sample rows. Once the backend serves the list, these
  // three controls become query parameters instead — the server must filter, not the browser,
  // because a row the caller may not see should never reach it in the first place.
  const rows = useMemo(() => {
    const needle = query.trim().toLowerCase()
    return SAMPLE_MEETINGS.filter(
      (m) =>
        (!needle || m.title.toLowerCase().includes(needle)) &&
        (!status || m.status === status) &&
        (!sensitivity || m.sensitivity === sensitivity),
    )
  }, [query, status, sensitivity])

  return (
    <>
      <PageHeader
        title="Cuộc họp"
        description="Danh sách các cuộc họp bạn có quyền xem."
        action={
          can('MeetingCreator') && (
            <Link className="btn btn-primary" to={routes.meetingNew}>
              <Icon name="plus" size={16} />
              Tạo cuộc họp
            </Link>
          )
        }
      />

      <div className="toolbar">
        <div className="search">
          <Icon name="search" size={16} />
          <input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Tìm theo tên cuộc họp…"
            aria-label="Tìm cuộc họp"
          />
        </div>

        <Select value={status} options={STATUS_OPTIONS} onChange={setStatus} label="Trạng thái" />
        <Select value={sensitivity} options={SENSITIVITY_OPTIONS} onChange={setSensitivity} label="Mức độ" />
      </div>

      <div className="table-card table-fill">
        <table className="data-table">
          <thead>
            <tr>
              <th>Tên cuộc họp</th>
              <th>Thời gian diễn ra</th>
              <th>Tạo lúc</th>
              <th>Trạng thái</th>
              <th>Mức độ</th>
              <th className="col-action">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((m) => (
              <tr key={m.id}>
                <td className="cell-title">{m.title}</td>
                <td className="cell-muted">{formatDateTime(m.startsAt)}</td>
                <td className="cell-muted">{formatDateTime(m.createdAt)}</td>
                <td>
                  <StatusBadge status={m.status} />
                </td>
                <td>
                  <Badge tone={SENSITIVITY_TONE[m.sensitivity]}>{SENSITIVITY_LABEL[m.sensitivity]}</Badge>
                </td>
                <td className="col-action">
                  <Link className="icon-button" to={routes.meeting(m.id)} title="Xem chi tiết">
                    <Icon name="eye" />
                  </Link>
                </td>
              </tr>
            ))}

            {rows.length === 0 && (
              <tr>
                <td colSpan={6} className="cell-empty">
                  Không có cuộc họp nào khớp bộ lọc.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <p className="mock-note">
        Dữ liệu trên là mẫu cố định trong <code>features/meetings/sampleData.ts</code>, chưa nối
        backend. Sẽ thay bằng <code>GET /api/meetings</code>.
      </p>
    </>
  )
}
