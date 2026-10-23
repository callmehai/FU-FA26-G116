import { Icon } from '../../components/Icon'
import { Select, type SelectOption } from '../../components/Select'
import { formatDateTime } from '../../lib/format'
import {
  ATTENDANCE_LABEL,
  LEGACY_ATTENDANCE_LABEL,
  type AttendancePage,
  type AttendanceRow,
  type AttendanceStatus,
} from '../../types/attendance'

/** Green as the meeting's "Đang diễn ra"; absent is grey — red stays for warnings and "Mật". */
const DOT: Record<AttendanceStatus, string> = { Present: 'var(--live)', Absent: 'var(--muted-ink)' }

const OPTIONS: SelectOption<AttendanceStatus>[] = (['Present', 'Absent'] as const).map((value) => ({
  value,
  label: ATTENDANCE_LABEL[value],
  dot: DOT[value],
}))

/** "Nguyễn Thư Ký điểm danh lúc 09:05 23/10/2026", as AI-eCabinet words it; null when never recorded. */
function recordedNote(row: AttendanceRow) {
  const parts = []
  if (row.legacyAttendance) parts.push(`Giá trị cũ: ${LEGACY_ATTENDANCE_LABEL[row.legacyAttendance]}`)
  if (row.recordedAt)
    parts.push(`${row.recordedBy?.displayName ?? 'Thư ký'} điểm danh lúc ${formatDateTime(row.recordedAt)}`)
  return parts.length > 0 ? parts.join('\n') : null
}

/**
 * One person's attendance, as AI-eCabinet shows it: the Secretary gets a small picker, dashed and
 * reading "Điểm danh" until something is picked — picking is the save. Everyone else reads a dot
 * and a word, or "Chưa điểm danh", which is not the same as absent. Who recorded it and when sits
 * in the tooltip.
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
  const note = recordedNote(row)

  if (!canRecord) return <AttendanceBadge row={row} note={note} />

  return (
    <span className="attendance-picker" data-tooltip={note ?? undefined} aria-busy={pending !== undefined}>
      <Select
        size="sm"
        value={pending ?? row.attendance}
        options={OPTIONS}
        placeholder="Điểm danh"
        label={`Điểm danh ${row.displayName}`}
        disabled={pending !== undefined}
        onChange={(value) => value !== row.attendance && onRecord(row, value)}
      />
    </span>
  )
}

function AttendanceBadge({ row, note }: { row: AttendanceRow; note: string | null }) {
  const tooltip = note ? { tabIndex: 0, 'data-tooltip': note } : {}
  if (row.attendance) {
    return (
      <span className="attendance-badge" {...tooltip}>
        <span className="select-dot" style={{ background: DOT[row.attendance] }} aria-hidden="true" />
        {ATTENDANCE_LABEL[row.attendance]}
      </span>
    )
  }
  if (row.legacyAttendance) {
    return (
      <span className="attendance-badge" {...tooltip}>
        {LEGACY_ATTENDANCE_LABEL[row.legacyAttendance]}
      </span>
    )
  }
  return <span className="attendance-unmarked">Chưa điểm danh</span>
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
