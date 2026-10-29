import { Link, NavLink, useLocation } from 'react-router-dom'
import { routes } from '../lib/routes'
import { avatarColors, initials } from '../lib/avatar'
import { useMediaQuery } from '../lib/useMediaQuery'
import { useAuth } from '../features/auth/useAuth'
import { NotificationBell } from '../features/notifications/NotificationBell'
import { BrandMark } from './BrandMark'
import { Icon } from './Icon'
import { ADMIN_NAV, MAIN_NAV, type NavEntry, isCurrent } from './nav'
import { useDropMenu } from './useDropMenu'

/** Under this width the sections fold into the ☰ menu, as AI-eCabinet's bar does. */
const FOLD_QUERY = '(max-width: 859px)'

/**
 * The one blue bar over every signed-in screen (ADR-0035, after AI-eCabinet): the brand home to the
 * calendar, the sections, the Quản trị area for administrators, then the bell and the avatar menu.
 * Hiding an entry is presentation only — every route and endpoint behind it re-checks the capability.
 */
export function TopBar({ onSignOut }: { onSignOut: () => void }) {
  const { can } = useAuth()
  const { pathname } = useLocation()
  const folded = useMediaQuery(FOLD_QUERY)
  const admin = ADMIN_NAV.filter((e) => !e.cap || can(e.cap))

  return (
    <header className="topbar">
      {folded && <NavMenu admin={admin} />}
      <Link className="topbar-brand" to={routes.calendar}>
        <span className="brand-disc">
          <BrandMark size={28} />
        </span>
        <span className="topbar-brand-text">
          <strong>TrustCabinet</strong>
          <span>Phòng họp không giấy</span>
        </span>
      </Link>

      {!folded && (
        <nav className="topbar-nav" aria-label="Điều hướng chính">
          {MAIN_NAV.map((entry) => (
            <NavLink key={entry.to} to={entry.to} className="topbar-tab">
              {entry.label}
            </NavLink>
          ))}
          {admin.length > 0 && (
            <Link
              to={admin[0].to}
              className={`topbar-tab topbar-tab-area${pathname.startsWith('/mgmt/admin') ? ' active' : ''}`}
              aria-current={pathname.startsWith('/mgmt/admin') ? 'page' : undefined}
            >
              Quản trị
            </Link>
          )}
        </nav>
      )}

      <div className="topbar-end">
        <NotificationBell />
        <UserMenu onSignOut={onSignOut} />
      </div>
    </header>
  )
}

/** The sections, and the Quản trị pages under their own heading, in one menu on a narrow screen. */
function NavMenu({ admin }: { admin: NavEntry[] }) {
  const { pathname } = useLocation()
  const { open, setOpen, rootRef, buttonRef } = useDropMenu()

  function item(entry: NavEntry) {
    const on = isCurrent(pathname, entry.to)
    return (
      <Link
        key={entry.to}
        to={entry.to}
        className={`menu-item drop-menu-link${on ? ' drop-menu-current' : ''}`}
        aria-current={on ? 'page' : undefined}
      >
        <Icon name={entry.icon} size={16} />
        {entry.label}
      </Link>
    )
  }

  return (
    <nav className="drop" ref={rootRef} aria-label="Điều hướng chính">
      <button
        ref={buttonRef}
        type="button"
        className="topbar-icon"
        aria-label="Mở menu điều hướng"
        data-tooltip="Mở menu điều hướng"
        aria-expanded={open}
        aria-controls={open ? 'nav-menu' : undefined}
        onClick={() => setOpen(!open)}
      >
        <Icon name={open ? 'x' : 'menu'} size={18} />
      </button>
      {open && (
        <div className="drop-menu drop-menu-start" id="nav-menu">
          {MAIN_NAV.map(item)}
          {admin.length > 0 && (
            <>
              <hr />
              <p className="drop-menu-label">Quản trị</p>
              {admin.map(item)}
            </>
          )}
        </div>
      )}
    </nav>
  )
}

/** The avatar, the name and a caret; the menu holds one's own pages and signing out. */
function UserMenu({ onSignOut }: { onSignOut: () => void }) {
  const { user } = useAuth()
  const { open, setOpen, close, rootRef, buttonRef } = useDropMenu()
  if (!user) return null
  const colors = avatarColors(user.fullName)
  const avatar = (size: number) => (
    <span className="avatar" style={{ width: size, height: size, backgroundColor: colors.bg, color: colors.fg }}>
      {initials(user.fullName)}
    </span>
  )

  return (
    <div className="drop" ref={rootRef}>
      <button
        ref={buttonRef}
        type="button"
        className="topbar-account"
        aria-label={`Tài khoản ${user.fullName}`}
        aria-expanded={open}
        aria-controls={open ? 'account-menu' : undefined}
        onClick={() => setOpen(!open)}
      >
        {avatar(32)}
        <b className="topbar-account-name">{user.fullName}</b>
        <span className="topbar-account-caret">
          <Icon name="chevronDown" size={14} />
        </span>
      </button>
      {open && (
        <div className="drop-menu drop-menu-end account-menu" id="account-menu">
          <div className="account-id">
            {avatar(38)}
            <div>
              <p className="account-name">{user.fullName}</p>
              <p className="account-meta">{user.email}</p>
              <p className="account-meta">{user.roleName}</p>
            </div>
          </div>
          <hr />
          <Link to={routes.myVoice} className="menu-item">
            <Icon name="fingerprint" size={16} />
            Giọng nói của tôi
          </Link>
          <button
            type="button"
            className="menu-item"
            onClick={() => {
              close()
              onSignOut()
            }}
          >
            <Icon name="logOut" size={16} />
            Đăng xuất
          </button>
        </div>
      )}
    </div>
  )
}
