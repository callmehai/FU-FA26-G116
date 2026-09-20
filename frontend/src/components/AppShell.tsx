import { useState, type ReactNode } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { routes } from '../lib/routes'
import { ROLE_LABEL } from '../types/user'
import { useAuth } from '../features/auth/useAuth'
import { BrandMark } from './BrandMark'
import { HealthBadge } from './HealthBadge'
import { Icon } from './Icon'

/**
 * Chrome shared by every signed-in screen: a collapsible sidebar, a header carrying the workspace
 * switcher and account menu, and the routed content region.
 *
 * `sidebar` differs per workspace — management lists its sections, the calendar shows date
 * navigation — while everything else stays identical, so switching workspaces never redraws the
 * frame. `wide` lets the calendar use the full content width; management keeps a reading measure.
 */
export function AppShell({ sidebar, wide = false }: { sidebar: ReactNode; wide?: boolean }) {
  const { user, signOut } = useAuth()
  const [collapsed, setCollapsed] = useState(false)

  if (!user) return null

  const initial = user.fullName.trim().charAt(0).toUpperCase() || '?'

  return (
    <div className={`shell${collapsed ? ' shell-collapsed' : ''}`}>
      <aside className="sidebar">
        <NavLink className="sidebar-brand" to={routes.meetings}>
          <BrandMark />
          <strong>TrustCabinet</strong>
        </NavLink>

        {sidebar}

        <div className="sidebar-foot">
          <HealthBadge />
          <button type="button" className="sidebar-collapse" onClick={() => setCollapsed((c) => !c)}>
            <Icon name="chevronLeft" />
            <span>{collapsed ? 'Mở rộng' : 'Thu gọn'}</span>
          </button>
        </div>
      </aside>

      <div className="shell-main">
        <header className="appbar">
          <nav className="workspace-switcher" aria-label="Không gian làm việc">
            <NavLink to={routes.meetings}>
              <Icon name="grid" size={16} />
              <span>Không gian làm việc</span>
            </NavLink>
            <NavLink to={routes.calendar}>
              <Icon name="calendar" size={16} />
              <span>Lịch làm việc</span>
            </NavLink>
          </nav>

          <button type="button" className="icon-button" title="Thông báo">
            <Icon name="bell" />
          </button>

          <details className="account">
            <summary title={user.fullName}>
              <span className="avatar">{initial}</span>
            </summary>
            <div className="account-menu">
              <p className="account-name">{user.fullName}</p>
              <p className="account-meta">{ROLE_LABEL[user.role]}</p>
              <p className="account-meta">{user.email}</p>
              <button type="button" onClick={signOut}>
                Đăng xuất
              </button>
            </div>
          </details>
        </header>

        <main className={`content${wide ? ' content-wide' : ''}`}>
          <Outlet />
        </main>
      </div>
    </div>
  )
}
