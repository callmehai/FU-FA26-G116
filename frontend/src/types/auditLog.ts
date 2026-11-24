/** System audit log (SCRUM-16, UC-12) — docs/03-api/21-nhat-ky-kiem-toan.md. */

/** Who did it (UC-12): an account, a Guest (named from the invitation), or the system itself. */
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
  /** Null for an event on no single object (a change to several roles' permissions). */
  objectId: string | null
  /**
   * The object's name: an account's or a voice owner's full name, a role's name, a meeting's title (also for its
   * minutes, recordings and sentences), a document's file name, a vote's title. Null when none is known.
   */
  objectName: string | null
  /**
   * The object is, or belongs to, a Mật meeting (Hải 20/11/2026): `objectName` is null, and the names the entry recorded
   * are left out of `details`. Shown as "Họp Mật" with the end of the id.
   */
  objectSecret: boolean
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
