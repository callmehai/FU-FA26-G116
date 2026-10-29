import { routes } from '../lib/routes'
import type { Capability } from '../types/user'
import type { IconName } from './Icon'

export interface NavEntry {
  to: string
  label: string
  icon: IconName
  /** When set, the entry is hidden unless the user holds the capability. */
  cap?: Capability
}

/** The top bar's sections, open to everyone signed in. One's own Voice ID is in the avatar menu. */
export const MAIN_NAV: NavEntry[] = [
  { to: routes.calendar, label: 'Lịch làm việc', icon: 'calendar' },
  { to: routes.meetings, label: 'Cuộc họp', icon: 'list' },
  { to: routes.documents, label: 'Kho tài liệu', icon: 'folder' },
]

/** The Quản trị area: its own column of pages, reached from one entry on the bar. */
export const ADMIN_NAV: NavEntry[] = [
  { to: routes.adminUsers, label: 'Người dùng & Quyền', icon: 'users', cap: 'AdminUsers' },
  { to: routes.adminRoles, label: 'Vai trò', icon: 'shield', cap: 'AdminUsers' },
]

/** True on the entry's page and below it: "Cuộc họp" stays current on a meeting's page. */
export function isCurrent(pathname: string, to: string) {
  return pathname === to || pathname.startsWith(`${to}/`)
}
