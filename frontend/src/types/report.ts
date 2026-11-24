import type { MeetingStatus, Sensitivity } from './meeting'

/**
 * SCRUM-38 (UC-79a) — what `GET /api/reports/meetings` returns: counts, rates and averages only, never a meeting's id
 * or title (BR-152). `byMonth` lists every month of the period and the other two every value, zeros included.
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
  /** BR-151: a fraction (0.87 is 87 %), null when no Ended meeting had attendance taken. */
  attendance: { averageRate: number | null; meetings: number; meetingsWithoutAttendance: number }
  /** PO 09/11/2026 (Q2): actual start to actual end of Ended meetings, null when none has both. */
  duration: { averageMinutes: number | null; meetings: number; meetingsWithoutTimes: number }
  /** BR-151: actual end to the first issue of the minutes, null when none is issued yet. */
  minutesTurnaround: { averageHours: number | null; meetings: number; meetingsWithoutIssuedMinutes: number }
  /** Closed votes the viewer may see, and how they were concluded. */
  votes: { closed: number; passed: number; notPassed: number; deferred: number }
  /** Meetings whose minutes were issued; revisions waiting for approval (only those the viewer runs, BR-139). */
  minutes: { issued: number; waitingApproval: number }
  /** Recordings the viewer may list: how many, hours playable (a combined recording counted once), hours transcribed. */
  recordings: { count: number; recordedHours: number; transcribedHours: number }
}

/** The main tables the report query reads (supervisor review 22/11/2026). */
export type ReportEntity = 'meetings' | 'participants' | 'votes' | 'documents' | 'minutes' | 'recordings'

/** `count`, `percent` (a fraction: 0.87 is 87 %), `hours` or `megabytes`. */
export type ReportFormat = 'count' | 'percent' | 'hours' | 'megabytes'

/**
 * `GET /api/reports/query` — one main table grouped, filtered, sorted and cut to the top groups. `groups` is how many
 * groups there were before the cut; `total` covers every row that passed the filters. A row's `key` is the value it is
 * grouped on: a month "yyyy-MM", an enum name, a user id, "guest:<invitation id>" or a meeting id; `None` when there is
 * nothing (no Chair, no conclusion, no transcript). Contract: docs/03-api/22-bao-cao-thong-ke.md.
 */
export interface ReportTable {
  entity: ReportEntity
  entityLabel: string
  groupBy: string
  groupLabel: string
  sort: string
  top: number | null
  from: string
  to: string
  generatedAt: string
  filters: { key: string; label: string; value: string; valueLabel: string }[]
  columns: { key: string; label: string; format: ReportFormat }[]
  groups: number
  total: Record<string, number | null>
  rows: { rank: number; key: string; label: string; values: Record<string, number | null> }[]
}
