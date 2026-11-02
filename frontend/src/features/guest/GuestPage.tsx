import { useCallback, useEffect, useLayoutEffect, useMemo, useState, type ReactNode } from 'react'
import { Outlet, useLocation, useNavigate, useParams } from 'react-router-dom'
import { setGuestAuth } from '../../api/client'
import { getGuestSession, openInvitation } from '../../api/guest'
import { BrandMark } from '../../components/BrandMark'
import { Icon } from '../../components/Icon'
import { errorMessage, isApiError } from '../../lib/errors'
import type { GuestInvitation, GuestSession } from '../../types/guest'
import { GuestContext, type GuestScope } from './guestContext'
import { GuestProfileForm } from './GuestProfileForm'
import { dropGuestSession, readGuestEntry, updateGuestEntry } from './guestStore'
import { GuestVerifyForm } from './GuestVerifyForm'

/** MSG371's text, for a link the page cannot even try: no token in it, or a malformed invitation id. */
const INVALID_LINK = 'Lời mời không còn hiệu lực. Vui lòng liên hệ Thư ký cuộc họp.'
/** MSG378's text, for a token past its time: the server answers that with a bare 401, not MSG378. */
const SESSION_ENDED = 'Phiên truy cập đã kết thúc. Vui lòng mở lại liên kết trong email.'
/** How often an open meeting re-checks the session: the hub stops telling a guest anything once it ends. */
const RECHECK_MS = 20_000
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
/** One re-check at a time; a tab shows one guest page. */
let rechecking = false
let shownGate: Gate['kind'] = 'checking'

type Gate =
  | { kind: 'checking' }
  | { kind: 'verify'; invitation: GuestInvitation; notice: string | null }
  | { kind: 'profile'; session: GuestSession }
  | { kind: 'in'; session: GuestSession }
  /** The link no longer opens anything (EX-1); `wasIn` when the meeting was on screen until now. */
  | { kind: 'closed'; message: string; wasIn: boolean }
  | { kind: 'signedOut' }
  | { kind: 'error'; message: string }

/**
 * UC-04, screen 1.3: the page an invitation email links to (`/guest/{invitationId}#token=…`), and the
 * guest's way into that one meeting. Public, outside the internal session and its sidebar (BR-156).
 *
 * The gate decides what shows: the meeting while the stored session is good (ALT-1, the code is
 * skipped), else the code sent to the invited email, then the self-declared name. Every way the link
 * can fail reads the same (EX-1, MSG371). While the meeting is open the session is re-checked on a
 * timer, on focus and after any 404 from a panel; once it is over the meeting is unmounted — nothing it
 * had loaded stays on screen — and the invitation is reopened to tell a lapsed session (a new code
 * opens it again) from a meeting that ended or a revoked invitation (nothing does).
 *
 * The guest token is handed to the API client for as long as this page is on screen, so the shared
 * panels and the meeting hub go as the guest, never with an internal session the browser may hold.
 */
export function GuestPage() {
  const { invitationId = '' } = useParams()
  const location = useLocation()
  const navigate = useNavigate()
  const [gate, setGate] = useState<Gate>({ kind: 'checking' })
  // What the gate shows, for the re-check, which only has work to do while the meeting is on screen.
  useEffect(() => {
    shownGate = gate.kind
  }, [gate.kind])

  // The link token travels in the fragment, never to a server; keep it, then drop it from the address bar.
  // An id that is no GUID can open nothing, so nothing is kept for it.
  const hashToken = new URLSearchParams(location.hash.slice(1)).get('token')?.trim() || null
  if (hashToken && GUID.test(invitationId) && readGuestEntry(invitationId).linkToken !== hashToken) {
    updateGuestEntry(invitationId, { linkToken: hashToken })
  }
  useEffect(() => {
    if (location.hash) navigate({ pathname: location.pathname, search: location.search }, { replace: true })
  }, [location.hash, location.pathname, location.search, navigate])

  /** Finds the way in, from the stored session or else the link. `notice` says why the code is asked again. */
  const enter = useCallback(
    async (notice: string | null = null) => {
      const entry = readGuestEntry(invitationId)
      let hadSession = false
      if (entry.accessToken) {
        try {
          const session = await getGuestSession(entry.accessToken)
          return setGate(session.profileCompleted ? { kind: 'in', session } : { kind: 'profile', session })
        } catch (err) {
          // MSG378, or a token past its time: this session is over, the invitation may not be.
          if (!isApiError(err, 404) && !isApiError(err, 401))
            return setGate({ kind: 'error', message: errorMessage(err) })
          dropGuestSession(invitationId)
          hadSession = true
          notice ??= isApiError(err, 404) ? errorMessage(err) : SESSION_ENDED
        }
      }
      // Closed after the guest was in — on screen now, or in a session this browser kept: most likely it ended.
      // The link opens nothing any more, so this browser forgets it.
      const closed = (message: string) => {
        updateGuestEntry(invitationId, { linkToken: undefined })
        setGate((g) => ({
          kind: 'closed',
          message,
          wasIn: hadSession || g.kind === 'in' || (g.kind === 'closed' && g.wasIn),
        }))
      }
      if (!entry.linkToken) return closed(INVALID_LINK)
      try {
        setGate({ kind: 'verify', invitation: await openInvitation(invitationId, entry.linkToken), notice })
      } catch (err) {
        if (isApiError(err, 404)) return closed(err.body?.message ?? INVALID_LINK)
        setGate({ kind: 'error', message: errorMessage(err) })
      }
    },
    [invitationId],
  )

  useEffect(() => {
    void Promise.resolve().then(() => enter())
  }, [enter])

  /**
   * One re-check at a time, and only while the meeting is on screen: a panel's request still in flight when
   * the meeting gave way must not start the gate over. A good answer only replaces the session when
   * something in it changed. Stable, since the panels' loaders depend on it through useAccessLost.
   */
  const recheck = useCallback(async () => {
    if (rechecking || shownGate !== 'in') return
    rechecking = true
    try {
      const token = readGuestEntry(invitationId).accessToken
      if (!token) return await enter()
      const session = await getGuestSession(token)
      setGate((g) =>
        g.kind === 'in' && JSON.stringify(g.session) !== JSON.stringify(session) ? { kind: 'in', session } : g,
      )
    } catch (err) {
      // An outage keeps the meeting on screen; the next check tries again.
      if (isApiError(err, 404) || isApiError(err, 401)) await enter()
    } finally {
      rechecking = false
    }
  }, [invitationId, enter])
  const recheckNow = useCallback(() => void recheck(), [recheck])

  // The panels and the hub take the guest token from here; a 401 means it is no longer good. The panels
  // only mount once the gate has been through the network, long after this has run.
  useLayoutEffect(() => {
    setGuestAuth({
      getAccessToken: async () => readGuestEntry(invitationId).accessToken ?? null,
      recoverFromUnauthorized: async () => {
        void recheck()
        return null
      },
    })
    return () => setGuestAuth(null)
  }, [invitationId, recheck])

  const inMeeting = gate.kind === 'in'
  useEffect(() => {
    if (!inMeeting) return
    const check = () => document.visibilityState === 'visible' && void recheck()
    const timer = window.setInterval(check, RECHECK_MS)
    window.addEventListener('focus', check)
    document.addEventListener('visibilitychange', check)
    return () => {
      window.clearInterval(timer)
      window.removeEventListener('focus', check)
      document.removeEventListener('visibilitychange', check)
    }
  }, [inMeeting, recheck])

  const session = gate.kind === 'in' ? gate.session : null
  const scope = useMemo<GuestScope | null>(
    () => session && { invitationId, session, recheck: recheckNow },
    [invitationId, session, recheckNow],
  )

  if (scope) {
    return (
      <GuestContext value={scope}>
        <Outlet context={{ signedOut: () => setGate({ kind: 'signedOut' }) } satisfies GuestOutletContext} />
      </GuestContext>
    )
  }

  return (
    <div className="login-screen guest-screen">
      <div className="login-card guest-card">
        {gate.kind === 'checking' ? (
          <p className="guest-loading" aria-busy="true">
            Đang mở lời mời…
          </p>
        ) : gate.kind === 'verify' ? (
          <GuestVerifyForm
            invitationId={invitationId}
            invitation={gate.invitation}
            notice={gate.notice}
            onVerified={() => void enter()}
            onClosed={(message) => setGate({ kind: 'closed', message, wasIn: false })}
          />
        ) : gate.kind === 'profile' ? (
          <GuestProfileForm
            invitationId={invitationId}
            session={gate.session}
            onSaved={(saved) => setGate({ kind: 'in', session: saved })}
            onSessionEnded={() => void enter()}
          />
        ) : gate.kind === 'error' ? (
          <GateMessage title="Chưa mở được lời mời" tone="alert-error" message={gate.message}>
            <button
              type="button"
              className="btn btn-outline btn-block"
              onClick={() => {
                setGate({ kind: 'checking' })
                void enter()
              }}
            >
              <Icon name="refresh" size={16} />
              Thử lại
            </button>
          </GateMessage>
        ) : gate.kind === 'signedOut' ? (
          <GateMessage title="Bạn đã thoát khỏi cuộc họp" tone="alert-info" message="Mở lại để nhập mã mới.">
            <button
              type="button"
              className="btn btn-outline btn-block"
              onClick={() => {
                setGate({ kind: 'checking' })
                void enter()
              }}
            >
              <Icon name="logIn" size={16} />
              Vào lại cuộc họp
            </button>
          </GateMessage>
        ) : gate.kind === 'closed' ? (
          // The backend never says which (EX-1): the meeting ended, or the invitation was revoked or never valid.
          <GateMessage
            title={gate.wasIn ? 'Cuộc họp đã kết thúc hoặc lời mời đã bị thu hồi' : 'Không mở được lời mời'}
            tone="alert-warning"
            message={gate.message}
          />
        ) : null}
      </div>
    </div>
  )
}

export interface GuestOutletContext {
  signedOut: () => void
}

function GateMessage({
  title,
  tone,
  message,
  children,
}: {
  title: string
  tone: string
  message: string
  children?: ReactNode
}) {
  return (
    <>
      <div className="login-head">
        <span className="login-mark">
          <BrandMark size={56} />
        </span>
        <h1>{title}</h1>
      </div>
      <p className={`alert ${tone}`} role="alert">
        {message}
      </p>
      {children}
    </>
  )
}
