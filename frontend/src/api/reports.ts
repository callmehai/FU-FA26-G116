import type { MeetingReport } from '../types/report'
import { api } from './client'

const query = (from: string, to: string) => `?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`

/** UC-70a — 400 MSG463 for an invalid period (BR-433), 500 MSG464 when the figures cannot be read. */
export const getMeetingReport = (from: string, to: string) =>
  api.get<MeetingReport>(`/api/reports/meetings${query(from, to)}`)

/**
 * UC-70b — the same figures, computed again, as an Excel file; the server records the export (BR-438).
 * 400 MSG463 for an invalid period, 500 MSG465 when the file cannot be built or the export recorded.
 */
export const exportMeetingReport = (from: string, to: string) =>
  api.blob(`/api/reports/meetings/export${query(from, to)}`)
