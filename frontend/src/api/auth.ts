import type { SignInResponse } from '../types/auth'
import type { CurrentUser } from '../types/user'
import { api } from './client'

/**
 * Sign-in endpoints (SCRUM-11). All anonymous: the credential is in the body, and a 401 here means
 * "wrong credentials", not "your session expired" — it must never trigger a token refresh.
 *
 * UC-01 and UC-02 number their messages differently for the same situation (a deactivated account
 * is MSG03 on /login but MSG04 on /google). Keep them apart; see `LoginPage`.
 */

/** UC-01. */
export const signIn = (email: string, password: string) =>
  api.post<SignInResponse>('/api/auth/login', { email, password }, { anonymous: true })

/** UC-02. `idToken` is the `credential` Google Identity Services hands back — a JWT, not an access token. */
export const signInWithGoogle = (idToken: string) =>
  api.post<SignInResponse>('/api/auth/google', { idToken }, { anonymous: true })

/** Trades a refresh token for a new pair. The old refresh token is dead as soon as this succeeds. */
export const refreshSession = (refreshToken: string) =>
  api.post<SignInResponse>('/api/auth/refresh', { refreshToken }, { anonymous: true })

/**
 * Who the caller is and what they may do, read from the database rather than from the token's
 * claims (SCRUM-12) — the one authoritative answer for what the UI should render. 401 when the
 * account is gone or deactivated: the session is over, not merely forbidden.
 */
export const getCurrentUser = () => api.get<CurrentUser>('/api/auth/me')

/** Revokes the refresh token server-side. Always 204, even for an unknown or expired token. */
export const signOut = (refreshToken: string) =>
  api.post<void>('/api/auth/logout', { refreshToken }, { anonymous: true })
