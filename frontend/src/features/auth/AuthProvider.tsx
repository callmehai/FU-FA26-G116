import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import * as authApi from '../../api/auth'
import type { Capability, CurrentUser } from '../../types/user'
import { AuthContext, type AuthStatus } from './authContext'
import * as session from './session'

interface SessionState {
  status: AuthStatus
  user: CurrentUser | null
  sessionExpired: boolean
}

/**
 * Session state for the whole app (SCRUM-11).
 *
 * The user — and with it every capability the UI gates on — always comes from the backend: from
 * the sign-in response, or from a refresh on reload. Tokens are handled by `session.ts`; this
 * component mirrors what it reports into React.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<SessionState>({ status: 'loading', user: null, sessionExpired: false })

  useEffect(() => {
    const unsubscribe = session.subscribe((user, expired) =>
      setState({ status: user ? 'authenticated' : 'anonymous', user, sessionExpired: expired }),
    )

    void session.restoreSession().then((user) => {
      // A successful restore has already been reported through the subscription.
      if (!user) setState((s) => (s.status === 'loading' ? { ...s, status: 'anonymous' } : s))
    })

    return unsubscribe
  }, [])

  // Coming back to the tab: re-read capabilities, so a menu an administrator took away in the
  // meantime disappears before the user clicks it and meets a 403.
  const authenticated = state.status === 'authenticated'
  useEffect(() => {
    if (!authenticated) return
    const onVisible = () => {
      if (document.visibilityState === 'visible') void session.reloadSession()
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [authenticated])

  const signIn = useCallback(async (email: string, password: string) => {
    session.startSession(await authApi.signIn(email, password))
  }, [])

  const signInWithGoogle = useCallback(async (idToken: string) => {
    session.startSession(await authApi.signInWithGoogle(idToken))
  }, [])

  const signOut = useCallback(() => session.signOut(), [])

  const reloadSession = useCallback(async () => {
    await session.reloadSession()
  }, [])

  const can = useCallback(
    (capability: Capability) => state.user?.capabilities.includes(capability) ?? false,
    [state.user],
  )

  const value = useMemo(
    () => ({ ...state, can, signIn, signInWithGoogle, signOut, reloadSession }),
    [state, can, signIn, signInWithGoogle, signOut, reloadSession],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
