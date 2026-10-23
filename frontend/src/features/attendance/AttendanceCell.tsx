import { useId } from 'react'
import { Icon } from '../../components/Icon'
import { formatDateTime } from '../../lib/format'
import {
  ATTENDANCE_LABEL,
  LEGACY_ATTENDANCE_LABEL,
  type AttendancePage,
  type AttendanceRow,
  type AttendanceStatus,
} from '../../types/attendance'

const CHOICES: { value: AttendanceStatus; icon: 'check' | 'x' }[] = [
  { value: 'Present', icon: 'check' },
  { value: 'Absent', icon: 'x' },
]

/** "Nguyễn Thư Ký điểm danh lúc 09:05 23/10/2026", as AI-eCabinet words it; null when never recorded. */
function recordedNote(row: AttendanceRow) {
  const parts = []
  if (row.legacyAttendance) parts.push(`Giá trị cũ: ${LEGACY_ATTENDANCE_LABEL[row.legacyAttendance]}`)
  if (row.recordedAt)
    parts.push(`${row.recordedBy?.displayName ?? 'Thư ký'} điểm danh lúc ${formatDateTime(row.recordedAt)}`)
  return parts.length > 0 ? parts.join('\n') : null
}

/**
 * One person's attendance. The Secretary gets two buttons, "Có mặt" and "Vắng": a click saves it
 * at once. Everyone else reads a chip, or "Chưa điểm danh" — which is not the same as absent. Who
 * recorded it and when sits in the tooltip.
 */
export function AttendanceCell({
  row,
  canRecord,
  pending,
  onRecord,
}: {
  row: AttendanceRow
  canRecord: boolean
  /** The value being saved for this row, shown while the request runs. */
  pending?: AttendanceStatus
  onRecord: (row: AttendanceRow, value: AttendanceStatus) => void
}) {
  const name = useId()
  const note = recordedNote(row)

  if (!canRecord) return <AttendanceBadge row={row} note={note} />

  const value = pending ?? row.attendance
  return (
    <div
      className="attendance-toggle"
      role="radiogroup"
      aria-label={`Điểm danh ${row.displayName}`}
      aria-busy={pending !== undefined}
      data-tooltip={note ?? undefined}
    >
      {CHOICES.map((choice) => (
        <label key={choice.value} className={`attendance-option attendance-${choice.value.toLowerCase()}`}>
          <input
            type="radio"
            name={name}
            value={choice.value}
            checked={value === choice.value}
            disabled={pending !== undefined}
            onChange={() => onRecord(row, choice.value)}
          />
          <span>
            <Icon name={choice.icon} size={14} />
            <span className="attendance-label" data-text={ATTENDANCE_LABEL[choice.value]}>
              {ATTENDANCE_LABEL[choice.value]}
            </span>
          </span>
        </label>
      ))}
    </div>
  )
}

function AttendanceBadge({ row, note }: { row: AttendanceRow; note: string | null }) {
  const tooltip = note ? { tabIndex: 0, 'data-tooltip': note } : {}
  if (row.attendance) {
    const present = row.attendance === 'Present'
    return (
      <span className={`badge attendance-badge ${present ? 'badge-emerald' : 'badge-slate'}`} {...tooltip}>
        <Icon name={present ? 'check' : 'x'} size={14} />
        {ATTENDANCE_LABEL[row.attendance]}
      </span>
    )
  }
  if (row.legacyAttendance) {
    return (
      <span className="badge badge-outline" {...tooltip}>
        {LEGACY_ATTENDANCE_LABEL[row.legacyAttendance]}
      </span>
    )
  }
  return <em className="value-empty attendance-unmarked">Chưa điểm danh</em>
}

/** "Có mặt 3/8", with the full split in the tooltip; counts are over the whole roster. */
export function AttendanceSummary({ totals }: { totals: AttendancePage['totals'] }) {
  const split = [`Có mặt ${totals.present}`, `Vắng ${totals.absent}`, `Chưa điểm danh ${totals.unmarked}`]
  if (totals.legacy > 0) split.push(`Giá trị cũ ${totals.legacy}`)
  return (
    <span className="badge badge-emerald attendance-summary" tabIndex={0} data-tooltip={split.join(' · ')}>
      <Icon name="userCheck" size={14} />
      Có mặt {totals.present}/{totals.total}
      <span className="visually-hidden">. {split.join(', ')}</span>
    </span>
  )
}
