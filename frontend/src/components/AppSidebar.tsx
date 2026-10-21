import { useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
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

/** Day-to-day work, open to everyone signed in. */
const WORK: NavEntry[] = [
  { to: routes.calendar, label: 'Lịch làm việc', icon: 'calendar' },
  { to: routes.meetings, label: 'Cuộc họp', icon: 'meeting' },
  { to: routes.documents, label: 'Kho tài liệu', icon: 'folder' },
]

/** Under "Quản trị". The group disappears when none of its entries is visible. */
const ADMIN: NavEntry[] = [
  { to: routes.adminUsers, label: 'Người dùng & Quyền', icon: 'users', cap: 'AdminUsers' },
  { to: routes.adminRoles, label: 'Vai trò', icon: 'shield', cap: 'AdminUsers' },
]

/**
 * The sidebar's sections (ADR-0012). Hiding an entry is presentation only — every route and
 * endpoint behind it re-checks the capability.
 *
 * Collapsed, the sidebar is 72px of icons: group headings go, and "Quản trị" gives way to its own
 * entries so each one stays one click away.
 */
export function AppSidebar({ collapsed }: { collapsed: boolean }) {
  const { can } = useAuth()
  const { pathname } = useLocation()
  const admin = ADMIN.filter((e) => !e.cap || can(e.cap))
  const inAdmin = admin.some((e) => pathname.startsWith(e.to))
  // Follows the route (open on an admin screen) until the user opens or closes it themselves.
  const [adminChoice, setAdminChoice] = useState<boolean | null>(null)
  const adminOpen = adminChoice ?? inAdmin

  return (
    <nav className="sidebar-nav" aria-label="Điều hướng chính">
      <p className="sidebar-group sidebar-label">Nghiệp vụ</p>
      {WORK.filter((e) => !e.cap || can(e.cap)).map((entry) => (
        <NavLink
          key={entry.to}
          to={entry.to}
          className="sidebar-item"
          data-tooltip={collapsed ? entry.label : undefined}
        >
          <Icon name={entry.icon} size={20} />
          <span className="sidebar-label">{entry.label}</span>
        </NavLink>
      ))}

      {admin.length > 0 && (
        <>
          <p className="sidebar-group sidebar-label">Hệ thống</p>
          {collapsed ? (
            admin.map((entry) => (
              <NavLink key={entry.to} to={entry.to} className="sidebar-item" data-tooltip={entry.label}>
                <Icon name={entry.icon} size={20} />
                <span className="sidebar-label">{entry.label}</span>
              </NavLink>
            ))
          ) : (
            <>
              <button
                type="button"
                className={`sidebar-item${inAdmin && !adminOpen ? ' active' : ''}`}
                aria-expanded={adminOpen}
                aria-controls="sidebar-admin"
                onClick={() => setAdminChoice(!adminOpen)}
              >
                <Icon name="settings" size={20} />
                <span className="sidebar-label sidebar-item-grow">Quản trị</span>
                <span className={`sidebar-label sidebar-caret${adminOpen ? ' sidebar-caret-open' : ''}`}>
                  <Icon name="chevronDown" size={16} />
                </span>
              </button>
              {adminOpen && (
                <div className="sidebar-sub" id="sidebar-admin">
                  {admin.map((entry) => (
                    <NavLink key={entry.to} to={entry.to} className="sidebar-subitem">
                      {entry.label}
                    </NavLink>
                  ))}
                </div>
              )}
            </>
          )}
        </>
      )}
    </nav>
  )
}
