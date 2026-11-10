/** System audit log (SCRUM-16, UC-13) — docs/03-api/21-nhat-ky-kiem-toan.md. */

/** Who did it (BR-441): an account, a Guest (named from the invitation), or the system itself. */
export interface AuditActor {
  kind: 'user' | 'guest' | 'system'
  /** The account id, or the invitation id for a Guest; null for the system. */
  id: string | null
  name: string | null
  email: string | null
}

export interface AuditLogItem {
  id: string
  occurredAt: string
  actor: AuditActor
  action: string
  /** Vietnamese name of the event; the code itself when the backend has none. */
  actionLabel: string
  objectType: string
  objectId: string
  /** Only for `User` and `Role` (BR-443) — never a meeting title or a document name. */
  objectName: string | null
  ipAddress: string | null
  /** As recorded; key names differ between event types, so never rely on a fixed one. */
  details: unknown
}

export interface AuditLogPage {
  items: AuditLogItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface AuditEventType {
  code: string
  label: string
  group: string
}

export interface AuditLogQuery {
  /** ISO 8601 with offset, both ends inclusive. */
  from?: string
  to?: string
  /** An account id, `guest` or `system`. */
  actor?: string
  action?: string
  q?: string
  page: number
  pageSize: number
}
