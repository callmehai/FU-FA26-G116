import { Link, Navigate, useLocation } from 'react-router-dom'
import { JUST_SIGNED_IN, routes } from '../lib/routes'

/**
 * Rendered both for unknown URLs and for screens the signed-in user lacks the capability for,
 * so a restricted page never reveals that it exists.
 *
 * Right after sign-in it sends the user to the meeting list instead: the page they were returned to
 * came from whoever was signed in before (SCRUM-66). Every cause — unknown URL, missing capability,
 * unreadable meeting — is treated alike, so the redirect reveals no more than the 404 does.
 */
export function NotFound() {
  const location = useLocation()
  if ((location.state as Record<string, unknown> | null)?.[JUST_SIGNED_IN] === true) {
    return <Navigate to={routes.meetings} replace />
  }
  return (
    <div className="empty-state">
      <h1>Không tìm thấy trang</h1>
      <p>Đường dẫn không tồn tại, hoặc bạn không có quyền xem nội dung này.</p>
      <Link className="btn btn-primary" to={routes.meetings}>
        Về danh sách cuộc họp
      </Link>
    </div>
  )
}
