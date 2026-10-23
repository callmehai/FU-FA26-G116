import type { MeetingStatus } from '../../types/meeting'

/**
 * Colour per lifecycle state (ADR-0012): amber waits, red is refused, green is approved, blue is
 * announced, grey is inert. It colours the state's icon (ADR-0028); "Đang diễn ra" is the one solid
 * label, so a live meeting is the first thing found on any screen.
 */
export const STATUS_TONE: Record<MeetingStatus, string> = {
  Draft: 'slate',
  WaitingApproval: 'amber',
  Rejected: 'red',
  Approved: 'emerald',
  Published: 'blue',
  InProgress: 'live',
  Ended: 'ended',
}
