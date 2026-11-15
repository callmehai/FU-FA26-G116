/**
 * Where a guest's credentials live in this browser (UC-04), apart from the internal session's.
 *
 * The internal session keeps its access token in memory and its refresh token in an HttpOnly cookie
 * (session.ts). A guest has neither: the backend hands the guest token over in a body and never
 * refreshes it. Reopening the emailed link in a new tab must still skip the code while the session lasts
 * (ALT-1), so the token goes to localStorage, keyed by invitation — one browser may hold several. The
 * link token is kept beside it, so a session that ends can reopen the invitation without the email.
 * An entry past its expiry is dropped on read; a dead one is dropped as soon as the backend says so.
 */

const KEY = 'trustcabinet.guest'

export interface GuestEntry {
  /** The link token from the email's fragment; only opens the invitation and asks for a code. */
  linkToken?: string
  accessToken?: string
  expiresAt?: string
  /** Set by "Thoát", cleared by the next verification: tells another tab of this browser why its token went. */
  signedOut?: boolean
}

function readAll(): Record<string, GuestEntry> {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(KEY) ?? '{}')
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, GuestEntry>) : {}
  } catch {
    return {}
  }
}

function writeAll(entries: Record<string, GuestEntry>) {
  try {
    if (Object.keys(entries).length === 0) localStorage.removeItem(KEY)
    else localStorage.setItem(KEY, JSON.stringify(entries))
  } catch {
    // Storage unavailable: the guest verifies again on the next open, nothing worse.
  }
}

export function readGuestEntry(invitationId: string): GuestEntry {
  const entry = readAll()[invitationId.toLowerCase()] ?? {}
  if (entry.expiresAt && Date.parse(entry.expiresAt) <= Date.now()) {
    return { linkToken: entry.linkToken }
  }
  return entry
}

/** A session this browser still holds but past its time — not one taken away by "Thoát" in another tab. */
export function guestSessionExpired(invitationId: string) {
  const entry = readAll()[invitationId.toLowerCase()]
  return !!entry?.accessToken && !!entry.expiresAt && Date.parse(entry.expiresAt) <= Date.now()
}

/** Merges into the invitation's entry; `undefined` fields are removed. */
export function updateGuestEntry(invitationId: string, change: GuestEntry) {
  const entries = readAll()
  const key = invitationId.toLowerCase()
  const next = { ...entries[key], ...change }
  for (const field of Object.keys(change) as (keyof GuestEntry)[]) if (change[field] === undefined) delete next[field]
  if (Object.keys(next).length === 0) delete entries[key]
  else entries[key] = next
  writeAll(entries)
}

/** Forgets the session, keeping the link token so the invitation can be reopened. */
export function dropGuestSession(invitationId: string) {
  updateGuestEntry(invitationId, { accessToken: undefined, expiresAt: undefined })
}

/** "Thoát": forgets the session and says so, for the other tabs of this browser. */
export function signOutGuestSession(invitationId: string) {
  updateGuestEntry(invitationId, { accessToken: undefined, expiresAt: undefined, signedOut: true })
}
