import type { Meeting } from '../../../types/meeting'

/**
 * Whether the caller may start or end this meeting now (SCRUM-69, UC-20, UC-21): the Chair or the Secretary starts it
 * (PO 20/11/2026), only the Chair ends it. The server decides and re-checks: MSG112/MSG114 when the caller's role or the
 * status moved on.
 */
export function lifecycleAction(meeting: Meeting): 'Start' | 'End' | null {
  const role = meeting.myMeetingRole
  if (meeting.status === 'Published' && (role === 'Chair' || role === 'Secretary')) return 'Start'
  if (meeting.status === 'InProgress' && role === 'Chair') return 'End'
  return null
}
