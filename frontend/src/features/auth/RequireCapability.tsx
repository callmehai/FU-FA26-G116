import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { NotFound } from '../../components/NotFound'
import { routes } from '../../lib/routes'
import type { Capability } from '../../types/user'
import { useAuth } from './useAuth'

/** Location-state key the login page sets on its redirect back to the page the user came from. */
export const JUST_SIGNED_IN = 'justSignedIn'

/**
 * Capability gate for a single route.
 *
 * Renders the same 404 as an unknown URL rather than a "forbidden" page: telling someone a screen
 * exists but is off-limits leaks the shape of the system. The one exception is the redirect right
 * after sign-in, see below. This is presentation only — the backend still has to reject the
 * underlying request.
 */
export function RequireCapability({ cap, children }: { cap: Capability; children: ReactNode }) {
  const { can } = useAuth()
  const location = useLocation()
  if (can(cap)) return <>{children}</>
  // Right after sign-in the page came from whoever was signed in before — another account, perhaps
  // with more capabilities. Landing that user on a 404 only confuses them (SCRUM-66).
  const justSignedIn = (location.state as Record<string, unknown> | null)?.[JUST_SIGNED_IN] === true
  return justSignedIn ? <Navigate to={routes.meetings} replace /> : <NotFound />
}
