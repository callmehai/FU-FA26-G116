import { lazy, Suspense, useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { routes } from '../lib/routes'
import { useAuth } from '../features/auth/useAuth'
import { avatarColors, initials } from '../lib/avatar'
import { NotificationBell } from '../features/notifications/NotificationBell'
import { AppSidebar } from './AppSidebar'
import { BrandMark } from './BrandMark'
import { ConfirmDialog } from './ConfirmDialog'
import { Icon } from './Icon'

// Loaded the first time `?create=` appears: most visits never open the form.
const CreateMeetingHost = lazy(() =>
  import('../features/meetings/MeetingDialogs').then((m) => ({ default: m.CreateMeetingHost })),
)

/** Where the collapsed/expanded choice survives a reload. A per-browser convenience only. */
const COLLAPSED_KEY = 'tc.sidebarCollapsed'
const NARROW_QUERY = '(max-width: 720px)'

/**
 * Chrome shared by every signed-in screen (ADR-0012): one vertical sidebar — brand, the sections,
 * then the account and the notification bell at its foot — and the routed content beside it.
 * There is no app bar; the page's own header is the first thing in the content.
 */
export function AppShell() {
  const { user, signOut } = useAuth()
  const navigate = useNavigate()
  const [collapsedByUser, setCollapsed] = useState(readCollapsed)
  // Below 720px there is no room for a 256px sidebar, whatever the user last chose.
  const narrow = useMediaQuery(NARROW_QUERY)
  const collapsed = collapsedByUser || narrow
  const [confirmingSignOut, setConfirmingSignOut] = useState(false)
  const accountMenuRef = useRef<HTMLDetailsElement>(null)
  const { pathname } = useLocation()

  // Moving to another screen closes the account menu, as it would on any outside click.
  useEffect(() => {
    accountMenuRef.current?.removeAttribute('open')
  }, [pathname])

  useEffect(() => {
    try {
      localStorage.setItem(COLLAPSED_KEY, collapsedByUser ? '1' : '0')
    } catch {
      // Storage blocked (private window): the choice just does not outlive the tab.
    }
  }, [collapsedByUser])

  // A <details> menu does not close on an outside click or on Escape by itself.
  useEffect(() => {
    function close(event: Event) {
      const menu = accountMenuRef.current
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

  const [params] = useSearchParams()
  const creating = params.has('create')

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

  const avatar = avatarColors(user.fullName)

  return (
    <div className={`shell${collapsed ? ' shell-collapsed' : ''}`}>
      <aside className="sidebar">
        <NavLink className="sidebar-brand" to={routes.calendar} data-tooltip={collapsed ? 'TrustCabinet' : undefined}>
          <span className="sidebar-brand-disc">
            <BrandMark size={32} />
          </span>
          <span className="sidebar-label sidebar-brand-text">
            <strong>TrustCabinet</strong>
            <span>Phòng họp không giấy</span>
          </span>
        </NavLink>

        {/* Inside the sidebar, under the brand: a round button on its edge sat beside every page's
            first control (the back link, "Hôm nay") and was easy to hit by mistake. Icon only, and
            under the fixed-height brand row, so it stays in one spot whether the sidebar is wide or
            narrow; at the foot, the account row growing when narrow pushed it up. */}
        <div className="sidebar-collapse">
          <button
            type="button"
            className="sidebar-item"
            onClick={() => setCollapsed((c) => !c)}
            aria-label={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
            data-tooltip={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
            aria-expanded={!collapsed}
          >
            <Icon name="chevronLeft" size={18} />
          </button>
        </div>

        <AppSidebar collapsed={collapsed} />

        <div className="sidebar-foot">
          <details className="account" ref={accountMenuRef}>
            <summary aria-label={`Tài khoản ${user.fullName}`} data-tooltip={collapsed ? user.fullName : undefined}>
              <span className="avatar" style={{ backgroundColor: avatar.bg, color: avatar.fg }}>
                {initials(user.fullName)}
              </span>
              <span className="sidebar-label account-summary">
                <span className="account-summary-name">{user.fullName}</span>
                <span className="account-summary-role">{user.roleName}</span>
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

          <NotificationBell />
        </div>
      </aside>

      <main className="content">
        {/* A screen's chunk loads inside the shell, so the sidebar never blinks out on navigation. */}
        <Suspense fallback={<p className="route-fallback">Đang tải…</p>}>
          <Outlet />
        </Suspense>
        {/* The create dialog opens over whichever page carries `?create=` (AI-eCabinet's host). */}
        {creating && (
          <Suspense fallback={null}>
            <CreateMeetingHost />
          </Suspense>
        )}
      </main>

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

function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(() => window.matchMedia(query).matches)
  useEffect(() => {
    const list = window.matchMedia(query)
    const onChange = () => setMatches(list.matches)
    list.addEventListener('change', onChange)
    return () => list.removeEventListener('change', onChange)
  }, [query])
  return matches
}

function readCollapsed() {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === '1'
  } catch {
    return false
  }
}
