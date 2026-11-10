import type { AuditEventType, AuditLogPage, AuditLogQuery } from '../types/auditLog'
import { api } from './client'

/**
 * System audit log (SCRUM-16, UC-13). Both endpoints need `AdminUsers`: 401 signed out, 403 without it.
 * A bad filter answers 400 MSG469 with `errors` per parameter; an unreadable log 500 MSG470.
 */
export function listAuditLog(query: AuditLogQuery) {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') params.set(key, String(value))
  }
  return api.get<AuditLogPage>(`/api/audit-log?${params}`)
}

/** The event filter's choices, grouped, with the backend's Vietnamese names. */
export const listAuditEventTypes = () => api.get<AuditEventType[]>('/api/audit-log/event-types')
