import { Link, Outlet, useLocation } from 'react-router-dom'
import { routes } from '../lib/routes'
import { useAuth } from '../features/auth/useAuth'
import { Icon } from './Icon'
import { ADMIN_NAV, isCurrent } from './nav'

/**
 * The Quản trị area, as AI-eCabinet lays it out: a white column of its pages beside the page, with
 * the way back to the calendar on top. Under 860px the column becomes one row of tabs over the page.
 */
export function AdminLayout() {
  const { can } = useAuth()
  const { pathname } = useLocation()
  const nav = ADMIN_NAV.filter((e) => !e.cap || can(e.cap))
  // Someone with no admin page gets the gated route's 404 in a plain frame, exactly as for an
  // unknown URL: the area's column would tell them the screen exists.
  if (nav.length === 0) return <PageFrame />

  const links = (className: string) =>
    nav.map((entry) => {
      const on = isCurrent(pathname, entry.to)
      return (
        <Link
          key={entry.to}
          to={entry.to}
          className={`${className}${on ? ' active' : ''}`}
          aria-current={on ? 'page' : undefined}
        >
          <Icon name={entry.icon} size={16} />
          {entry.label}
        </Link>
      )
    })

  return (
    <div className="area">
      <aside className="area-aside">
        <Link to={routes.calendar} className="area-back">
          <Icon name="chevronLeft" size={14} />
          Về Lịch làm việc
        </Link>
        <p className="area-title">Quản trị</p>
        <nav className="area-nav" aria-label="Quản trị">
          {links('area-link')}
        </nav>
      </aside>
      <main className="content">
        <nav className="area-tabs" aria-label="Quản trị">
          {links('area-tab')}
        </nav>
        <div className="page">
          <Outlet />
        </div>
      </main>
    </div>
  )
}

/** Every other signed-in page: one scrolling column, read at most 1400px wide. */
export function PageFrame() {
  return (
    <main className="content">
      <div className="page">
        <Outlet />
      </div>
    </main>
  )
}
