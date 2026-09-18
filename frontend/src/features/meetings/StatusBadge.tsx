import { Badge, type BadgeTone } from '../../components/Badge'
import { MEETING_STATUS_LABEL, type MeetingStatus } from '../../types/meeting'

/**
 * Colour per lifecycle state, chosen to read semantically: amber waits, red is refused, emerald is
 * approved, blue is announced, indigo is live, violet is issued, slate is inert (draft, ended,
 * archived).
 */
const TONE: Record<MeetingStatus, BadgeTone> = {
  Draft: 'slate',
  WaitingApproval: 'amber',
  Rejected: 'red',
  Approved: 'emerald',
  Published: 'blue',
  InProgress: 'indigo',
  Ended: 'slate',
  MinutesIssued: 'violet',
  Archived: 'slate',
}

/**
 * A meeting's lifecycle state as a colour-coded chip.
 *
 * `label` overrides the text while keeping the colour. The history timeline needs that: a row
 * there records what *happened* ("Đã gửi duyệt"), not what the meeting *is* ("Chờ duyệt").
 */
export function StatusBadge({ status, label }: { status: MeetingStatus; label?: string }) {
  return <Badge tone={TONE[status]}>{label ?? MEETING_STATUS_LABEL[status]}</Badge>
}
