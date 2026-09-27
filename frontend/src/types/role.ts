import type { Capability } from './user'

/** A role as returned by GET /api/roles. */
export interface RoleRecord {
  id: string
  name: string
  description: string | null
  isBuiltIn: boolean
  isDefault: boolean
  userCount: number
}

export interface CreateRoleRequest {
  name: string
  description: string | null
  capabilities: Capability[]
}

export interface UpdateRoleRequest {
  name: string
  description: string | null
}

export interface RoleMutationResponse {
  code: string
  message: string
  role: RoleRecord
}

export interface DeleteRoleResponse {
  code: string
  message: string
}
