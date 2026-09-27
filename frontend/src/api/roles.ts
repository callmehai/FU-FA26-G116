import type {
  CreateRoleRequest,
  DeleteRoleResponse,
  RoleMutationResponse,
  RoleRecord,
  UpdateRoleRequest,
} from '../types/role'
import { api } from './client'

/** Role management (SCRUM-63). The backend rechecks AdminUsers on every request. */
export const listRoles = () => api.get<RoleRecord[]>('/api/roles')

export const createRole = (body: CreateRoleRequest) => api.post<RoleMutationResponse>('/api/roles', body)

export const updateRole = (id: string, body: UpdateRoleRequest) =>
  api.put<RoleMutationResponse>(`/api/roles/${id}`, body)

export const deleteRole = (id: string) => api.delete<DeleteRoleResponse>(`/api/roles/${id}`)
