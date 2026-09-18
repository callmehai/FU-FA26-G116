import { NotFound } from '../../components/NotFound'
import type { Capability } from '../../types/user'
import { useAuth } from './useAuth'
import type { ReactNode } from 'react'

/**
 * Capability gate for a single route.
 *
 * Renders the same 404 as an unknown URL rather than a "forbidden" page: telling someone a screen
 * exists but is off-limits leaks the shape of the system. This is presentation only — the backend
 * still has to reject the underlying request.
 */
export function RequireCapability({ cap, children }: { cap: Capability; children: ReactNode }) {
  const { can } = useAuth()
  return can(cap) ? <>{children}</> : <NotFound />
}
