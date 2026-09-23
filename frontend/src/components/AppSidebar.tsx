import { NavLink } from 'react-router-dom'
import { routes } from '../lib/routes'
import type { Capability } from '../types/user'
import { useAuth } from '../features/auth/useAuth'
import { Icon, type IconName } from './Icon'

interface NavEntry {
  to: string
  label: string
  icon: IconName
  /** When set, the entry is hidden unless the user holds the capability. */
  cap?: Capability
}

const ENTRIES: NavEntry[] = [
  { to: routes.meetings, label: 'Cuộc họp', icon: 'calendar' },
  { to: routes.adminUsers, label: 'Người dùng & Quyền', icon: 'users', cap: 'AdminUsers' },
  { to: routes.adminRoles, label: 'Vai trò', icon: 'shield', cap: 'AdminUsers' },
]

/** Navigation for the management workspace. */
export function AppSidebar() {
  const { can } = useAuth()

  return (
    <nav className="sidebar-nav">
      {ENTRIES.filter((e) => !e.cap || can(e.cap)).map((entry) => (
        <NavLink key={entry.to} to={entry.to}>
          <Icon name={entry.icon} size={16} />
          <span>{entry.label}</span>
        </NavLink>
      ))}
    </nav>
  )
}
