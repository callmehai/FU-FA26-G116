import type { Meeting } from '../../../types/meeting'

/**
 * Whether the Chair may start or end this meeting now (SCRUM-69, UC-21/22). The server decides and
 * re-checks: MSG112/MSG114 when the caller is no longer the Chair or the status moved on.
 */
export function lifecycleAction(meeting: Meeting): 'Start' | 'End' | null {
  if (meeting.myMeetingRole !== 'Chair') return null
  if (meeting.status === 'Published') return 'Start'
  if (meeting.status === 'InProgress') return 'End'
  return null
}
