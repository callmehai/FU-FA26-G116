import type { MeetingStatus } from './meeting'
import type { InvitationResponse, MeetingRole } from './participant'

/**
 * Attendance (SCRUM-26, UC-31). Contract in docs/03-api/10-diem-danh.md.
 *
 * Only the current Secretary records it, only while the meeting is InProgress, and only as Present
 * or Absent. Someone not recorded yet is "Chưa điểm danh" — not Absent. Joining the call room or
 * accepting the invitation never marks anyone present.
 */
export type AttendanceStatus = 'Present' | 'Absent'

export const ATTENDANCE_LABEL: Record<AttendanceStatus, string> = {
  Present: 'Có mặt',
  Absent: 'Vắng',
}

/** Values recorded before Present/Absent were the only choices; shown, never written. */
export type LegacyAttendance = 'Excused' | 'Delegated'

export const LEGACY_ATTENDANCE_LABEL: Record<LegacyAttendance, string> = {
  Excused: 'Vắng có phép',
  Delegated: 'Ủy quyền',
}

export interface AttendanceRecorder {
  id: string
  displayName: string
}

/** One person on the current roster: an internal assignment or a guest invitation. */
export interface AttendanceRow {
  /** The assignment id (internal) or the invitation id (guest) — never the user id. */
  id: string
  kind: 'Internal' | 'Guest'
  displayName: string
  /** Null for a guest. */
  role: MeetingRole | null
  /** False when the internal account was deactivated; null for a guest. */
  isActive: boolean | null
  invitationResponse: InvitationResponse
  /** Null while not recorded, or when only a legacy value is stored. */
  attendance: AttendanceStatus | null
  legacyAttendance: LegacyAttendance | null
  /** Opaque token of this person's attendance; sent back as `expectedVersion`. */
  rowVersion: string
  recordedBy: AttendanceRecorder | null
  recordedAt: string | null
}

/** GET /api/meetings/{id}/attendance — sorted by name and paged on the server. */
export interface AttendancePage {
  meetingId: string
  meetingStatus: MeetingStatus
  serverTime: string
  permissions: { canRecordAttendance: boolean; canViewInvitationResponses: boolean }
  items: AttendanceRow[]
  total: number
  page: number
  pageSize: number
  /** Over the whole roster, not only this page. */
  totals: { present: number; absent: number; unmarked: number; legacy: number; total: number }
}

/** One entry of POST /api/meetings/{id}/attendance. */
export interface AttendanceEntry {
  id: string
  kind: AttendanceRow['kind']
  attendance: AttendanceStatus
  /** The `rowVersion` the change was decided on. */
  expectedVersion: string
}

/** 200 MSG147, also when nothing changed (`changedCount` 0). Entries come back in request order. */
export interface AttendanceSaved {
  code: string
  message: string
  meetingId: string
  changedCount: number
  entries: Pick<
    AttendanceRow,
    'id' | 'kind' | 'attendance' | 'legacyAttendance' | 'rowVersion' | 'recordedBy' | 'recordedAt'
  >[]
}

/** `attendanceChanged` on `/hubs/meetings`: only says "re-read", carries no roster data. */
export interface AttendanceChangedEvent {
  eventId: string
  meetingId: string
  occurredAt: string
}
