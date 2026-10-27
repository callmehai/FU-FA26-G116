import { Icon, type IconName } from '../../components/Icon'
import { MEETING_STATUS_LABEL, SENSITIVITY_LABEL, type MeetingStatus, type Sensitivity } from '../../types/meeting'
import { STATUS_TONE } from './statusTone'

/**
 * One glyph per state, so states differ by shape and not only by colour (ADR-0028). "Đang diễn ra"
 * has none: its solid fill is its shape, and an icon on a filled label is one decoration too many.
 */
const STATUS_ICON: Partial<Record<MeetingStatus, IconName>> = {
  Draft: 'pencil',
  WaitingApproval: 'clock',
  Rejected: 'xCircle',
  Approved: 'checkCircle',
  Published: 'send',
  Ended: 'checkCheck',
  Cancelled: 'calendarX',
}

/**
 * A meeting's lifecycle state as a coloured icon and text, with no chip around it; "Đang diễn ra" is
 * the one solid label, so a live meeting is the first thing found on any screen (ADR-0028).
 *
 * `label` overrides the text while keeping the colour. The history timeline needs that: a row
 * there records what *happened* ("Đã gửi duyệt"), not what the meeting *is* ("Chờ duyệt"), and
 * passes `plain`: its own dot already sits on the timeline, so the row shows the text alone.
 */
export function StatusBadge({
  status,
  label,
  plain = false,
}: {
  status: MeetingStatus
  label?: string
  plain?: boolean
}) {
  return (
    <span className={`status status-${STATUS_TONE[status]}${plain ? ' status-plain' : ''}`}>
      {!plain && STATUS_ICON[status] && <Icon name={STATUS_ICON[status]} size={14} />}
      {label ?? MEETING_STATUS_LABEL[status]}
    </span>
  )
}

const LEVEL: Record<Sensitivity, { className: string; icon: IconName }> = {
  Normal: { className: 'level-normal', icon: 'globe' },
  Internal: { className: 'level-internal', icon: 'building' },
  Confidential: { className: 'level-confidential', icon: 'shieldAlert' },
  Secret: { className: 'level-secret', icon: 'lock' },
}

/**
 * A meeting's sensitivity as a square-cornered chip with its own icon, so the levels differ by
 * shape as well as colour. Red is reserved for "Mật". "Thường" is the usual case and stays plain
 * text: a chip on every row would make nothing stand out. A row of chips (a record's header, a
 * calendar card) leaves "Thường" out altogether, as the call room does (ADR-0028).
 */
export function SensitivityBadge({ sensitivity }: { sensitivity: Sensitivity }) {
  if (sensitivity === 'Normal') return <span className="level level-plain">{SENSITIVITY_LABEL[sensitivity]}</span>
  const level = LEVEL[sensitivity]
  return (
    <span className={`level ${level.className}`}>
      <Icon name={level.icon} size={13} />
      {SENSITIVITY_LABEL[sensitivity]}
    </span>
  )
}
