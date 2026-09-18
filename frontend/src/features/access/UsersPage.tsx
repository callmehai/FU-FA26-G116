import { PageHeader } from '../../components/PageHeader'

export function UsersPage() {
  return (
    <>
      <PageHeader
        title="Người dùng & Quyền"
        description="Quản lý tài khoản và phân quyền hệ thống."
        action={
          <button className="btn btn-primary" type="button" disabled>
            Tạo tài khoản
          </button>
        }
      />

      <div className="card card-pending">
        <p>
          Màn hình chưa có nghiệp vụ. Sẽ hiện thực khi backend có <code>GET /api/users</code> và{' '}
          <code>POST /api/users</code>.
        </p>
      </div>
    </>
  )
}
