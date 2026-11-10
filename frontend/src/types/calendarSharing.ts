/**
 * SCRUM-142 — calendar sharing (UC-84a → UC-84f, UC-85c), as `docs/03-api/23-chia-se-lich.md` §3–§4 sends it.
 * Busy/free only: no field ever names a meeting (BR-427). Dates are `YYYY-MM-DD` in Vietnam time.
 */

/** `Level1`: ordinary meetings; `Level2`: ordinary and internal. Secret meetings are never shared (BR-414). */
export type ShareLevel = 'Level1' | 'Level2'

export type ShareStatus = 'Pending' | 'Effective' | 'Declined' | 'Withdrawn' | 'Ended' | 'Stopped'

/** What the caller may do with a share right now — the buttons follow this, never the status alone. */
export type ShareAction = 'accept' | 'decline' | 'withdraw' | 'requestChange' | 'update' | 'end'

export interface SharePerson {
  id: string
  fullName: string
  email: string
}

export interface Share {
  id: string
  /** `Viewer`: asked for by the viewer (UC-84a); `Owner`: shared by the owner (UC-84b). */
  initiatedBy: 'Viewer' | 'Owner'
  level: ShareLevel
  owner: SharePerson
  viewer: SharePerson
  startDate: string
  endDate: string
  status: ShareStatus
  /** The viewer's request to raise the level or extend the range, waiting for the owner (UC-84d). */
  pendingChange: { level: ShareLevel; endDate: string; requestedAt: string } | null
  createdAt: string
  decidedAt: string | null
  closedAt: string | null
  /** The sender of a Pending share was deactivated: it can only be declined (UC-84c EX-5, MSG468). */
  senderDeactivated: boolean
  actions: ShareAction[]
}

export interface ShareLists {
  /** Shares of the caller's own calendar. */
  asOwner: Share[]
  /** Shares the caller may view; always empty without the right to create meetings. */
  asViewer: Share[]
}

export interface ShareCandidate extends SharePerson {
  /** A Pending or Effective share in the same direction already exists. */
  hasOpenShare: boolean
}

export interface ShareCandidatePage {
  items: ShareCandidate[]
  total: number
  page: number
  pageSize: number
}

export interface ShareResult {
  code: string
  message: string
  share: Share
}

/** Values sent to accept (narrowed), change or update a share; omitted fields stay as they are. */
export interface ShareValues {
  level?: ShareLevel
  startDate?: string
  endDate?: string
}

export interface SharedWeekDay {
  date: string
  /** False outside the share's dates: unknown, never drawn as free (BR-415). */
  known: boolean
  busy: { start: string; end: string }[]
}

export interface SharedWeekPerson {
  shareId: string
  owner: SharePerson
  status: 'Effective' | 'Stopped'
  level: ShareLevel
  startDate: string
  endDate: string
  /** Monday to Friday; empty for a Stopped share. */
  days: SharedWeekDay[]
}

export interface SharedWeek {
  weekStart: string
  weekEnd: string
  /** "08:00": every busy block is already cut to this window. */
  dayStart: string
  dayEnd: string
  people: SharedWeekPerson[]
}
