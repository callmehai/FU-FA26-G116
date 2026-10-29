import { lazy, Suspense, useState } from 'react'
import { Outlet, useNavigate, useSearchParams } from 'react-router-dom'
import { routes } from '../lib/routes'
import { useAuth } from '../features/auth/useAuth'
import { ConfirmDialog } from './ConfirmDialog'
import { TopBar } from './TopBar'

// Loaded the first time `?create=` appears: most visits never open the form.
const CreateMeetingHost = lazy(() =>
  import('../features/meetings/MeetingDialogs').then((m) => ({ default: m.CreateMeetingHost })),
)

/**
 * Chrome shared by every signed-in screen (ADR-0043): the blue top bar over the routed region. The
 * region is a page frame or the Quản trị area (AdminLayout), each with its own scrolling column.
 */
export function AppShell() {
  const { user, signOut } = useAuth()
  const navigate = useNavigate()
  const [confirmingSignOut, setConfirmingSignOut] = useState(false)
  const [params] = useSearchParams()
  const creating = params.has('create')

  if (!user) return null

  /** UC-03: server-side revocation happens inside `signOut`, before the local token is dropped. */
  async function confirmSignOut() {
    if (!(await signOut())) throw new Error('Không thể đăng xuất. Vui lòng thử lại.')
    navigate(routes.login, { replace: true })
  }

  return (
    <div className="shell">
      <TopBar onSignOut={() => setConfirmingSignOut(true)} />
      <div className="shell-body">
        {/* A screen's chunk loads under the bar, so the bar never blinks out on navigation. */}
        <Suspense fallback={<p className="route-fallback">Đang tải…</p>}>
          <Outlet />
        </Suspense>
      </div>
      {/* The create dialog opens over whichever page carries `?create=` (AI-eCabinet's host). */}
      {creating && (
        <Suspense fallback={null}>
          <CreateMeetingHost />
        </Suspense>
      )}

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
