/** Shape returned by GET /api/health — mirrors the anonymous type in Program.cs. */
export interface HealthStatus {
  /** "unavailable" (with HTTP 503) when the database cannot be reached — ADR-0047. */
  status: 'ok' | 'unavailable'
  service: string
  version: string
  database: 'ok' | 'unreachable'
  timeUtc: string
}
