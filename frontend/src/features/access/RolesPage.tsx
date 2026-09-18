import { PageHeader } from '../../components/PageHeader'
import { CAPABILITIES, ROLE_CAPABILITIES, ROLE_LABEL, type Role } from '../../types/user'

const ROLES = Object.keys(ROLE_LABEL) as Role[]

const CAPABILITY_LABEL: Record<(typeof CAPABILITIES)[number], string> = {
  meeting_creator: 'Tạo cuộc họp',
  meeting_approver: 'Duyệt cuộc họp',
  minutes_editor: 'Soạn biên bản',
  admin_users: 'Quản trị người dùng',
}

/**
 * Read-only view of the role → capability matrix.
 *
 * It renders the same table the frontend actually gates on, which makes the mock RBAC inspectable:
 * whoever is testing can see exactly why a menu item appeared or did not.
 */
export function RolesPage() {
  return (
    <>
      <PageHeader title="Vai trò" description="Mỗi vai trò mang theo một tập quyền cố định." />

      <div className="card table-card">
        <table className="data-table">
          <thead>
            <tr>
              <th>Vai trò</th>
              {CAPABILITIES.map((c) => (
                <th key={c}>{CAPABILITY_LABEL[c]}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {ROLES.map((role) => (
              <tr key={role}>
                <td className="cell-title">{ROLE_LABEL[role]}</td>
                {CAPABILITIES.map((cap) => (
                  <td key={cap}>{ROLE_CAPABILITIES[role].includes(cap) ? '✓' : '—'}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="mock-note">
        Bảng này đang đọc từ <code>types/user.ts</code> ở phía trình duyệt. Khi backend có phân
        quyền thật, nó phải là nguồn duy nhất — ẩn menu không phải là kiểm soát truy cập.
      </p>
    </>
  )
}
