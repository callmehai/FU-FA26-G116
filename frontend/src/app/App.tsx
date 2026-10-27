import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes, useParams } from 'react-router-dom'
import { AppShell } from '../components/AppShell'
import { TooltipLayer } from '../components/TooltipLayer'
import { NotFound } from '../components/NotFound'
import { RequireAuth } from '../features/auth/RequireAuth'
import { RequireCapability } from '../features/auth/RequireCapability'
import { LoginPage } from '../features/auth/LoginPage'
import { routes } from '../lib/routes'

// Screens are code-split so each lands in its own chunk, loaded on first navigation. The shell and
// the session gate stay eager — they are needed immediately on every signed-in view.
const MeetingsPage = lazy(() =>
  import('../features/meetings/MeetingsPage').then((m) => ({ default: m.MeetingsPage })),
)
const MeetingDetailPage = lazy(() =>
  import('../features/meetings/detail/MeetingDetailPage').then((m) => ({ default: m.MeetingDetailPage })),
)
const MeetingRoomPage = lazy(() =>
  import('../features/calls/room/MeetingRoomPage').then((m) => ({ default: m.MeetingRoomPage })),
)
const UsersPage = lazy(() => import('../features/access/UsersPage').then((m) => ({ default: m.UsersPage })))
const RolesPage = lazy(() => import('../features/access/RolesPage').then((m) => ({ default: m.RolesPage })))
const ResetPasswordPage = lazy(() =>
  import('../features/auth/ResetPasswordPage').then((m) => ({ default: m.ResetPasswordPage })),
)
const CalendarPage = lazy(() =>
  import('../features/calendar/CalendarPage').then((m) => ({ default: m.CalendarPage })),
)
const DocumentViewerPage = lazy(() =>
  import('../features/documents/DocumentViewerPage').then((m) => ({ default: m.DocumentViewerPage })),
)
const DocumentsPage = lazy(() =>
  import('../features/documents/DocumentsPage').then((m) => ({ default: m.DocumentsPage })),
)
const LibraryViewerPage = lazy(() =>
  import('../features/documents/LibraryViewerPage').then((m) => ({ default: m.LibraryViewerPage })),
)
const UserLibraryPage = lazy(() =>
  import('../features/documents/UserLibraryPage').then((m) => ({ default: m.UserLibraryPage })),
)
const VoicePage = lazy(() => import('../features/voice/VoicePage').then((m) => ({ default: m.VoicePage })))

/**
 * Route map: a public login and password reset, then every screen behind a session gate, inside
 * one shell with one sidebar (ADR-0012).
 *
 * Capability-gated routes render the same 404 as an unknown URL, so an admin-only screen never
 * reveals its existence to someone who lacks the capability. None of this is security: the backend
 * re-authorizes every request.
 */
export function App() {
  return (
    <Suspense fallback={<div className="route-fallback">Đang tải…</div>}>
      {/* One tooltip for every screen, the sign-in pages included. */}
      <TooltipLayer />
      <Routes>
        <Route path={routes.login} element={<LoginPage />} />
        {/* Public and outside the session gate: whoever opens the emailed link has no session. */}
        <Route path={routes.resetPassword} element={<ResetPasswordPage />} />

        {/* The bare root lands on the meeting list. */}
        <Route path="/" element={<Navigate to={routes.meetings} replace />} />

        <Route element={<RequireAuth />}>
          {/* The call room fills the window, outside the shell. Anyone may open the URL; the
              backend answers 404 to someone who is not invited, and the page renders NotFound. */}
          <Route path={routes.meetingRoom(':meetingId')} element={<MeetingRoomPage />} />
          {/* Full screen too; an unreadable meeting or document renders NotFound. */}
          <Route path={routes.meetingDocument(':meetingId', ':documentId')} element={<DocumentViewerPage />} />
          <Route path={routes.libraryDocument(':documentId')} element={<LibraryViewerPage />} />
          <Route
            path={routes.userLibraryDocument(':userId', ':documentId')}
            element={
              <RequireCapability cap="AdminUsers">
                <LibraryViewerPage />
              </RequireCapability>
            }
          />
          <Route element={<AppShell />}>
            {/* Inside the guard and the shell: following an old /mgmt link must not remount either —
                that would drop the notification connection. */}
            <Route path={routes.mgmt} element={<Navigate to={routes.meetings} replace />} />
            <Route path={routes.calendar} element={<CalendarPage />} />
            <Route path={routes.documents} element={<DocumentsPage />} />
            <Route path={routes.myVoice} element={<VoicePage />} />
            <Route path={routes.meetings} element={<MeetingsPage />} />
            {/* Create and edit are dialogs now (`?create=`, `?edit=`); old links land on them. */}
            <Route path={routes.meetingNew} element={<Navigate to={`${routes.meetings}?create=1`} replace />} />
            <Route path={routes.meetingEdit(':meetingId')} element={<EditLinkRedirect />} />
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
              path={routes.userLibrary(':userId')}
              element={
                <RequireCapability cap="AdminUsers">
                  <UserLibraryPage />
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
        </Route>
      </Routes>
    </Suspense>
  )
}

/** `/mgmt/meetings/{id}/edit` from before the edit dialog: the meeting's page with it open. */
function EditLinkRedirect() {
  const { meetingId = '' } = useParams()
  return <Navigate to={`${routes.meeting(meetingId)}?edit=1`} replace />
}
