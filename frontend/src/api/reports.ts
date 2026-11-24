import type { MeetingReport, ReportTable } from '../types/report'
import { api } from './client'

const query = (from: string, to: string) => `?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`

/** UC-79a — 400 MSG463 for an invalid period (UC-79a), 500 MSG464 when the figures cannot be read. */
export const getMeetingReport = (from: string, to: string) =>
  api.get<MeetingReport>(`/api/reports/meetings${query(from, to)}`)

/**
 * UC-79b — the same figures, computed again, as an Excel file; the server records the export (BR-010).
 * 400 MSG463 for an invalid period, 500 MSG465 when the file cannot be built or the export recorded.
 */
export const exportMeetingReport = (from: string, to: string) =>
  api.blob(`/api/reports/meetings/export${query(from, to)}`)

/** The report query's parameters as the URL holds them: entity, from, to, groupBy, sort, top and the filters. */
export type ReportQueryParams = Record<string, string>

const queryString = (params: ReportQueryParams) => `?${new URLSearchParams(params).toString()}`

/**
 * The report query: 400 MSG463 for an invalid period, 400 MSG484 for a table, grouping, sort, top or filter value that
 * does not exist, 500 MSG464 when the rows cannot be read.
 */
export const getReportTable = (params: ReportQueryParams) =>
  api.get<ReportTable>(`/api/reports/query${queryString(params)}`)

/** The same table as an Excel file; the server records the export (BR-010). 500 MSG465 when it cannot. */
export const exportReportTable = (params: ReportQueryParams) =>
  api.blob(`/api/reports/query/export${queryString(params)}`)
