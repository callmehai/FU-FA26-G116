import type { IconName } from '../../components/Icon'
import type { NotificationType } from '../../types/notification'

/** One of the soft chip palettes declared on :root (`--chip-*`). */
export type KindTone = 'blue' | 'amber' | 'violet' | 'emerald' | 'red' | 'indigo' | 'slate'

interface Kind {
  icon: IconName
  tone: KindTone
}

/**
 * How each kind of notification looks in the panel, so a row says what it is about before it is
 * read. The title and body come from the backend; this only picks the icon and its colour.
 */
const KINDS: Record<NotificationType, Kind> = {
  MeetingInvited: { icon: 'calendar', tone: 'blue' },
  MeetingWaitingApproval: { icon: 'checkCircle', tone: 'violet' },
  MeetingApproved: { icon: 'checkCircle', tone: 'emerald' },
  MeetingRejected: { icon: 'xCircle', tone: 'red' },
  MeetingPublished: { icon: 'send', tone: 'emerald' },
  MeetingStartingSoon: { icon: 'clock', tone: 'amber' },
  MeetingStarted: { icon: 'play', tone: 'emerald' },
  MeetingRescheduled: { icon: 'calendar', tone: 'amber' },
  MeetingCancelled: { icon: 'calendarX', tone: 'red' },
  VotingOpened: { icon: 'checkSquare', tone: 'indigo' },
  MinutesWaitingApproval: { icon: 'fileText', tone: 'violet' },
  MinutesIssued: { icon: 'fileText', tone: 'emerald' },
  MinutesReturned: { icon: 'reply', tone: 'amber' },
  DocumentAdded: { icon: 'paperclip', tone: 'slate' },
  MeetingRoleChanged: { icon: 'users', tone: 'blue' },
  MeetingParticipantRemoved: { icon: 'calendarX', tone: 'slate' },
  AgendaPresenterAssigned: { icon: 'meeting', tone: 'indigo' },
  CalendarShareRequested: { icon: 'calendarCheck', tone: 'violet' },
  CalendarShareReceived: { icon: 'calendarCheck', tone: 'blue' },
  CalendarShareAccepted: { icon: 'calendarCheck', tone: 'emerald' },
  CalendarShareDeclined: { icon: 'calendarX', tone: 'slate' },
  CalendarShareChangeRequested: { icon: 'calendarCheck', tone: 'violet' },
  CalendarShareUpdated: { icon: 'calendarCheck', tone: 'blue' },
  CalendarShareChangeDeclined: { icon: 'calendarX', tone: 'slate' },
  CalendarShareWithdrawn: { icon: 'calendarX', tone: 'slate' },
  CalendarShareEnded: { icon: 'calendarX', tone: 'slate' },
  CalendarShareStopped: { icon: 'calendarX', tone: 'red' },
}

/** A kind added on the backend later still renders, as a plain bell. */
const FALLBACK: Kind = { icon: 'bell', tone: 'slate' }

export function notificationKind(type: string): Kind {
  return KINDS[type as NotificationType] ?? FALLBACK
}
