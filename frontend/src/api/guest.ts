import type { GuestCodeSent, GuestInvitation, GuestProfileSaved, GuestSession, GuestVerified } from '../types/guest'
import { api } from './client'

/**
 * Guest access by invitation (UC-04, SCRUM-116). Contract in docs/03-api/16-truy-cap-khach-uc04.md.
 *
 * The first three are anonymous: the proof is the link token from the email's fragment, sent in the
 * body. Every way a link can fail answers the same 404 MSG371, never the reason. The session calls take
 * the guest token explicitly, so they never depend on which session the page happens to be using.
 */

const invitation = (id: string) => `/api/guest-access/invitations/${id}`
const bearer = (token: string) => ({ anonymous: true, headers: { Authorization: `Bearer ${token}` } })

export const openInvitation = (id: string, token: string) =>
  api.post<GuestInvitation>(invitation(id), { token }, { anonymous: true })

/** 200 MSG375; 429 MSG373 / MSG379 carry `retryAfterSeconds`; 503 MSG374. */
export const sendGuestCode = (id: string, token: string) =>
  api.post<GuestCodeSent>(`${invitation(id)}/code`, { token }, { anonymous: true })

/** 400 MSG126 (wrong, expired or used code) or MSG372 (locked after 5 misses); 404 MSG371; 503 MSG374. */
export const verifyGuestCode = (id: string, token: string, code: string) =>
  api.post<GuestVerified>(`${invitation(id)}/verify`, { token, code }, { anonymous: true })

/** 404 MSG378 once the session, the invitation or the meeting is over. */
export const getGuestSession = (accessToken: string) =>
  api.get<GuestSession>('/api/guest-access/session', bearer(accessToken))

/** 400 MSG377 with `errors.displayName` / `errors.jobTitle`. */
export const saveGuestProfile = (accessToken: string, displayName: string, jobTitle: string) =>
  api.put<GuestProfileSaved>('/api/guest-access/session/profile', { displayName, jobTitle }, bearer(accessToken))

/** Ends this session only; 204 again when repeated. */
export const endGuestSession = (accessToken: string) =>
  api.delete<void>('/api/guest-access/session', bearer(accessToken))
