import type { Meeting, MeetingApprover, MeetingMutationResponse, MeetingRequest } from '../types/meeting'
import { api } from './client'

/**
 * Meetings (SCRUM-17: UC-11 create, UC-12 update). Contract in docs/03-api/05-quan-ly-cuoc-hop.md.
 *
 * Reading a meeting needs a relationship with it rather than a capability, so a caller who may not
 * see it gets the same empty 404 as an unknown id. Creating and editing need `MeetingCreator`, and
 * editing also needs to be the meeting's creator — anyone else gets that 404 too.
 */

export const getMeeting = (id: string) => api.get<Meeting>(`/api/meetings/${id}`)

/** UC-11 — 201 with MEETING_CREATED. The meeting starts as a draft. */
export const createMeeting = (body: MeetingRequest) => api.post<MeetingMutationResponse>('/api/meetings', body)

/** UC-12 — MEETING_UPDATED, or 409 MEETING_NOT_EDITABLE once the meeting has left Draft/Rejected. */
export const updateMeeting = (id: string, body: MeetingRequest) =>
  api.put<MeetingMutationResponse>(`/api/meetings/${id}`, body)

/** Who can be picked as approver. Needs `MeetingCreator`, like the forms that use it. */
export const listMeetingApprovers = () => api.get<MeetingApprover[]>('/api/meetings/approvers')
