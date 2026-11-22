import { useState } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { routes } from '../../lib/routes'
import { DocumentOverlayProvider } from '../documents/DocumentOverlay'
import { NotificationsProvider } from '../notifications/NotificationsProvider'
import { RoomMicProvider } from '../recordings/RoomMicProvider'
import { UploadsProvider } from '../uploads/UploadsProvider'
import { useAuth } from './useAuth'

/** Session gate for every screen inside a workspace. Sends signed-out visitors to the login page. */
export function RequireAuth() {
  const { status, user, sessionExpired } = useAuth()
  const location = useLocation()
  const [wasSignedIn, setWasSignedIn] = useState(false)
  if (status === 'authenticated' && !wasSignedIn) setWasSignedIn(true)

  // Still trading the stored refresh token for a session — redirecting now would send every
  // reload through /login and back.
  if (status === 'loading') return <div className="route-fallback">Đang khôi phục phiên đăng nhập…</div>

  // `state.from` lets the login page send the user back to where they were headed: a link opened while
  // signed out, or the page a session expired on. A sign-out (here or in another tab) leaves no
  // destination, so the next sign-in lands on the calendar.
  if (status === 'anonymous') {
    const keep = !wasSignedIn || sessionExpired
    return <Navigate to={routes.login} replace state={keep ? { from: location } : undefined} />
  }

  // One notification connection for everything a signed-in user sees. Keyed by account, so another
  // account signing in from a second tab never inherits the previous one's list. The upload queue lives
  // here too, so a page change never stops a file on its way, and so does the Secretary's room mic, so going
  // between a meeting's page and its call room never stops it (ADR-0072).
  return (
    <NotificationsProvider key={user?.id}>
      <UploadsProvider>
        {/* A document opens over whichever page it was picked from, the call room included. */}
        <DocumentOverlayProvider>
          <RoomMicProvider>
            <Outlet />
          </RoomMicProvider>
        </DocumentOverlayProvider>
      </UploadsProvider>
    </NotificationsProvider>
  )
}
