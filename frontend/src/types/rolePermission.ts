/**
 * The role ↔ capability matrix (SCRUM-60, UC-09 and UC-10).
 *
 * These mirror the backend's response shapes exactly, so the codes here are the backend's
 * PascalCase enum names — `AdminUsers`, not the `admin_users` used by the temporary
 * `ROLE_CAPABILITIES` table in `types/user.ts`. That table is scaffolding that goes away with
 * SCRUM-46; this is the real matrix, read from the database, and an administrator can change it
 * at runtime. Do not hardcode a copy of it.
 */

/** A role's PascalCase name, as the backend spells it. */
export type RoleCode = 'Chair' | 'Secretary' | 'Member' | 'Admin'

/** A capability's PascalCase name, as the backend spells it. */
export type CapabilityCode = 'MeetingCreator' | 'MeetingApprover' | 'MinutesEditor' | 'AdminUsers'

/**
 * One capability, described for a human reader.
 *
 * The labels come from the backend rather than from a table here, so a capability added to the
 * enum cannot reach this screen as a blank column.
 */
export interface PermissionDescriptor {
  code: CapabilityCode
  name: string
  description: string
  functionalGroup: string
}

/** One row of the grid. */
export interface RolePermissions {
  role: RoleCode
  capabilities: CapabilityCode[]
  /** Whether the role is part of the system's definition. All four are, today. */
  isBuiltIn: boolean
}

/** UC-09's answer: the columns and the rows of the grid. */
export interface RolePermissionMatrix {
  capabilities: PermissionDescriptor[]
  roles: RolePermissions[]
}

/** What a successful UC-10 returns — MSG16, plus the matrix as it now stands. */
export interface MatrixMutationResponse {
  code: string
  message: string
  matrix: RolePermissionMatrix
}

/**
 * A refused configuration. `errors` is field name → message for MSG17, and absent for MSG18,
 * where nothing about the request was wrong and retrying is the right advice.
 */
export interface MatrixErrorResponse {
  code: string
  message: string
  errors?: Record<string, string>
}
