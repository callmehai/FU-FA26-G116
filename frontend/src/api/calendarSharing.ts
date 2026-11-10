import type {
  ShareCandidatePage,
  ShareLevel,
  ShareLists,
  ShareResult,
  ShareValues,
  SharedWeek,
} from '../types/calendarSharing'
import { api } from './client'

/** SCRUM-142 — docs/03-api/23-chia-se-lich.md. Every call answers 404 with no body for a share of other people. */
const BASE = '/api/calendar'

/** UC-84f — both lists, newest first, shares past their end date left out. 500 MSG453. */
export const listShares = () => api.get<ShareLists>(`${BASE}/shares`)

/**
 * The people to pick from: `request` (UC-84a, needs the right to create meetings, 403 MSG448) lists every active
 * account but the caller; `direct` (UC-84b) lists those who may receive a share. 500 MSG467.
 */
export const listShareCandidates = (purpose: 'request' | 'direct', q: string, page = 1, pageSize = 20) => {
  const params = new URLSearchParams({ purpose, page: String(page), pageSize: String(pageSize) })
  if (q.trim()) params.set('q', q.trim())
  return api.get<ShareCandidatePage>(`${BASE}/share-candidates?${params}`)
}

/** UC-84a — 201 MSG435; 403 MSG448, 409 MSG449, 400 MSG443. */
export const requestShare = (body: { ownerId: string; level: ShareLevel; startDate: string; endDate: string }) =>
  api.post<ShareResult>(`${BASE}/shares`, body)

/** UC-84b — always Level1. 201 MSG441; 400 MSG455 (cannot receive), 409 MSG449, 400 MSG443. */
export const shareDirectly = (body: { viewerId: string; startDate: string; endDate: string }) =>
  api.post<ShareResult>(`${BASE}/shares/direct`, body)

/** UC-84c — 200 MSG436 (share) or MSG442 (pending change); 409 MSG450 / MSG468, 400 MSG443. */
export const acceptShare = (id: string, values?: ShareValues) =>
  api.post<ShareResult>(`${BASE}/shares/${id}/accept`, values ?? {})

/** UC-84c — 200 MSG437 (share) or MSG452 (pending change). */
export const declineShare = (id: string) => api.post<ShareResult>(`${BASE}/shares/${id}/decline`)

/** UC-84d — the viewer asks (201 MSG454), the owner changes at once (200 MSG442). 400 MSG443, 409 MSG450. */
export const changeShare = (id: string, values: ShareValues) => api.patch<ShareResult>(`${BASE}/shares/${id}`, values)

/** UC-84e — the sender withdraws a Pending share or change. 200 MSG438. */
export const withdrawShare = (id: string) => api.post<ShareResult>(`${BASE}/shares/${id}/withdraw`)

/** UC-84e AF-1 — either side ends an Effective share. 200 MSG451. */
export const endShare = (id: string) => api.post<ShareResult>(`${BASE}/shares/${id}/end`)

/** UC-85c — busy hours of the week holding `week` (any day of it). 403 MSG448, 500 MSG453. */
export const getSharedWeek = (week: string) => api.get<SharedWeek>(`${BASE}/shared-week?week=${week}`)
