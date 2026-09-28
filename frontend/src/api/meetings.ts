import type {
  Meeting,
  MeetingApprover,
  MeetingListQuery,
  MeetingListResponse,
  MeetingMutationResponse,
  MeetingRequest,
} from '../types/meeting'
import { api } from './client'

/**
 * Meetings (SCRUM-17: UC-11 create, UC-12 update; SCRUM-18: UC-14 list). Contract in
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

/** UC-11 — 201 with MEETING_CREATED. The meeting starts as a draft. */
export const createMeeting = (body: MeetingRequest) => api.post<MeetingMutationResponse>('/api/meetings', body)

/** UC-12 — MEETING_UPDATED, or 409 MEETING_NOT_EDITABLE once the meeting has left Draft/Rejected. */
export const updateMeeting = (id: string, body: MeetingRequest) =>
  api.put<MeetingMutationResponse>(`/api/meetings/${id}`, body)

/** Who can be picked as approver. Needs `MeetingCreator`, like the forms that use it. */
export const listMeetingApprovers = () => api.get<MeetingApprover[]>('/api/meetings/approvers')
