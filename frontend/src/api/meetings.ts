import type {
  CreateMeetingRequest,
  MeetingApproverOption,
  MeetingMutationResponse,
} from '../types/meeting'
import { api } from './client'

/** SCRUM-17 — active users who currently hold MeetingApprover. */
export const listMeetingApprovers = () => api.get<MeetingApproverOption[]>('/api/meetings/approvers')

/** UC-11 — create a draft meeting. */
export const createMeeting = (body: CreateMeetingRequest) =>
  api.post<MeetingMutationResponse>('/api/meetings', body)
