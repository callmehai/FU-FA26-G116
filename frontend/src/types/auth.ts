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

/** POST /api/auth/password-reset — UC-07's second half. `token` comes from the email link's fragment. */
export interface PasswordResetRequest {
  token: string
  newPassword: string
}

/** Its success body (MSG92). No tokens: the user signs in again with the new password. */
export interface PasswordResetResponse {
  code: string
  message: string
}
