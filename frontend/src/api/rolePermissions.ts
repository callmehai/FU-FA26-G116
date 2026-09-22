import { api } from './client'
import type {
  CapabilityCode,
  MatrixMutationResponse,
  RoleCode,
  RolePermissionMatrix,
} from '../types/rolePermission'

/**
 * The role ↔ capability matrix endpoints (SCRUM-60).
 *
 * Both require the `AdminUsers` capability, and the backend re-checks it on every request — the
 * screen hiding a control is not what keeps anyone out.
 */

/** UC-09 — the whole matrix plus the capability catalogue that gives the grid its columns. */
export const getRolePermissionMatrix = () =>
  api.get<RolePermissionMatrix>('/api/roles/capabilities')

/**
 * UC-10 — sets exactly which capabilities a role carries.
 *
 * `capabilities` is the role's complete end state, not a delta: send every capability it should
 * have after the change, including the ones it already had. An empty array is a valid request and
 * strips the role of everything — which is why it must always be sent explicitly.
 *
 * Rejected with MSG17 when it would leave no role holding `AdminUsers`: the system would then
 * have nobody able to reach this screen, and no way back except a database console.
 */
export const updateRolePermissions = (role: RoleCode, capabilities: CapabilityCode[]) =>
  api.put<MatrixMutationResponse>('/api/roles/capabilities', { role, capabilities })
