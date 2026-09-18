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
 * TEMPORARY: the real mapping belongs in the backend, which will return a user's capabilities at
 * sign-in. This table exists so screens can be built and demoed before that endpoint lands.
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
  /** Organisational unit the user belongs to. */
  unit: string
}
