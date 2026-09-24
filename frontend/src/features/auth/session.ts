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
 * - The refresh token is an HttpOnly cookie the backend sets on /api/auth/*. This code never sees
 *   it, so an XSS on the page cannot read it either; the browser attaches it by itself.
 * - localStorage keeps only a "signed in" marker, which is no credential. It saves anonymous
 *   visitors a pointless /refresh, tells "your session expired" apart from "you never signed in",
 *   and carries a sign-out to the other tabs. Who the user is always comes from the backend, never
 *   from storage the user can edit.
 *
 * Refresh tokens are single-use, and the backend treats a token presented twice as stolen: it ends
 * every session of that account. So every refresh goes through one shared promise, and through a
 * Web Lock so two tabs sharing the one cookie take turns as well.
 */

const SIGNED_IN_KEY = 'trustcabinet.signedIn'
/** Keys earlier versions kept: a whole mock user object, then the refresh token. Removed on sight. */
const LEGACY_KEYS = ['trustcabinet.session', 'trustcabinet.refreshToken']
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

/**
 * Whether a refresh cookie is worth trying. When storage is unavailable the marker cannot be read,
 * so assume yes: a spare /refresh that answers 401 beats losing a session on every reload.
 */
function mightBeSignedIn() {
  try {
    return localStorage.getItem(SIGNED_IN_KEY) !== null
  } catch {
    return true
  }
}

function markSignedIn(signedIn: boolean) {
  try {
    if (signedIn) localStorage.setItem(SIGNED_IN_KEY, '1')
    else localStorage.removeItem(SIGNED_IN_KEY)
  } catch {
    // Only the marker is lost; the session itself lives in the cookie and in memory.
  }
}

/** Adopts a fresh token pair from /login, /google or /refresh. */
export function startSession(response: SignInResponse) {
  accessToken = response.accessToken
  accessTokenExpiresAt = Date.parse(response.accessTokenExpiresAt)
  markSignedIn(true)
  emit(response.user)
}

/** Forgets the session locally. The cookie is the backend's to clear. */
function endSession(expired: boolean) {
  accessToken = null
  accessTokenExpiresAt = 0
  markSignedIn(false)
  emit(null, expired)
}

function withRefreshLock<T>(work: () => Promise<T>): Promise<T> {
  // Without Web Locks the in-tab single flight still holds; only the cross-tab race reopens.
  return typeof navigator !== 'undefined' && navigator.locks
    ? navigator.locks.request(REFRESH_LOCK, work)
    : work()
}

/**
 * Trades the refresh cookie for a new pair. Resolves to null when the backend no longer accepts it
 * — the session is over and listeners have been told. Rejects on network or server failure,
 * leaving the session alone: an outage is not a reason to sign anyone out.
 */
function refresh(): Promise<SignInResponse | null> {
  refreshing ??= withRefreshLock(async () => {
    // Checked inside the lock: another tab may have signed out while this one waited. If another
    // tab rotated the cookie instead, the browser sends the new one — nothing to re-read here.
    if (!mightBeSignedIn()) return null

    try {
      const response = await authApi.refreshSession()
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

/** On app start: rebuilds the session from the refresh cookie, if this browser was signed in. */
export async function restoreSession(): Promise<CurrentUser | null> {
  try {
    for (const key of LEGACY_KEYS) localStorage.removeItem(key)
  } catch {
    // Nothing to clean up when storage is unavailable.
  }

  if (!mightBeSignedIn()) return null
  try {
    return (await refresh())?.user ?? null
  } catch {
    // Backend unreachable. Keep the marker so the next load can try again.
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
  if (!accessToken && !mightBeSignedIn()) return null
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
 * UC-03. Only the backend can revoke the refresh token and clear its cookie — JavaScript cannot
 * touch an HttpOnly cookie. So the call has to succeed: if it does not reach the server, the
 * cookie survives and the next reload would sign the user straight back in.
 *
 * Taken under the refresh lock, after any refresh already in flight. Racing one, the server would
 * rotate the token the logout came to revoke, and the refresh's new cookie could land after the
 * logout cleared it — a user shown the login page whom a reload signs straight back in. The
 * session ends inside the lock too, so a tab waiting to refresh finds it over rather than trying.
 *
 * Returns false when the server could not be reached — UC-03's MSG01 — and leaves the session as
 * it was, so the user can retry rather than believe they signed out.
 */
export async function signOut() {
  await refreshing?.catch(() => undefined)
  try {
    await withRefreshLock(async () => {
      await authApi.signOut()
      endSession(false)
    })
    return true
  } catch {
    return false
  }
}

configureAuth({
  async getAccessToken() {
    if (accessToken && accessTokenExpiresAt - Date.now() > REFRESH_MARGIN_MS) return accessToken
    if (!mightBeSignedIn()) return accessToken
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

// Signing out in one tab signs out every tab: they all share the one refresh cookie, which the
// sign-out has just revoked. Signing in works the same way the other way round: the new cookie is
// shared too, so a tab without a session picks it up with a refresh instead of waiting for F5.
// The marker only changes on sign-in and sign-out, never on a refresh, so this costs nothing
// while a session merely continues.
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key !== SIGNED_IN_KEY) return
    if (event.newValue !== null && !accessToken) {
      // An outage just leaves this tab signed out, as it was.
      void refresh().catch(() => undefined)
      return
    }
    if (event.newValue === null && accessToken) {
      accessToken = null
      accessTokenExpiresAt = 0
      emit(null)
    }
  })
}
