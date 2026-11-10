import { useCallback, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { declineShare, endShare, listShares, withdrawShare } from '../../api/calendarSharing'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { SegmentedControl } from '../../components/SegmentedControl'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { Share, ShareLists, ShareResult } from '../../types/calendarSharing'
import { useAuth } from '../auth/useAuth'
import { AcceptShareDialog, ChangeShareDialog, NewShareDialog } from './ShareDialogs'
import { isStale, type ShareOutcome } from './shareOutcome'
import { ShareRow, type RowIntent } from './ShareRow'
import { SharedWeek } from './SharedWeek'
import { isClosed, vietnamToday } from './shareFormat'

type Tab = 'list' | 'week'
type Open = RowIntent | { kind: 'new'; mode: 'request' | 'direct' } | null

/** Open shares first, then the closed ones; the server already sends each group newest first. */
const ordered = (shares: Share[]) => [...shares.filter((s) => !isClosed(s)), ...shares.filter(isClosed)]

/** `?week=` holds any day of the week shown; anything else falls back to this week. */
const DAY = /^\d{4}-\d{2}-\d{2}$/

/**
 * SCRUM-142 — Chia sẻ lịch (UC-84a → UC-84f) and the week of busy hours shared with the caller (UC-85c), on one page so
 * managing a share and seeing its hours are a tab apart, not two menus (AI-eCabinet splits them).
 *
 * Everyone may open it from the account menu: a share of one's own calendar is answered, changed or ended here
 * (PO 10/11/2026, L8). Asking to see others and the week view need the right to create meetings — they are hidden
 * without it, and the server refuses them anyway (403 MSG448).
 */
export function CalendarSharingPage() {
  const { user, can } = useAuth()
  const toast = useToast()
  const organizer = can('MeetingCreator')
  const [params, setParams] = useSearchParams()
  const tab: Tab = organizer && params.get('tab') === 'week' ? 'week' : 'list'
  const weekParam = params.get('week')
  const week = weekParam && DAY.test(weekParam) ? weekParam : vietnamToday()
  const focusId = params.get('share')

  const [lists, setLists] = useState<ShareLists | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  const [open, setOpen] = useState<Open>(null)
  const latest = useRef(0)

  useEffect(() => {
    if (tab !== 'list') return
    const ticket = ++latest.current
    void Promise.resolve().then(async () => {
      try {
        const next = await listShares()
        if (ticket !== latest.current) return
        setLists(next)
        setLoadError(null)
      } catch (err) {
        if (ticket === latest.current) setLoadError(errorMessage(err))
      }
    })
  }, [tab, attempt])

  const reload = useCallback(() => setAttempt((n) => n + 1), [])

  // A notification lands on `?share={id}`: bring that row into view once the lists are in.
  useEffect(() => {
    if (!lists || !focusId) return
    document.getElementById(`share-${focusId}`)?.scrollIntoView({ block: 'center', behavior: 'smooth' })
  }, [lists, focusId])

  function setTab(next: Tab) {
    const p = new URLSearchParams(params)
    if (next === 'week') p.set('tab', 'week')
    else p.delete('tab')
    p.delete('share')
    setParams(p, { replace: true })
  }

  function setWeek(day: string) {
    const p = new URLSearchParams(params)
    if (day === vietnamToday()) p.delete('week')
    else p.set('week', day)
    setParams(p, { replace: true })
  }

  /** After a save: the server's message, then the lists as they now are. */
  const saved = (result: ShareResult) => {
    toast.show(result.message, 'success')
    setOpen(null)
    reload()
  }

  /** The list was out of date: say why (a 404 has no body), then show what is true now. */
  const stale = (error: unknown) => {
    toast.show(isApiError(error, 404) ? 'Chia sẻ này không còn.' : errorMessage(error), 'error')
    setOpen(null)
    reload()
  }

  const done = (outcome: ShareOutcome) => (outcome.kind === 'saved' ? saved(outcome.result) : stale(outcome.error))

  /** A confirmation's call: stale refusals close it and reload; other failures stay inside it. */
  const confirmed = (call: () => Promise<ShareResult>) => async () => {
    try {
      saved(await call())
    } catch (err) {
      if (isStale(err)) stale(err)
      else throw err
    }
  }

  const me = user?.id ?? ''
  const asOwner = lists ? ordered(lists.asOwner) : []
  const asViewer = lists ? ordered(lists.asViewer) : []

  return (
    <>
      <PageHeader title="Chia sẻ lịch" />

      {organizer && (
        <div className="share-tabs">
          <SegmentedControl
            label="Chia sẻ lịch"
            value={tab}
            options={[
              { value: 'list', label: 'Danh sách chia sẻ', short: 'Danh sách' },
              { value: 'week', label: 'Lịch được chia sẻ', short: 'Lịch tuần' },
            ]}
            onChange={setTab}
          />
        </div>
      )}

      {tab === 'week' ? (
        <SharedWeek week={week} onWeek={setWeek} onAsk={() => setOpen({ kind: 'new', mode: 'request' })} />
      ) : loadError && !lists ? (
        <div className="alert alert-error page-alert" role="alert">
          <p>{loadError}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={reload}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      ) : (
        <div className={`share-grid${organizer ? '' : ' share-grid-single'}`}>
          <ShareSection
            title="Người xem được lịch của tôi"
            shares={asOwner}
            loading={!lists}
            as="owner"
            focusId={focusId}
            empty="Bạn chưa chia sẻ lịch với ai."
            action={{
              label: 'Chia sẻ lịch',
              icon: 'calendarCheck',
              onClick: () => setOpen({ kind: 'new', mode: 'direct' }),
            }}
            onIntent={setOpen}
          />
          {organizer && (
            <ShareSection
              title="Lịch được chia sẻ với tôi"
              shares={asViewer}
              loading={!lists}
              as="viewer"
              focusId={focusId}
              empty="Chưa ai chia sẻ lịch với bạn."
              action={{ label: 'Xin xem lịch', icon: 'send', onClick: () => setOpen({ kind: 'new', mode: 'request' }) }}
              onIntent={setOpen}
            />
          )}
        </div>
      )}

      {open?.kind === 'new' && <NewShareDialog mode={open.mode} onClose={() => setOpen(null)} onDone={done} />}
      {open?.kind === 'accept' && <AcceptShareDialog share={open.share} onClose={() => setOpen(null)} onDone={done} />}
      {open?.kind === 'change' && (
        <ChangeShareDialog share={open.share} as={open.as} onClose={() => setOpen(null)} onDone={done} />
      )}
      {open?.kind === 'decline' && (
        <DeclineConfirm
          share={open.share}
          me={me}
          onConfirm={confirmed(() => declineShare(open.share.id))}
          onClose={() => setOpen(null)}
        />
      )}
      {open?.kind === 'withdraw' && (
        <ConfirmDialog
          title={open.share.status === 'Effective' ? 'Rút yêu cầu thay đổi?' : 'Rút yêu cầu?'}
          confirmLabel="Rút yêu cầu"
          pendingLabel="Đang rút…"
          cancelLabel="Giữ yêu cầu"
          onConfirm={confirmed(() => withdrawShare(open.share.id))}
          onClose={() => setOpen(null)}
        >
          {open.share.status === 'Effective'
            ? 'Chia sẻ giữ như hiện tại.'
            : `${(open.share.owner.id === me ? open.share.viewer : open.share.owner).fullName} sẽ không còn thấy yêu cầu này.`}
        </ConfirmDialog>
      )}
      {open?.kind === 'end' && (
        <ConfirmDialog
          title={open.as === 'owner' ? 'Ngừng chia sẻ lịch?' : `Thôi xem lịch của ${open.share.owner.fullName}?`}
          confirmLabel={open.as === 'owner' ? 'Ngừng chia sẻ' : 'Thôi xem'}
          pendingLabel="Đang lưu…"
          cancelLabel="Giữ chia sẻ"
          tone="danger"
          onConfirm={confirmed(() => endShare(open.share.id))}
          onClose={() => setOpen(null)}
        >
          {open.as === 'owner'
            ? `${open.share.viewer.fullName} sẽ không còn thấy giờ bận của bạn.`
            : 'Muốn xem lại phải xin chia sẻ mới.'}
        </ConfirmDialog>
      )}
    </>
  )
}

/** Declining is final: a declined share never comes back, so the dialog says what happens next. */
function DeclineConfirm({
  share,
  me,
  onConfirm,
  onClose,
}: {
  share: Share
  me: string
  onConfirm: () => Promise<void>
  onClose: () => void
}) {
  const change = share.status === 'Effective'
  const iAmOwner = share.owner.id === me
  const other = iAmOwner ? share.viewer : share.owner
  return (
    <ConfirmDialog
      title={change ? 'Từ chối thay đổi?' : iAmOwner ? 'Từ chối cho xem lịch?' : `Từ chối lịch của ${other.fullName}?`}
      confirmLabel="Từ chối"
      pendingLabel="Đang từ chối…"
      cancelLabel="Để sau"
      onConfirm={onConfirm}
      onClose={onClose}
    >
      {change
        ? 'Chia sẻ giữ như hiện tại.'
        : iAmOwner
          ? `${other.fullName} không xem được lịch của bạn; muốn xem phải xin lại.`
          : 'Muốn xem lịch này sau phải xin chia sẻ mới.'}
    </ConfirmDialog>
  )
}

function ShareSection({
  title,
  shares,
  loading,
  as,
  focusId,
  empty,
  action,
  onIntent,
}: {
  title: string
  shares: Share[]
  loading: boolean
  as: 'owner' | 'viewer'
  focusId: string | null
  empty: string
  action: { label: string; icon: 'calendarCheck' | 'send'; onClick: () => void }
  onIntent: (intent: RowIntent) => void
}) {
  const open = shares.filter((s) => !isClosed(s)).length
  return (
    <section className="card share-section" aria-labelledby={`share-${as}`}>
      <header className="share-section-head">
        <h2 id={`share-${as}`}>
          {title}
          {!loading && <span className="share-count">{open}</span>}
        </h2>
        <button type="button" className="btn btn-outline btn-sm" onClick={action.onClick}>
          <Icon name={action.icon} size={16} />
          {action.label}
        </button>
      </header>
      {loading ? (
        <div className="share-skeleton" aria-busy="true">
          {[0, 1, 2].map((i) => (
            <span key={i} className="skeleton" />
          ))}
        </div>
      ) : shares.length === 0 ? (
        <p className="share-empty-line">{empty}</p>
      ) : (
        <ul className="share-list">
          {shares.map((share) => (
            <ShareRow key={share.id} share={share} as={as} focused={share.id === focusId} onIntent={onIntent} />
          ))}
        </ul>
      )}
    </section>
  )
}
