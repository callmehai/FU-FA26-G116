import type { MeetingSession } from '../types/meetingSession'
import { api } from './client'

/**
 * UC-53 — a meeting's sessions, ordered by planned start, cancelled ones included. Same visibility
 * as the meeting itself: a meeting the caller may not read answers an empty 404. MSG127 on 500.
 */
export const listMeetingSessions = (meetingId: string) =>
  api.get<MeetingSession[]>(`/api/meetings/${meetingId}/sessions`)
