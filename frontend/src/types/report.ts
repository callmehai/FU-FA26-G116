import type { MeetingStatus, Sensitivity } from './meeting'

/**
 * SCRUM-38 (UC-70a) — what `GET /api/reports/meetings` returns: counts, rates and averages only, never a meeting's id
 * or title (BR-437). `byMonth` lists every month of the period and the other two every value, zeros included.
 * Months are "yyyy-MM" in Vietnam time. Contract: docs/03-api/22-bao-cao-thong-ke.md.
 */
export interface MeetingReport {
  from: string
  to: string
  generatedAt: string
  total: number
  byMonth: { month: string; count: number }[]
  byStatus: { status: MeetingStatus; count: number }[]
  bySensitivity: { sensitivity: Sensitivity; count: number }[]
  /** BR-435: a fraction (0.87 is 87 %), null when no Ended meeting had attendance taken. */
  attendance: { averageRate: number | null; meetings: number; meetingsWithoutAttendance: number }
  /** PO 09/11/2026 (Q2): actual start to actual end of Ended meetings, null when none has both. */
  duration: { averageMinutes: number | null; meetings: number; meetingsWithoutTimes: number }
  /** BR-436: actual end to the first issue of the minutes, null when none is issued yet. */
  minutesTurnaround: { averageHours: number | null; meetings: number; meetingsWithoutIssuedMinutes: number }
}
