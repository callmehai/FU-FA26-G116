import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from '../components/AppShell'
import { AppSidebar } from '../components/AppSidebar'
import { NotFound } from '../components/NotFound'
import { RequireAuth } from '../features/auth/RequireAuth'
import { RequireCapability } from '../features/auth/RequireCapability'
import { CalendarSidebar } from '../features/calendar/CalendarSidebar'
import { LoginPage } from '../features/auth/LoginPage'
import { routes } from '../lib/routes'

// Screens are code-split so each lands in its own chunk, loaded on first navigation. The shell and
// the session gate stay eager — they are needed immediately on every signed-in view.
const MeetingsPage = lazy(() =>
  import('../features/meetings/MeetingsPage').then((m) => ({ default: m.MeetingsPage })),
)
const CreateMeetingPage = lazy(() =>
  import('../features/meetings/CreateMeetingPage').then((m) => ({ default: m.CreateMeetingPage })),
)
const EditMeetingPage = lazy(() =>
  import('../features/meetings/EditMeetingPage').then((m) => ({ default: m.EditMeetingPage })),
)
const MeetingDetailPage = lazy(() =>
  import('../features/meetings/detail/MeetingDetailPage').then((m) => ({ default: m.MeetingDetailPage })),
)
const UsersPage = lazy(() => import('../features/access/UsersPage').then((m) => ({ default: m.UsersPage })))
const RolesPage = lazy(() => import('../features/access/RolesPage').then((m) => ({ default: m.RolesPage })))
const CalendarPage = lazy(() =>
  import('../features/calendar/CalendarPage').then((m) => ({ default: m.CalendarPage })),
)

/**
 * Route map: a public login, then every screen behind a session gate, split into two workspaces —
 * management under /mgmt and the calendar under /calendar.
 *
 * Capability-gated routes render the same 404 as an unknown URL, so an admin-only screen never
 * reveals its existence to someone who lacks the capability. None of this is security: the backend
 * re-authorizes every request.
 */
export function App() {
  return (
    <Suspense fallback={<div className="route-fallback">Đang tải…</div>}>
      <Routes>
        <Route path={routes.login} element={<LoginPage />} />

        {/* Bare roots land on the management home. */}
        <Route path="/" element={<Navigate to={routes.meetings} replace />} />
        <Route path={routes.mgmt} element={<Navigate to={routes.meetings} replace />} />

        {/* Management workspace. */}
        <Route element={<RequireAuth />}>
          <Route element={<AppShell sidebar={<AppSidebar />} />}>
            <Route path={routes.meetings} element={<MeetingsPage />} />
            <Route
              path={routes.meetingNew}
              element={
                <RequireCapability cap="MeetingCreator">
                  <CreateMeetingPage />
                </RequireCapability>
              }
            />
            {/* The page also 404s for anyone but the meeting's creator, as PUT does. */}
            <Route
              path={routes.meetingEdit(':meetingId')}
              element={
                <RequireCapability cap="MeetingCreator">
                  <EditMeetingPage />
                </RequireCapability>
              }
            />
            {/* No capability gate: an unreadable meeting 404s, which the page renders as NotFound. */}
            <Route path={routes.meeting(':meetingId')} element={<MeetingDetailPage />} />
            <Route
              path={routes.adminUsers}
              element={
                <RequireCapability cap="AdminUsers">
                  <UsersPage />
                </RequireCapability>
              }
            />
            <Route
              path={routes.adminRoles}
              element={
                <RequireCapability cap="AdminUsers">
                  <RolesPage />
                </RequireCapability>
              }
            />
            <Route path="*" element={<NotFound />} />
          </Route>

          {/* Calendar workspace — same shell, its own sidebar, full content width. */}
          <Route element={<AppShell sidebar={<CalendarSidebar />} wide />}>
            <Route path={routes.calendar} element={<CalendarPage />} />
          </Route>
        </Route>
      </Routes>
    </Suspense>
  )
}
