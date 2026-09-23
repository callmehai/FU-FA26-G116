import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { routes } from '../../lib/routes'
import { useAuth } from './useAuth'

/** Session gate for every screen inside a workspace. Sends signed-out visitors to the login page. */
export function RequireAuth() {
  const { status } = useAuth()
  const location = useLocation()

  // Still trading the stored refresh token for a session — redirecting now would send every
  // reload through /login and back.
  if (status === 'loading') return <div className="route-fallback">Đang khôi phục phiên đăng nhập…</div>

  // `state.from` lets the login page send the user back to where they were headed.
  if (status === 'anonymous') return <Navigate to={routes.login} replace state={{ from: location }} />

  return <Outlet />
}
