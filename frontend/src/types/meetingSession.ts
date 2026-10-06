/** One meeting session's lifecycle (SCRUM-68). Only `Planned` sessions can be edited or cancelled. */
export type MeetingSessionStatus = 'Planned' | 'InProgress' | 'Completed' | 'Cancelled'

/**
 * One row of GET /api/meetings/{meetingId}/sessions (UC-53, docs/03-api/08-quan-ly-phien-hop.md).
 * The schedule of a meeting lives here since SCRUM-70; times are UTC.
 */
export interface MeetingSession {
  id: string
  meetingId: string
  title: string
  plannedStartsAt: string
  plannedEndsAt: string
  status: MeetingSessionStatus
}
