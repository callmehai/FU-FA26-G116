import type {
  Meeting,
  MeetingApprover,
  MeetingHistoryPage,
  MeetingListQuery,
  MeetingListResponse,
  MeetingMutationResponse,
  MeetingRequest,
  MeetingSubmissionReadiness,
  MeetingTransitionRequest,
  MeetingTransitionResponse,
} from '../types/meeting'
import { api } from './client'

/**
 * Meetings (SCRUM-17: create, update; SCRUM-18: list; SCRUM-19: approval and publishing). Contract
 * in docs/03-api/05-quan-ly-cuoc-hop.md.
 *
 * Reading a meeting needs a relationship with it rather than a capability, so a caller who may not
 * see it gets the same empty 404 as an unknown id. Creating and editing need `MeetingCreator`, and
 * editing also needs to be the meeting's creator — anyone else gets that 404 too.
 */

/**
 * UC-17 — filtering, search, sorting and paging all happen in the database, after the row-visibility
 * rule, so a meeting the caller may not see never reaches the browser and is not counted.
 */
export function listMeetings(query: MeetingListQuery) {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') params.set(key, String(value))
  }
  // URLSearchParams encodes a timestamp's "+" offset as %2B, which is what the backend requires.
  return api.get<MeetingListResponse>(`/api/meetings?${params}`)
}

export const getMeeting = (id: string) => api.get<Meeting>(`/api/meetings/${id}`)

/** UC-14 — 201 with MSG29. The meeting starts as a draft. */
export const createMeeting = (body: MeetingRequest) => api.post<MeetingMutationResponse>('/api/meetings', body)

/** UC-15 — MSG30, or 409 MSG33 once the meeting has left Draft/Rejected. */
export const updateMeeting = (id: string, body: MeetingRequest) =>
  api.put<MeetingMutationResponse>(`/api/meetings/${id}`, body)

/**
 * Who can be picked as approver: active accounts holding `MeetingApprover`, minus the caller.
 * Needs `MeetingCreator`. 503 when the list cannot be read.
 */
export const listMeetingApprovers = () => api.get<MeetingApprover[]>('/api/meetings/approvers')

/**
 * SCRUM-19 — submit, approve, reject, return to draft or publish. The server re-checks the actor's
 * relationship and the current status inside one transaction; a status that moved on meanwhile
 * answers 409 and writes nothing (MSG41 submit, MSG46 publish, or the approval/return-to-draft codes).
 */
export const transitionMeeting = (id: string, body: MeetingTransitionRequest) =>
  api.post<MeetingTransitionResponse>(`/api/meetings/${id}/transitions`, body)

/** SCRUM-19 — lifecycle history, oldest first. Rejection reasons are hidden from participants. */
export const getMeetingHistory = (id: string, page = 1, pageSize = 50) =>
  api.get<MeetingHistoryPage>(`/api/meetings/${id}/history?page=${page}&pageSize=${pageSize}`)

/** SCRUM-19 — whether a meeting may be (re)submitted yet. Organizer only, Draft or Rejected (else 409 MSG41). */
export const getMeetingSubmissionReadiness = (id: string) =>
  api.get<MeetingSubmissionReadiness>(`/api/meetings/${id}/submission-readiness`)
