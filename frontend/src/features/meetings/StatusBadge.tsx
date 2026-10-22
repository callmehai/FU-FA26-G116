import { Icon, type IconName } from '../../components/Icon'
import { MEETING_STATUS_LABEL, SENSITIVITY_LABEL, type MeetingStatus, type Sensitivity } from '../../types/meeting'
import { STATUS_TONE } from './statusTone'

/**
 * A meeting's lifecycle state as a rounded chip with a dot.
 *
 * `label` overrides the text while keeping the colour. The history timeline needs that: a row
 * there records what *happened* ("Đã gửi duyệt"), not what the meeting *is* ("Chờ duyệt"), and
 * passes `plain`: its own dot already sits on the timeline.
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
 * shape as well as colour. Red is reserved for "Mật".
 */
export function SensitivityBadge({ sensitivity }: { sensitivity: Sensitivity }) {
  const level = LEVEL[sensitivity]
  return (
    <span className={`level ${level.className}`}>
      <Icon name={level.icon} size={13} />
      {SENSITIVITY_LABEL[sensitivity]}
    </span>
  )
}
