import type { MeetingStatus } from './meeting'

/**
 * Register to Speak (SCRUM-27, UC-79). Contract in docs/03-api/20-dang-ky-phat-bieu.md.
 *
 * Only open requests are listed: closed ones (`Finished`, `Cancelled`) stay with the meeting for the minutes
 * (BR-339) but never come back here.
 */
export type SpeakingStatus = 'Queued' | 'Invited' | 'Speaking'

export interface SpeakingRequest {
  id: string
  kind: 'User' | 'Guest'
  /** A guest's is the name they gave, or the one on the invitation. */
  displayName: string
  /** The meeting role; a guest's is "Guest". */
  role: 'Chair' | 'Secretary' | 'Member' | 'Guest' | null
  status: SpeakingStatus
  queuePosition: number
  requestedAt: string
  invitedAt: string | null
  startedAt: string | null
  /** The agenda item under discussion when the hand went up, picked by the server. */
  agendaItemId: string | null
  agendaItemTitle: string | null
  isMine: boolean
}

export interface SpeakingQueue {
  meetingId: string
  meetingStatus: MeetingStatus
  /** The caller is the current Chair: invite, decline, reorder and close turns. Rechecked on every action. */
  canManage: boolean
  /** The caller's own open request, null when the hand is down. */
  myRequestId: string | null
  /** The invited or speaking person first, then the queue in the Chair's order. */
  requests: SpeakingRequest[]
}

/** `speakingChanged` on `/hubs/meetings`, and `speakingInvited` to the invited person alone: only says "re-read". */
export interface SpeakingChangedEvent {
  eventId: string
  meetingId: string
  changeType: 'Raise' | 'Lower' | 'MoveUp' | 'MoveDown' | 'Decline' | 'Invite' | 'Start' | 'Finish' | 'Left'
  requestId: string | null
  occurredAt: string
}
