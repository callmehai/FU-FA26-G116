import { useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useToast } from '../../components/toastContext'
import { isApiError } from '../../lib/errors'
import { routes } from '../../lib/routes'
import { useAuth } from './useAuth'

/**
 * Handles a 403 from an admin endpoint in the middle of a session.
 *
 * The admin endpoints re-read the caller's account on every request, so being deactivated,
 * demoted, or having `AdminUsers` moved off one's role turns the very next request into a 403 —
 * no waiting for the token to expire. That is expected, not a bug.
 *
 * Retrying would get the same answer, and bouncing to /login loops when the account can still sign
 * in but no longer administer. So: leave the screen, and reload the session so the sidebar drops
 * what the user can no longer open. If the session itself is gone, that reload signs them out.
 *
 * Returns whether the error was a 403 and has been handled.
 */
export function useAccessLost() {
  const navigate = useNavigate()
  const toast = useToast()
  const { reloadSession } = useAuth()

  return useCallback(
    (error: unknown) => {
      if (!isApiError(error, 403)) return false
      toast.show('Quyền truy cập của bạn đã thay đổi, không thể tiếp tục thao tác này.', 'error')
      navigate(routes.meetings, { replace: true })
      void reloadSession()
      return true
    },
    [navigate, toast, reloadSession],
  )
}
