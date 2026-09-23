import type { CurrentUser } from './user'

/**
 * What POST /api/auth/login, /google and /refresh return on success — the same shape for all
 * three, so the client never needs to know how a session was started.
 */
export interface SignInResponse {
  /** JWT, 15 minutes by default. Sent as `Authorization: Bearer …`. */
  accessToken: string
  /** Opaque and single-use: every refresh returns a new one and kills the old. */
  refreshToken: string
  accessTokenExpiresAt: string
  user: CurrentUser
}
