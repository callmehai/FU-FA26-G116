import type { MeetingStatus } from './meeting'

/**
 * Meeting participants (SCRUM-20, SCRUM-97). Contract in docs/03-api/09-thanh-phan-tham-du.md.
 *
 * A meeting always has exactly one Chair; from Published on it also keeps at least one Secretary.
 * Guests are invited by email and have no internal role.
 */
export const MEETING_ROLES = ['Chair', 'Secretary', 'Member'] as const
export type MeetingRole = (typeof MEETING_ROLES)[number]

export const MEETING_ROLE_LABEL: Record<MeetingRole, string> = {
  Chair: 'Chủ tọa',
  Secretary: 'Thư ký',
  Member: 'Thành viên',
}

/**
 * An invitee's answer, as in AI-eCabinet: asked once, final, never of the Chair. "Pending" means not
 * answered yet, which counts as attending. It changes no access.
 */
export type InvitationResponse = 'Pending' | 'Accepted' | 'Declined'

/** GET /api/meetings/{id}/participant-context — what the caller may do with the roster right now. */
export interface ParticipantContext {
  meetingId: string
  status: MeetingStatus
  /** The roster revision this answer was read at; sent back in `If-Match` with every change. */
  revision: string
  myRole: MeetingRole | 'Guest' | null
  permissions: {
    /** Chair or Secretary, while the status still allows roster changes. */
    canManageParticipants: boolean
    /** May move the Chair to someone else (Published/InProgress: the Chair, or a Secretary when the Chair's account is inactive). */
    canReplaceChair: boolean
    canRespond: boolean
  }
}

/** One row of the roster: an internal assignment or a guest invitation. */
export interface ParticipantRow {
  /** The assignment id (internal) or the invitation id (guest) — what DELETE and role changes take. */
  id: string
  kind: 'Internal' | 'Guest'
  displayName: string
  email: string
  /** Null for a guest. */
  role: MeetingRole | null
  userId: string | null
  /** False when the internal account was deactivated; null for a guest. */
  isActive: boolean | null
  invitedName: string | null
  selfDeclaredDisplayName: string | null
  selfDeclaredJobTitle: string | null
  response: InvitationResponse
  respondedAt: string | null
}

/** GET /api/meetings/{id}/participants — sorted by name, searched and paged on the server. */
export interface ParticipantPage {
  items: ParticipantRow[]
  /** After the search. */
  total: number
  page: number
  pageSize: number
  revision: string
}

/** An active account that can be invited. */
export interface ParticipantOption {
  id: string
  fullName: string
  email: string
}

/**
 * GET /api/meetings/participant-options (before the meeting exists) and
 * GET /api/meetings/{id}/participants/editor-options (people not on the roster yet).
 */
export interface ParticipantOptionPage {
  items: ParticipantOption[]
  total: number
  page: number
  pageSize: number
  roles: MeetingRole[]
}

/** One entry of `participants` on POST /api/meetings. */
export interface ParticipantSeed {
  userId: string
  role: MeetingRole
}

/** PUT /api/meetings/{id}/participants/assignments. */
export interface AssignmentsRequest {
  changes: { participantId: string; role: MeetingRole }[]
  /** Required whenever the Chair moves: the outgoing Chair is demoted and the new one promoted together. */
  chairReplacement?: { outgoingParticipantId: string; incomingParticipantId: string; confirmed: true }
}

/** Success of a roster change: MSG96 added, MSG100 role changed, MSG257 Chair handed over, MSG103 removed, MSG154 nothing changed. */
export interface ParticipantMutationResponse {
  code: string
  message: string
  revision?: string
  createdIds?: string[]
}

/** PUT /api/meetings/{id}/invitation-response — MSG105, or MSG154 when the same answer was already recorded. */
export interface InvitationResponseResult {
  code: string
  message: string
  response: {
    id: string
    kind: 'Internal' | 'Guest'
    response: InvitationResponse
    respondedAt: string | null
    canRespond: boolean
  }
}

/**
 * `participantsChanged` on `/hubs/meetings`. Only says "re-read": an answer to the invitation keeps
 * the same revision, so a revision that did not move is no reason to skip it.
 */
export interface ParticipantsChangedEvent {
  eventId: string
  meetingId: string
  revision: string
  occurredAt: string
}
