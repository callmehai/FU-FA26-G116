/**
 * Capabilities match the backend enum names. Role names and IDs come from role records.
 *
 * Capabilities gate what a user may do. The UI uses them only to decide what to render —
 * authorization is the backend's job, and every request is re-checked server-side. Roles are
 * database records; the backend returns their IDs and display names. Which role carries which
 * capability lives in `role_capabilities`, and must never be hardcoded here.
 */
export type Capability = 'MeetingCreator' | 'MeetingApprover' | 'MinutesEditor' | 'AdminUsers'

/** The signed-in user — GET /api/auth/me, and the `user` of every sign-in response. */
export interface CurrentUser {
  id: string
  email: string
  fullName: string
  roleId: string
  roleName: string
  /** May be empty: users with no granted capabilities may still sign in. */
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
  roleId: string
  roleName: string
  isActive: boolean
  hasPassword: boolean
  hasGoogleLink: boolean
  createdAt: string
}

/**
 * What POST /api/users (MSG07), PUT /api/users/{id} (MSG10), PUT …/status (MSG11) and
 * POST …/password-reset (MSG13) return.
 */
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
  roleId: string
}

/** No `password` and no `isActive`: the backend ignores both here, on purpose. */
export interface UpdateUserRequest {
  fullName: string
  email: string
  roleId: string
}
