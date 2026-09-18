import { Link } from 'react-router-dom'
import { routes } from '../lib/routes'

/**
 * Rendered both for unknown URLs and for screens the signed-in user lacks the capability for,
 * so a restricted page never reveals that it exists.
 */
export function NotFound() {
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
