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
  // Public: opened from the UC-08 email; the token travels in the fragment, never the query.
  resetPassword: '/reset-password',

  mgmt: '/mgmt',
  meetings: '/mgmt/meetings',
  meetingNew: '/mgmt/meetings/new',
  meeting: (id: string) => `/mgmt/meetings/${id}`,
  meetingEdit: (id: string) => `/mgmt/meetings/${id}/edit`,
  adminUsers: '/mgmt/admin/users',
  adminRoles: '/mgmt/admin/roles',
  // ADR-0006: each user's own document library.
  documents: '/mgmt/documents',
  calendar: '/calendar',
} as const
