import type { Capability } from './user'

/**
 * The role ↔ capability matrix (SCRUM-60, UC-09 and UC-10).
 *
 * These mirror the backend's response shapes exactly. This is the real matrix, read from the
 * database, and an administrator can change it at runtime — do not hardcode a copy of it.
 */

/**
 * One capability, described for a human reader.
 *
 * The labels come from the backend rather than from a table here, so a capability added to the
 * enum cannot reach this screen as a blank column.
 */
export interface PermissionDescriptor {
  code: Capability
  name: string
  description: string
  functionalGroup: string
}

/** One row of the grid. */
export interface RolePermissions {
  id: string
  name: string
  description: string | null
  capabilities: Capability[]
  /** Whether the role is one of the four system defaults. */
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
