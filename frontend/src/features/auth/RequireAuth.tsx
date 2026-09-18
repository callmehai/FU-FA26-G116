import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { routes } from '../../lib/routes'
import { useAuth } from './useAuth'

/** Session gate for every screen inside a workspace. Sends signed-out visitors to the login page. */
export function RequireAuth() {
  const { user } = useAuth()
  const location = useLocation()

  // `state.from` lets the login page send the user back to where they were headed.
  if (!user) return <Navigate to={routes.login} replace state={{ from: location }} />

  return <Outlet />
}
