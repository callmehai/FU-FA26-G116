/**
 * Central route map. Every in-app path is built from here, so restructuring the URL space is one
 * edit instead of a grep across the codebase.
 *
 * Two workspaces share one shell: management under /mgmt and the calendar under /calendar.
 * These are navigation targets only — the backend must re-authorize every request, because a
 * hidden menu item is not an access control.
 */
export const routes = {
  login: '/login',

  // Management workspace.
  mgmt: '/mgmt',
  meetings: '/mgmt/meetings',
  meetingNew: '/mgmt/meetings/new',
  meeting: (id: string) => `/mgmt/meetings/${id}`,
  adminUsers: '/mgmt/admin/users',
  adminRoles: '/mgmt/admin/roles',

  // Calendar workspace.
  calendar: '/calendar',
} as const
