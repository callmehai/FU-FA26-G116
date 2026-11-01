import { Link, Navigate, useLocation } from 'react-router-dom'
import { useGuest } from '../features/guest/guestContext'
import { JUST_SIGNED_IN, routes } from '../lib/routes'

/**
 * Rendered both for unknown URLs and for screens the signed-in user lacks the capability for,
 * so a restricted page never reveals that it exists.
 *
 * Right after sign-in it sends the user to the calendar, the home page, instead: the page they were returned to
 * came from whoever was signed in before (SCRUM-66). Every cause — unknown URL, missing capability,
 * unreadable meeting — is treated alike, so the redirect reveals no more than the 404 does.
 * On a guest page (UC-04) the way back is the guest's own meeting: a guest has no internal screen (BR-156).
 */
export function NotFound() {
  const location = useLocation()
  const guest = useGuest()
  if ((location.state as Record<string, unknown> | null)?.[JUST_SIGNED_IN] === true) {
    return <Navigate to={routes.calendar} replace />
  }
  return (
    <div className="empty-state">
      <h1>Không tìm thấy trang</h1>
      <p>Đường dẫn không tồn tại, hoặc bạn không có quyền xem nội dung này.</p>
      <Link className="btn btn-primary" to={guest ? routes.guest(guest.invitationId) : routes.meetings}>
        {guest ? 'Về cuộc họp' : 'Về danh sách cuộc họp'}
      </Link>
    </div>
  )
}
