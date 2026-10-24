import { useMemo } from 'react'
import { Icon } from '../../components/Icon'
import { avatarColors, initials } from '../../lib/avatar'
import type { AttendanceRow } from '../../types/attendance'
import { MEETING_ROLE_LABEL, type MeetingRole } from '../../types/participant'
import { AttendanceCell } from './AttendanceCell'
import { useAttendance } from './useAttendance'

const ROLE_ORDER: Record<MeetingRole, number> = { Chair: 0, Secretary: 1, Member: 2 }

/**
 * Attendance in the call room's side panel (SCRUM-26, ADR-0011), so the Secretary records it without
 * leaving the call. Same data, same cell and same rules as the attendees tab: it reads the whole
 * roster itself and re-reads on the hub's events. Joining the room never marks anyone present.
 */
export function AttendancePanel({ meetingId }: { meetingId: string }) {
  const { state, pending, record, reload } = useAttendance(meetingId)
  const data = state.kind === 'loaded' || state.kind === 'error' ? state.data : null
  // As the room's "Người dự" panel orders people: Chair, Secretaries, Members, then guests.
  const rows = useMemo(
    () =>
      [...(data?.rows ?? [])].sort(
        (a, b) =>
          (a.role ? ROLE_ORDER[a.role] : 3) - (b.role ? ROLE_ORDER[b.role] : 3) ||
          a.displayName.localeCompare(b.displayName, 'vi'),
      ),
    [data?.rows],
  )

  return (
    <div className="room-panel-body attendance-panel">
      <h2 className="room-panel-title">Điểm danh</h2>

      {state.kind === 'gone' && <p className="cell-sub">Bạn không còn xem được điểm danh của cuộc họp này.</p>}
      {state.kind === 'error' && (
        <div className="alert alert-error" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void reload()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}
      {state.kind === 'loading' && (
        <ul className="attendance-list" aria-busy="true" aria-label="Đang tải điểm danh">
          {Array.from({ length: 4 }, (_, i) => (
            <li key={i} className="attendance-row">
              <span className="skeleton attendance-skeleton" />
            </li>
          ))}
        </ul>
      )}
      {data && (
        <ul className="attendance-list">
          {rows.map((row) => (
            <li key={row.id} className="attendance-row">
              <PersonLine row={row} />
              <AttendanceCell
                row={row}
                canRecord={data.permissions.canRecordAttendance}
                pending={pending.get(row.id)}
                onRecord={record}
              />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function PersonLine({ row }: { row: AttendanceRow }) {
  const colors = avatarColors(row.displayName)
  return (
    <>
      <span className="avatar avatar-sm" style={{ backgroundColor: colors.bg, color: colors.fg }} aria-hidden="true">
        {initials(row.displayName)}
      </span>
      <span className="room-person-name attendance-name">
        <strong>{row.displayName}</strong>
        <small>{row.role ? MEETING_ROLE_LABEL[row.role] : 'Khách'}</small>
      </span>
    </>
  )
}
