import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { ROLE_CAPABILITIES, type Capability, type Role, type User } from '../../types/user'
import { AuthContext } from './authContext'

/**
 * Session state for the whole app.
 *
 * TEMPORARY: authentication is mocked in the browser because the backend has no user table, no
 * password hashing and no token issuing yet. `signIn` accepts any credentials and fabricates a
 * user, so screens can be built and reviewed before auth lands.
 *
 * When the real endpoints exist, only this file changes: `signIn` posts to `/api/auth/login`, the
 * returned token is attached by `api/client.ts`, and the localStorage cache below gives way to
 * whatever the token strategy requires.
 */

const STORAGE_KEY = 'trustcabinet.session'

function readCachedUser(): User | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as User) : null
  } catch {
    // Private mode or a corrupted value — treat it as signed out rather than crashing the app.
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  // Read straight into the initial state: the cache is synchronous, so there is no restoring
  // phase for guards to wait on and no first paint showing a signed-in user as signed out.
  const [user, setUser] = useState<User | null>(readCachedUser)

  const signIn = useCallback((email: string, role: Role) => {
    const localPart = email.split('@')[0] || 'nguoi.dung'

    const next: User = {
      id: crypto.randomUUID(),
      fullName: localPart.replace(/[._]/g, ' '),
      email,
      role,
      capabilities: ROLE_CAPABILITIES[role],
      unit: 'Chưa gán đơn vị',
    }

    setUser(next)
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    } catch {
      // The session simply will not survive a reload; not worth blocking sign-in over.
    }
  }, [])

  const signOut = useCallback(() => {
    setUser(null)
    try {
      localStorage.removeItem(STORAGE_KEY)
    } catch {
      // Nothing to clean up when storage is unavailable.
    }
  }, [])

  const can = useCallback(
    (capability: Capability) => user?.capabilities.includes(capability) ?? false,
    [user],
  )

  const value = useMemo(() => ({ user, can, signIn, signOut }), [user, can, signIn, signOut])

  return <AuthContext value={value}>{children}</AuthContext>
}
