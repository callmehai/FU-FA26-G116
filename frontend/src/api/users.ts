import type {
  CreateUserRequest,
  UpdateUserRequest,
  UserAccount,
  UserMutationResponse,
} from '../types/user'
import { api } from './client'

/**
 * User management (SCRUM-13, UC-04 … UC-08). Every endpoint needs `AdminUsers`, re-checked
 * against the database on each request — a 403 mid-session means the caller lost it.
 */

/** UC-04 — every account, deactivated ones included, already sorted by the database's Vietnamese collation. */
export const listUsers = () => api.get<UserAccount[]>('/api/users')

/** UC-06, first half — the account to fill the edit form with. */
export const getUser = (id: string) => api.get<UserAccount>(`/api/users/${id}`)

/** UC-05 — 201 with MSG07. The account starts active. */
export const createUser = (body: CreateUserRequest) => api.post<UserMutationResponse>('/api/users', body)

/** UC-06, second half — MSG10. Changing the role signs that person out everywhere. */
export const updateUser = (id: string, body: UpdateUserRequest) =>
  api.put<UserMutationResponse>(`/api/users/${id}`, body)

/**
 * UC-07 — MSG11 both ways. `isActive` is always sent explicitly: the backend refuses a body
 * without it rather than reading a missing field as "deactivate".
 */
export const changeUserStatus = (id: string, isActive: boolean) =>
  api.put<UserMutationResponse>(`/api/users/${id}/status`, { isActive })

/**
 * UC-08 — MSG13. Emails the account a one-time link, valid 60 minutes, that replaces any earlier
 * one; the current password keeps working until the link is used. Sent synchronously over SMTP, so
 * it can take as long as the backend's send timeout. 409 MSG14 for a deactivated account, 503 MSG15
 * when the mail server would not take it — an earlier link then still works.
 */
export const requestPasswordReset = (id: string) =>
  api.post<UserMutationResponse>(`/api/users/${id}/password-reset`)
