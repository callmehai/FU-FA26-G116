/**
 * Note text the server has not confirmed yet, kept in this browser so a lost connection, a closed tab or a crash
 * loses nothing (SCRUM-30, BR-403). One entry per user, meeting and note; removed once the server has the same text.
 *
 * Where it is kept depends on the meeting (BR-403): a Normal meeting's text goes to `localStorage`, so it survives a
 * closed tab; a Secret meeting's only to `sessionStorage`, so it goes with the page and never stays on a shared
 * computer. Every entry goes when the user signs out (`clearNoteDrafts`) and a meeting's when the user may no longer
 * read its notes (`forgetNoteDrafts`). Storage can be unavailable (private mode, quota): the panel then works as before,
 * only without the safety net.
 */

const PREFIX = 'trustcabinet.notes.'

export interface NoteDraft {
  body: string
  /** The `updatedAt` this text was typed on; null for a note never saved. */
  base: string | null
}

/** Whose drafts, of which meeting, and whether that meeting is Secret. */
export interface DraftScope {
  userId: string
  meetingId: string
  secret: boolean
}

const prefixOf = (scope: Pick<DraftScope, 'userId' | 'meetingId'>) => `${PREFIX}${scope.userId}.${scope.meetingId}.`
const storeOf = (scope: DraftScope): Storage => (scope.secret ? sessionStorage : localStorage)

/** Every key of `store` under `prefix`, read before any is removed. */
function keysOf(store: Storage, prefix: string): string[] {
  const keys: string[] = []
  for (let i = 0; i < store.length; i++) {
    const key = store.key(i)
    if (key?.startsWith(prefix)) keys.push(key)
  }
  return keys
}

function parse(raw: string | null): NoteDraft | null {
  if (!raw) return null
  const draft = JSON.parse(raw) as Partial<NoteDraft>
  return typeof draft.body === 'string' ? { body: draft.body, base: draft.base ?? null } : null
}

export function readNoteDraft(scope: DraftScope, noteKey: string): NoteDraft | null {
  try {
    return parse(storeOf(scope).getItem(prefixOf(scope) + noteKey))
  } catch {
    return null
  }
}

export function writeNoteDraft(scope: DraftScope, noteKey: string, draft: NoteDraft) {
  try {
    storeOf(scope).setItem(prefixOf(scope) + noteKey, JSON.stringify(draft))
  } catch {
    // Storage full or blocked: the next save still carries the text.
  }
}

export function removeNoteDraft(scope: DraftScope, noteKey: string) {
  try {
    storeOf(scope).removeItem(prefixOf(scope) + noteKey)
  } catch {
    // Nothing to clean up if storage is unavailable.
  }
}

/** Every note of a meeting this browser still holds unsent. */
export function listNoteDrafts(scope: DraftScope): (NoteDraft & { noteKey: string })[] {
  const prefix = prefixOf(scope)
  const found: (NoteDraft & { noteKey: string })[] = []
  try {
    const store = storeOf(scope)
    for (const key of keysOf(store, prefix)) {
      const draft = parse(store.getItem(key))
      if (draft) found.push({ ...draft, noteKey: key.slice(prefix.length) })
    }
  } catch {
    // Storage unavailable: nothing was kept.
  }
  return found
}

/**
 * A meeting made Secret after text was kept for it: the text moves to `sessionStorage` rather than staying in
 * `localStorage` (BR-403). Nothing to do for a Normal meeting.
 */
export function settleNoteDrafts(scope: DraftScope) {
  if (!scope.secret) return
  try {
    for (const key of keysOf(localStorage, prefixOf(scope))) {
      const raw = localStorage.getItem(key)
      if (raw) sessionStorage.setItem(key, raw)
      localStorage.removeItem(key)
    }
  } catch {
    // Storage unavailable: nothing was kept.
  }
}

/** The user may no longer read this meeting's notes (EX-8): nothing of them stays on the device (BR-403). */
export function forgetNoteDrafts(userId: string, meetingId: string) {
  const prefix = prefixOf({ userId, meetingId })
  for (const store of [localStorage, sessionStorage]) {
    try {
      for (const key of keysOf(store, prefix)) store.removeItem(key)
    } catch {
      // Storage unavailable: nothing was kept.
    }
  }
}

/** On sign-out: no meeting note stays behind on a shared computer. */
export function clearNoteDrafts() {
  for (const store of [localStorage, sessionStorage]) {
    try {
      for (const key of keysOf(store, PREFIX)) store.removeItem(key)
    } catch {
      // Storage unavailable: nothing was kept.
    }
  }
}
