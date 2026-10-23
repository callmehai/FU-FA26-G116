import type { AttendanceEntry, AttendancePage, AttendanceSaved } from '../types/attendance'
import { api } from './client'

/**
 * Attendance (SCRUM-26, UC-31). Contract in docs/03-api/10-diem-danh.md.
 *
 * Every entry carries the `rowVersion` it was decided on. One stale entry answers 409 MSG151 and
 * saves nothing in the batch; 409 MSG149 means the meeting is no longer in progress; 503 MSG150
 * means the outcome is unknown — re-read before trying again, never retry blindly.
 */

const base = (meetingId: string) => `/api/meetings/${meetingId}/attendance`

export const getAttendance = (meetingId: string, page: number, pageSize: number) =>
  api.get<AttendancePage>(
    `${base(meetingId)}?${new URLSearchParams({ page: String(page), pageSize: String(pageSize) })}`,
  )

/** Only the entries sent change; everyone else keeps what is stored. Secretary only. */
export const saveAttendance = (meetingId: string, entries: AttendanceEntry[]) =>
  api.post<AttendanceSaved>(base(meetingId), { entries })
