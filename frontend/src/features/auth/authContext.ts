import { createContext } from 'react'
import type { Capability, CurrentUser } from '../../types/user'

/**
 * `loading` is the moment after a reload while the stored refresh token is traded for a session.
 * Guards must wait it out: treating it as signed out would bounce every reload through /login.
 */
export type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  user: CurrentUser | null
  /** True when the backend ended the session (expired, revoked) rather than the user signing out. */
  sessionExpired: boolean
  /** UI-only check. The backend re-authorizes every request regardless of what this returns. */
  can: (capability: Capability) => boolean
  /** UC-01. Rejects with an `ApiError` carrying the backend's message code. */
  signIn: (email: string, password: string) => Promise<void>
  /** UC-02. `idToken` is the credential from Google Identity Services. */
  signInWithGoogle: (idToken: string) => Promise<void>
  /** UC-03. Resolves false when the local token could not be cleared. */
  signOut: () => Promise<boolean>
  /** Re-reads the user's capabilities from the backend, signing out if the session is gone. */
  reloadSession: () => Promise<void>
}

/** Kept apart from the provider so the module exports only a context — fast refresh needs that. */
export const AuthContext = createContext<AuthContextValue | null>(null)
