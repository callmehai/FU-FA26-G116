/** Shape returned by GET /api/health — mirrors the anonymous type in Program.cs. */
export interface HealthStatus {
  status: string
  service: string
  version: string
  timeUtc: string
}
