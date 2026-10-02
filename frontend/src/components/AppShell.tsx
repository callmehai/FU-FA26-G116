import { useEffect, useRef, useState, type ReactNode } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { routes } from '../lib/routes'
import { useAuth } from '../features/auth/useAuth'
import { NotificationBell } from '../features/notifications/NotificationBell'
import { BrandMark } from './BrandMark'
import { ConfirmDialog } from './ConfirmDialog'
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
  const navigate = useNavigate()
  const [collapsed, setCollapsed] = useState(false)
  const [confirmingSignOut, setConfirmingSignOut] = useState(false)
  const accountMenuRef = useRef<HTMLDetailsElement>(null)

  // A <details> menu does not close on an outside click by itself.
  useEffect(() => {
    function onPointerDown(event: PointerEvent) {
      const menu = accountMenuRef.current
      if (menu?.open && !menu.contains(event.target as Node)) menu.removeAttribute('open')
    }
    document.addEventListener('pointerdown', onPointerDown)
    return () => document.removeEventListener('pointerdown', onPointerDown)
  }, [])

  if (!user) return null

  function askToSignOut() {
    accountMenuRef.current?.removeAttribute('open')
    setConfirmingSignOut(true)
  }

  /** UC-03: server-side revocation happens inside `signOut`, before the local token is dropped. */
  async function confirmSignOut() {
    if (!(await signOut())) throw new Error('Không thể đăng xuất. Vui lòng thử lại.')
    navigate(routes.login, { replace: true })
  }

  const initial = user.fullName.trim().charAt(0).toUpperCase() || '?'
  const avatar = avatarColors(user.fullName)

  return (
    <div className={`shell${collapsed ? ' shell-collapsed' : ''}`}>
      <aside className="sidebar">
        <NavLink className="sidebar-brand" to={routes.meetings}>
          <BrandMark size={28} />
          <strong>TrustCabinet</strong>
        </NavLink>

        {sidebar}

        <div className="sidebar-foot">
          <button type="button" className="sidebar-collapse" onClick={() => setCollapsed((c) => !c)}>
            <Icon name="chevronLeft" size={16} />
            <span>{collapsed ? 'Mở rộng' : 'Thu gọn'}</span>
          </button>
        </div>
      </aside>

      <div className="shell-main">
        <header className="appbar">
          <nav className="workspace-switcher" aria-label="Không gian làm việc">
            {/* Lit for every management screen, not only the meetings list it lands on. */}
            <NavLink to={routes.mgmt}>
              <Icon name="grid" size={16} />
              <span>Không gian làm việc</span>
            </NavLink>
            <NavLink to={routes.calendar}>
              <Icon name="calendar" size={16} />
              <span>Lịch làm việc</span>
            </NavLink>
          </nav>

          <NotificationBell />

          <details className="account" ref={accountMenuRef}>
            <summary aria-label={user.fullName}>
              <span className="avatar" style={{ backgroundColor: avatar.bg, color: avatar.fg }}>
                {initial}
              </span>
            </summary>
            <div className="account-menu">
              <div className="account-id">
                <p className="account-name">{user.fullName}</p>
                <p className="account-meta">{user.email}</p>
                <p className="account-meta">{user.roleName}</p>
              </div>
              <hr />
              <button type="button" className="menu-item" onClick={askToSignOut}>
                <Icon name="logOut" size={16} />
                Đăng xuất
              </button>
            </div>
          </details>
        </header>

        <main className={`content${wide ? ' content-wide' : ''}`}>
          <Outlet />
        </main>
      </div>

      {confirmingSignOut && (
        <ConfirmDialog
          title="Đăng xuất"
          confirmLabel="Đăng xuất"
          pendingLabel="Đang đăng xuất…"
          onConfirm={confirmSignOut}
          onClose={() => setConfirmingSignOut(false)}
        >
          Bạn có chắc muốn đăng xuất? Phiên đăng nhập trên thiết bị này sẽ kết thúc.
        </ConfirmDialog>
      )}
    </div>
  )
}

/** Soft hues, kept off the brand red so an avatar never reads as a primary button. */
const AVATAR_HUES = [28, 45, 95, 150, 190, 215, 255, 290, 330]

/** A stable tint per name: same person, same colour, on every screen and every visit. */
function avatarColors(name: string) {
  let hash = 0
  for (const char of name) hash = (hash * 31 + char.charCodeAt(0)) >>> 0
  const hue = AVATAR_HUES[hash % AVATAR_HUES.length]
  return { bg: `hsl(${hue} 62% 90%)`, fg: `hsl(${hue} 48% 32%)` }
}
