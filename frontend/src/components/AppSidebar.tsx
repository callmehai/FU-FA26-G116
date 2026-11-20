import { useEffect, useRef, useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { routes } from '../lib/routes'
import type { Capability } from '../types/user'
import { useAuth } from '../features/auth/useAuth'
import { Icon, type IconName } from './Icon'

interface NavEntry {
  to: string
  label: string
  /** The word under the icon in the phone's bottom bar, where a tab is a fifth of 390px. */
  short?: string
  icon: IconName
  /** When set, the entry is hidden unless the user holds the capability. */
  cap?: Capability
}

/**
 * Day-to-day work, open to everyone signed in. What is one's own — the document library, the Voice ID — opens from
 * the account menu at the sidebar's foot instead, as AI-eCabinet's avatar menu does (`AppShell`).
 */
const WORK: NavEntry[] = [
  { to: routes.calendar, label: 'Lịch làm việc', short: 'Lịch', icon: 'calendar' },
  { to: routes.meetings, label: 'Cuộc họp', icon: 'meeting' },
  // SCRUM-137: finding a time reads calendars shared with the caller, so only a Meeting Organizer sees it (BR-430).
  { to: routes.findTime, label: 'Tìm giờ họp', short: 'Tìm giờ', icon: 'calendarSearch', cap: 'MeetingCreator' },
  // SCRUM-38: no capability — the figures count only the meetings the viewer may read (BR-432).
  { to: routes.reports, label: 'Báo cáo', icon: 'chart' },
]

/** Under "Quản trị". The group disappears when none of its entries is visible. */
const ADMIN: NavEntry[] = [
  { to: routes.adminUsers, label: 'Người dùng', icon: 'users', cap: 'AdminUsers' },
  { to: routes.adminRoles, label: 'Vai trò', icon: 'shield', cap: 'AdminUsers' },
  { to: routes.adminAi, label: 'Cấu hình AI', icon: 'sparkles', cap: 'AdminUsers' },
  { to: routes.adminAuditLog, label: 'Nhật ký hệ thống', icon: 'history', cap: 'AdminUsers' },
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
          // Only the calendar itself: calendar sharing lives under /calendar but opens from the account menu.
          end={entry.to === routes.calendar}
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

/**
 * The phone's navigation (ADR-0070): the sidebar's work entries as a bottom tab bar, a fifth of the width each, and
 * "Quản trị" as one more tab whose entries open in a menu above it. Same entries and the same capability filter as
 * the sidebar; the account and the bell sit in the top bar instead (AppShell).
 */
export function PhoneTabBar() {
  const { can } = useAuth()
  const { pathname } = useLocation()
  const admin = ADMIN.filter((e) => !e.cap || can(e.cap))
  const inAdmin = admin.some((e) => pathname.startsWith(e.to))
  const menuRef = useRef<HTMLDetailsElement>(null)

  // Moving to another screen closes the admin menu; so does a tap outside it or Escape.
  useEffect(() => {
    menuRef.current?.removeAttribute('open')
  }, [pathname])
  useEffect(() => {
    function close(event: Event) {
      const menu = menuRef.current
      if (!menu?.open) return
      if (event instanceof KeyboardEvent ? event.key === 'Escape' : !menu.contains(event.target as Node)) {
        menu.removeAttribute('open')
      }
    }
    document.addEventListener('pointerdown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('pointerdown', close)
      document.removeEventListener('keydown', close)
    }
  }, [])

  return (
    <nav className="tabbar" aria-label="Điều hướng chính">
      {WORK.filter((e) => !e.cap || can(e.cap)).map((entry) => (
        <NavLink
          key={entry.to}
          to={entry.to}
          end={entry.to === routes.calendar}
          className="tabbar-item"
          aria-label={entry.label}
        >
          <Icon name={entry.icon} size={22} />
          <span className="tabbar-label" aria-hidden="true">
            {entry.short ?? entry.label}
          </span>
        </NavLink>
      ))}
      {admin.length > 0 && (
        <details className="tabbar-more" ref={menuRef}>
          <summary className={`tabbar-item${inAdmin ? ' active' : ''}`} aria-label="Quản trị">
            <Icon name="settings" size={22} />
            <span className="tabbar-label" aria-hidden="true">
              Quản trị
            </span>
          </summary>
          <div className="account-menu tabbar-menu">
            {admin.map((entry) => (
              <NavLink key={entry.to} to={entry.to} className="menu-item">
                <Icon name={entry.icon} size={16} />
                {entry.label}
              </NavLink>
            ))}
          </div>
        </details>
      )}
    </nav>
  )
}
