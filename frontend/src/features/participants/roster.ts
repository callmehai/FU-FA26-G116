import type { MeetingRole } from '../../types/participant'

/** One person picked for a new meeting. */
export interface RosterEntry {
  userId: string
  fullName: string
  email: string
  role: MeetingRole
}

/** The backend's limit on the roster sent with a new meeting. */
export const ROSTER_MAX = 100

/** The same rules the backend applies on POST (MSG277), so the commonest mistakes cost no round trip. */
export function rosterError(roster: RosterEntry[]): string | undefined {
  if (roster.length === 0) return 'Chọn ít nhất một người dự họp.'
  if (roster.length > ROSTER_MAX) return `Mỗi lần tạo chọn tối đa ${ROSTER_MAX} người.`
  if (roster.filter((p) => p.role === 'Chair').length !== 1) return 'Chọn một người làm Chủ tọa.'
  return undefined
}
