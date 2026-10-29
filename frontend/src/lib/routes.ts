/**
 * Central route map. Every in-app path is built from here, so restructuring the URL space is one
 * edit instead of a grep across the codebase.
 *
 * Every signed-in screen sits in one shell with one sidebar (ADR-0012). The /mgmt and /calendar
 * prefixes are kept from the earlier two-workspace layout so existing links keep working.
 * These are navigation targets only — the backend must re-authorize every request, because a
 * hidden menu item is not an access control.
 */
export const routes = {
  login: '/login',
  // Public: opened from the UC-07 email; the token travels in the fragment, never the query.
  resetPassword: '/reset-password',

  mgmt: '/mgmt',
  meetings: '/mgmt/meetings',
  meetingNew: '/mgmt/meetings/new',
  meeting: (id: string) => `/mgmt/meetings/${id}`,
  meetingEdit: (id: string) => `/mgmt/meetings/${id}/edit`,
  /** Full-screen call room (SCRUM-41): outside the app shell, so the call owns the whole tab. */
  meetingRoom: (id: string) => `/mgmt/meetings/${id}/room`,
  /** Document viewer (SCRUM-21): full screen as well, and what the call room opens in a new tab. */
  meetingDocument: (id: string, documentId: string) => `/mgmt/meetings/${id}/documents/${documentId}`,
  adminUsers: '/mgmt/admin/users',
  adminRoles: '/mgmt/admin/roles',
  /** SCRUM-113: a deactivated person's library, read-only, for the administrator who recovers it. */
  userLibrary: (userId: string, folderId: string | null = null) =>
    `/mgmt/admin/users/${userId}/library${folderId ? `?folder=${folderId}` : ''}`,
  userLibraryDocument: (userId: string, id: string, folderId: string | null = null) =>
    `/mgmt/admin/users/${userId}/library/${id}${folderId ? `?folder=${folderId}` : ''}`,
  // ADR-0006: each user's own document library.
  documents: '/mgmt/documents',
  /** A document of one's library, full screen; `folder` is where "‹ Kho tài liệu" returns and where it is listed. */
  libraryDocument: (id: string, folderId: string | null = null) =>
    `/mgmt/documents/${id}${folderId ? `?folder=${folderId}` : ''}`,
  // SCRUM-35: the signed-in person's own Voice ID.
  myVoice: '/mgmt/my-voice',
  calendar: '/calendar',
} as const

/**
 * Location-state key the login page sets on its redirect to the page the user came from. That page
 * may have belonged to the account signed in before, so NotFound sends this one to the meeting list
 * rather than showing a 404 (SCRUM-66).
 */
export const JUST_SIGNED_IN = 'justSignedIn'
