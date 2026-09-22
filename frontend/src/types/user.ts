/**
 * Capabilities gate what a user may do. They are deliberately coarse and named after actions
 * rather than job titles, so a role change never means editing every screen.
 *
 * The UI uses them only to decide what to render. Authorization is the backend's job: hiding a
 * button is not access control, and every request must be re-checked server-side.
 */
export const CAPABILITIES = ['meeting_creator', 'meeting_approver', 'minutes_editor', 'admin_users'] as const

export type Capability = (typeof CAPABILITIES)[number]

export type Role = 'chair' | 'secretary' | 'member' | 'admin'

export const ROLE_LABEL: Record<Role, string> = {
  chair: 'Chủ toạ',
  secretary: 'Thư ký',
  member: 'Thành viên',
  admin: 'Quản trị viên',
}

/**
 * Which capabilities each role carries.
 *
 * TODO(SCRUM-46): delete this table and read `capabilities` from `GET /api/auth/me`.
 *
 * It is scaffolding for the mocked `AuthProvider`, and it is **already wrong**: the backend is the
 * single source of truth (`role_capabilities`, editable at runtime via SCRUM-60), it returns
 * capability codes in PascalCase — `AdminUsers`, not `admin_users` — and an administrator can move
 * a capability to another role without this file noticing.
 *
 * Do not add a new capability here. Nothing is gated on it: every endpoint re-checks the
 * capability server-side, so this table only decides what gets rendered.
 *
 * See docs/03-api/04-phan-quyen-phia-server.md.
 */
export const ROLE_CAPABILITIES: Record<Role, Capability[]> = {
  chair: ['meeting_creator', 'meeting_approver'],
  secretary: ['meeting_creator', 'minutes_editor'],
  member: [],
  admin: ['admin_users'],
}

export interface User {
  id: string
  fullName: string
  email: string
  role: Role
  capabilities: Capability[]
}
