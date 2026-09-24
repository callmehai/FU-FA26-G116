import type { CurrentUser } from './user'

/**
 * What POST /api/auth/login, /google and /refresh return on success — the same shape for all
 * three, so the client never needs to know how a session was started.
 *
 * No refresh token: the backend sets it as an HttpOnly cookie, which this code never sees.
 */
export interface SignInResponse {
  /** JWT, 15 minutes by default. Sent as `Authorization: Bearer …`. */
  accessToken: string
  accessTokenExpiresAt: string
  user: CurrentUser
}
