/**
 * Meeting call room (SCRUM-39, SCRUM-41). Contract in docs/03-api/09-phong-hop-truc-tuyen.md.
 * Media and signalling go through LiveKit; these shapes only cover what the backend decides.
 */

/** Whether the room can be entered, computed on the server from the meeting's status and mode. */
export type CallRoomStatus = 'Open' | 'NotStarted' | 'Ended' | 'InPersonOnly'

/** A participant's role in one meeting — not their system role. */
export type MeetingRole = 'Chair' | 'Secretary' | 'Member' | 'Guest'

export const MEETING_ROLE_LABEL: Record<MeetingRole, string> = {
  Chair: 'Chủ toạ',
  Secretary: 'Thư ký',
  Member: 'Thành viên',
  Guest: 'Khách mời',
}

/** GET /api/meetings/{id}/call */
export interface CallRoomState {
  meetingId: string
  status: CallRoomStatus
  myRole: MeetingRole
  /** False when the server has no LiveKit configured; joining would answer 503. */
  available: boolean
  /** People in the room now; null when the room is not open or LiveKit could not be asked. */
  participantCount: number | null
}

/** POST /api/meetings/{id}/call/join — connect with exactly this serverUrl and token. */
export interface CallJoinResponse {
  serverUrl: string
  token: string
  roomName: string
  identity: string
  expiresAt: string
}

/** What the backend puts in each LiveKit participant's metadata. */
export interface CallParticipantMetadata {
  meetingRole?: MeetingRole
}
