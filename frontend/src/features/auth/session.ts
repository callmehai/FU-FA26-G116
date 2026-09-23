import * as authApi from '../../api/auth'
import { ApiError, configureAuth } from '../../api/client'
import type { SignInResponse } from '../../types/auth'
import type { CurrentUser } from '../../types/user'

/**
 * Token handling for the signed-in session (SCRUM-11), kept outside React so `api/client.ts` can
 * ask for a token from any request without going through a component.
 *
 * - The access token lives only in memory. It is valid for 15 minutes; losing it on reload costs
 *   one refresh.
 * - The refresh token lives in localStorage so the session survives a reload. That puts it within
 *   reach of any XSS on the page — the accepted trade-off until the backend moves it to an
 *   HttpOnly cookie (see docs/03-api/01-xac-thuc.md, *Còn nợ*). Nothing else about the session is
 *   stored: who the user is always comes from the backend, never from storage the user can edit.
 *
 * Refresh tokens are single-use. Two refreshes racing with the same token means one wins and the
 * other signs the user out for no reason, so every refresh goes through one shared promise, and
 * through a Web Lock so two tabs sharing the stored token take turns as well.
 */

const REFRESH_TOKEN_KEY = 'trustcabinet.refreshToken'
/** Where the old browser-side mock kept a whole user object. Removed on sight. */
const LEGACY_SESSION_KEY = 'trustcabinet.session'
const REFRESH_LOCK = 'trustcabinet.refresh'
/** Refresh this long before the access token expires, rather than waiting for a 401. */
const REFRESH_MARGIN_MS = 60_000

let accessToken: string | null = null
let accessTokenExpiresAt = 0
let refreshing: Promise<SignInResponse | null> | null = null

/** `expired` is true when the backend ended a session the user did not sign out of. */
type Listener = (user: CurrentUser | null, expired: boolean) => void
const listeners = new Set<Listener>()

export function subscribe(listener: Listener) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

function emit(user: CurrentUser | null, expired = false) {
  for (const listener of listeners) listener(user, expired)
}

function readRefreshToken() {
  try {
    return localStorage.getItem(REFRESH_TOKEN_KEY)
  } catch {
    return null
  }
}

/** Returns false when storage refused the write — for a removal, the token is still there. */
function writeRefreshToken(token: string | null) {
  try {
    if (token) localStorage.setItem(REFRESH_TOKEN_KEY, token)
    else localStorage.removeItem(REFRESH_TOKEN_KEY)
    return true
  } catch {
    return false
  }
}

/** Adopts a fresh token pair from /login, /google or /refresh. */
export function startSession(response: SignInResponse) {
  accessToken = response.accessToken
  accessTokenExpiresAt = Date.parse(response.accessTokenExpiresAt)
  // A failed write only means the session will not survive a reload — not worth failing over.
  writeRefreshToken(response.refreshToken)
  emit(response.user)
}

/** Forgets the session locally. Returns false when the stored refresh token could not be removed. */
function endSession(expired: boolean) {
  accessToken = null
  accessTokenExpiresAt = 0
  const cleared = writeRefreshToken(null)
  emit(null, expired)
  return cleared
}

function withRefreshLock<T>(work: () => Promise<T>): Promise<T> {
  // Without Web Locks the in-tab single flight still holds; only the cross-tab race reopens.
  return typeof navigator !== 'undefined' && navigator.locks
    ? navigator.locks.request(REFRESH_LOCK, work)
    : work()
}

/**
 * Trades the stored refresh token for a new pair. Resolves to null when the backend no longer
 * accepts it — the session is over and listeners have been told. Rejects on network or server
 * failure, leaving the session alone: an outage is not a reason to sign anyone out.
 */
function refresh(): Promise<SignInResponse | null> {
  refreshing ??= withRefreshLock(async () => {
    // Read inside the lock: another tab may have rotated the token while this one waited.
    const token = readRefreshToken()
    if (!token) return null

    try {
      const response = await authApi.refreshSession(token)
      startSession(response)
      return response
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        endSession(true)
        return null
      }
      throw error
    }
  }).finally(() => {
    refreshing = null
  })

  return refreshing
}

/** On app start: rebuilds the session from the stored refresh token, if there is one. */
export async function restoreSession(): Promise<CurrentUser | null> {
  try {
    localStorage.removeItem(LEGACY_SESSION_KEY)
  } catch {
    // Nothing to clean up when storage is unavailable.
  }

  if (!readRefreshToken()) return null
  try {
    return (await refresh())?.user ?? null
  } catch {
    // Backend unreachable. Keep the refresh token so the next load can try again.
    return null
  }
}

/**
 * Re-reads the user from GET /api/auth/me so the UI picks up capability changes an administrator
 * made — that endpoint reads the database, not the token (docs/03-api/04-phan-quyen-phia-server.md).
 * An expired access token is refreshed by the client on the way; a 401 that survives it means the
 * account is gone or deactivated, and the session ends. Any other failure leaves it alone.
 */
export async function reloadSession(): Promise<CurrentUser | null> {
  if (!accessToken && !readRefreshToken()) return null
  try {
    const user = await authApi.getCurrentUser()
    emit(user)
    return user
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) endSession(true)
    return null
  }
}

/**
 * UC-03. Revokes the refresh token server-side first — otherwise it stays usable for up to seven
 * days on whatever copy exists — then forgets it locally. The server call always answers 204, so
 * a failure there is a network problem and must not trap the user in a session they want to leave.
 *
 * Returns false when the local token could not be cleared: UC-03's MSG01.
 */
export async function signOut() {
  const token = readRefreshToken()
  if (token) await authApi.signOut(token).catch(() => undefined)
  return endSession(false)
}

configureAuth({
  async getAccessToken() {
    if (accessToken && accessTokenExpiresAt - Date.now() > REFRESH_MARGIN_MS) return accessToken
    if (!readRefreshToken()) return accessToken
    try {
      return (await refresh())?.accessToken ?? null
    } catch {
      // Could not refresh right now; the current token may still have a few seconds left.
      return accessToken
    }
  },

  async recoverFromUnauthorized() {
    try {
      return (await refresh())?.accessToken ?? null
    } catch {
      return null
    }
  },
})

// Signing out in one tab signs out every tab: they all share the one stored refresh token.
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === REFRESH_TOKEN_KEY && event.newValue === null && accessToken) {
      accessToken = null
      accessTokenExpiresAt = 0
      emit(null)
    }
  })
}
