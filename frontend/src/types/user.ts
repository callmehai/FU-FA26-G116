/**
 * Roles and capabilities, spelled exactly as the backend spells them: PascalCase enum names.
 *
 * Capabilities gate what a user may do. The UI uses them only to decide what to render —
 * authorization is the backend's job, and every request is re-checked server-side. Which role
 * carries which capability is not known here: it lives in the `role_capabilities` table, an
 * administrator can change it at runtime, and the backend hands each user their own list at
 * sign-in. Never keep a copy of that mapping in the frontend.
 */
export type Role = 'Chair' | 'Secretary' | 'Member' | 'Admin'

export type Capability = 'MeetingCreator' | 'MeetingApprover' | 'MinutesEditor' | 'AdminUsers'

/** Vietnamese names for the four built-in roles. The backend returns only the enum name. */
export const ROLE_LABEL: Record<Role, string> = {
  Chair: 'Chủ toạ',
  Secretary: 'Thư ký',
  Member: 'Thành viên',
  Admin: 'Quản trị viên',
}

export const ROLES = Object.keys(ROLE_LABEL) as Role[]

/** The signed-in user — GET /api/auth/me, and the `user` of every sign-in response. */
export interface CurrentUser {
  id: string
  email: string
  fullName: string
  role: Role
  /** May be empty: a Member signs in normally without holding any capability. */
  capabilities: Capability[]
}

/**
 * One account as the user-management screens see it (SCRUM-13).
 *
 * `hasPassword` / `hasGoogleLink` only say whether each sign-in method exists; the backend never
 * sends the hash or the Google subject.
 */
export interface UserAccount {
  id: string
  fullName: string
  email: string
  role: Role
  isActive: boolean
  hasPassword: boolean
  hasGoogleLink: boolean
  createdAt: string
}

/** What POST /api/users (MSG07), PUT /api/users/{id} (MSG10) and PUT …/status (MSG11) return. */
export interface UserMutationResponse {
  code: string
  message: string
  /** The account after the change — patch it into the list instead of fetching again. */
  user: UserAccount
}

export interface CreateUserRequest {
  fullName: string
  email: string
  password: string
  role: Role
}

/** No `password` and no `isActive`: the backend ignores both here, on purpose. */
export interface UpdateUserRequest {
  fullName: string
  email: string
  role: Role
}
