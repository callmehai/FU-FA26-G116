import type {
  AssignmentsRequest,
  InvitationResponseResult,
  MeetingRole,
  ParticipantContext,
  ParticipantMutationResponse,
  ParticipantOptionPage,
  ParticipantPage,
} from '../types/participant'
import { api } from './client'

/**
 * Meeting participants (SCRUM-20, SCRUM-97). Contract in docs/03-api/09-thanh-phan-tham-du.md.
 *
 * Every roster change carries the revision it was decided on, as `If-Match: "participants-N"`; a
 * roster someone else changed meanwhile answers 409 MSG275 and writes nothing. Answering one's own
 * invitation is the exception: it does not touch the roster's revision.
 */

const base = (meetingId: string) => `/api/meetings/${meetingId}`
const ifMatch = (revision: string) => ({ headers: { 'If-Match': `"participants-${revision}"` } })

function paged(q: string, page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (q.trim()) params.set('q', q.trim())
  return params
}

/** Active accounts to pick from while creating a meeting. Needs `MeetingCreator`. */
export const listParticipantOptions = (q: string, page = 1, pageSize = 20) =>
  api.get<ParticipantOptionPage>(`/api/meetings/participant-options?${paged(q, page, pageSize)}`)

export const getParticipantContext = (meetingId: string) =>
  api.get<ParticipantContext>(`${base(meetingId)}/participant-context`)

export const listParticipants = (meetingId: string, q: string, page: number, pageSize: number) =>
  api.get<ParticipantPage>(`${base(meetingId)}/participants?${paged(q, page, pageSize)}`)

/** People not on the roster yet. Chair or Secretary, while the roster can still change. */
export const listEditorOptions = (meetingId: string, q: string, page = 1, pageSize = 20) =>
  api.get<ParticipantOptionPage>(`${base(meetingId)}/participants/editor-options?${paged(q, page, pageSize)}`)

export const addParticipants = (meetingId: string, revision: string, userIds: string[], role: MeetingRole) =>
  api.post<ParticipantMutationResponse>(`${base(meetingId)}/participants`, { userIds, role }, ifMatch(revision))

export const inviteGuest = (meetingId: string, revision: string, invitedName: string, email: string) =>
  api.post<ParticipantMutationResponse>(
    `${base(meetingId)}/guest-invitations`,
    { invitedName, email },
    ifMatch(revision),
  )

export const changeAssignments = (meetingId: string, revision: string, body: AssignmentsRequest) =>
  api.put<ParticipantMutationResponse>(`${base(meetingId)}/participants/assignments`, body, ifMatch(revision))

export const removeParticipant = (meetingId: string, revision: string, participantId: string) =>
  api.delete<ParticipantMutationResponse>(`${base(meetingId)}/participants/${participantId}`, ifMatch(revision))

export const revokeGuest = (meetingId: string, revision: string, invitationId: string) =>
  api.delete<ParticipantMutationResponse>(`${base(meetingId)}/guest-invitations/${invitationId}`, ifMatch(revision))

/**
 * The caller's answer to their invitation, as in AI-eCabinet: once, final (409 MSG285 for a
 * different second answer), never from the Chair (409 MSG286). Declining hides the meeting from
 * the caller's calendar and changes nothing else.
 */
export const respondToInvitation = (meetingId: string, response: 'Accepted' | 'Declined') =>
  api.put<InvitationResponseResult>(`${base(meetingId)}/invitation-response`, { response })
