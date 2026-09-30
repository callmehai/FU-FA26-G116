import type {
  Meeting,
  MeetingApprover,
  MeetingHistoryQuery,
  MeetingHistoryResponse,
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
 * Meetings (SCRUM-17 CRUD, SCRUM-18 list, SCRUM-19 approval workflow). Contract in
 * docs/03-api/05-quan-ly-cuoc-hop.md.
 *
 * Reading a meeting needs a relationship with it rather than a capability, so a caller who may not
 * see it gets the same empty 404 as an unknown id. Creating and editing need `MeetingCreator`, and
 * editing also needs to be the meeting's creator — anyone else gets that 404 too.
 */

/**
 * UC-14 — filtering, search, sorting and paging all happen in the database, after the row-visibility
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

/** UC-11 — 201 with MSG29. The meeting starts as a draft. */
export const createMeeting = (body: MeetingRequest) => api.post<MeetingMutationResponse>('/api/meetings', body)

/** UC-12 — MSG30 in Draft/Rejected, or 409 MSG33 in other states. */
export const updateMeeting = (id: string, body: MeetingRequest) =>
  api.put<MeetingMutationResponse>(`/api/meetings/${id}`, body)

/** Who can be picked at submission. Needs `MeetingCreator`. */
export const listMeetingApprovers = () => api.get<MeetingApprover[]>('/api/meetings/approvers')

/** UC-15–17 — authorization, current state and transaction boundaries are enforced server-side. */
export const transitionMeeting = (id: string, body: MeetingTransitionRequest) =>
  api.post<MeetingTransitionResponse>(`/api/meetings/${id}/transitions`, body)

export const getMeetingSubmissionReadiness = (id: string) =>
  api.get<MeetingSubmissionReadiness>(`/api/meetings/${id}/submission-readiness`)

export function getMeetingHistory(id: string, query: MeetingHistoryQuery = { page: 1, pageSize: 20 }) {
  const params = new URLSearchParams({ page: String(query.page), pageSize: String(query.pageSize) })
  return api.get<MeetingHistoryResponse>(`/api/meetings/${id}/history?${params}`)
}
