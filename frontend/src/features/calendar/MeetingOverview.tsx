import { useCallback, useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { getMeeting } from '../../api/meetings'
import { listParticipants } from '../../api/participants'
import { Icon } from '../../components/Icon'
import { avatarColors, initials } from '../../lib/avatar'
import { backState } from '../../lib/backTarget'
import { errorMessage } from '../../lib/errors'
import { formatClock, formatWeekdayDate } from '../../lib/format'
import { routes } from '../../lib/routes'
import { MEETING_MODE_LABEL, type Meeting, type MeetingListItem } from '../../types/meeting'
import type { ParticipantRow } from '../../types/participant'
import { useAccessLost } from '../auth/useAccessLost'
import { SensitivityBadge, StatusBadge } from '../meetings/StatusBadge'
import { RsvpButtons, RsvpChip } from '../participants/Rsvp'

/** "+N" takes one slot: 4 on a wide screen (3 people and "+N"), 3 on a phone. */
const MAX_PEOPLE = 4
const MAX_PEOPLE_PHONE = 3
const PHONE = '(max-width: 640px)'
/** Gap to the tile, and the least room kept to the viewport's edges. */
const SIDE_OFFSET = 8
const EDGE = 8

type Loaded = { meeting: Meeting; people: ParticipantRow[] | null }

/**
 * The quick look behind a calendar tile, as AI-eCabinet opens it (its MeetingOverviewContent): when
 * and where, the Chair, a few lines of the description, who attends, and the one thing the meeting
 * asks of the caller right now ("Xác nhận tham dự?"). The full picture is one click away through
 * "Xem chi tiết". Beside the tile on a wide screen; a bottom sheet over a scrim on a phone.
 *
 * Escape and a click elsewhere close it; a click on another tile opens that one instead.
 */
export function MeetingOverview({
  item,
  anchor,
  keyboard,
  onClose,
  onAnswered,
}: {
  item: MeetingListItem
  anchor: HTMLElement
  /** Opened from the keyboard: focus moves into the card, and back to the tile on close. */
  keyboard: boolean
  onClose: () => void
  onAnswered: (response: 'Accepted' | 'Declined') => void
}) {
  const titleId = useId()
  const listId = useId()
  const handleAccessLost = useAccessLost()
  const back = backState('Lịch làm việc', useLocation())
  const ref = useRef<HTMLElement>(null)
  const [phone, setPhone] = useState(() => window.matchMedia(PHONE).matches)
  const [state, setState] = useState<
    { kind: 'loading' } | { kind: 'loaded'; data: Loaded } | { kind: 'error'; message: string }
  >({
    kind: 'loading',
  })
  const [attempt, setAttempt] = useState(0)
  const [expanded, setExpanded] = useState(false)

  useEffect(() => {
    const query = window.matchMedia(PHONE)
    const follow = () => setPhone(query.matches)
    query.addEventListener('change', follow)
    return () => query.removeEventListener('change', follow)
  }, [])

  // The meeting for its Chair, link and description; the roster for who attends. A roster the
  // caller may not read just leaves the people out.
  useEffect(() => {
    let cancelled = false
    void Promise.resolve().then(async () => {
      try {
        const [meeting, people] = await Promise.all([
          getMeeting(item.id),
          listParticipants(item.id, '', 1, 100).then(
            (page) => page.items,
            () => null,
          ),
        ])
        if (!cancelled) setState({ kind: 'loaded', data: { meeting, people } })
      } catch (err) {
        if (cancelled || handleAccessLost(err)) return
        setState({ kind: 'error', message: errorMessage(err) })
      }
    })
    return () => {
      cancelled = true
    }
  }, [item.id, attempt, handleAccessLost])

  // Beside the tile: to its right when there is room, else to its left, kept inside the viewport.
  const place = useCallback(() => {
    const card = ref.current
    if (!card || phone) return
    const box = anchor.getBoundingClientRect()
    const width = card.offsetWidth
    const height = card.offsetHeight
    const right = box.right + SIDE_OFFSET
    const left = right + width + EDGE <= window.innerWidth ? right : Math.max(EDGE, box.left - SIDE_OFFSET - width)
    const top = Math.min(Math.max(EDGE, box.top), window.innerHeight - height - EDGE)
    card.style.left = `${left}px`
    card.style.top = `${Math.max(EDGE, top)}px`
  }, [anchor, phone])

  useLayoutEffect(() => {
    place()
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [place, state, expanded])

  // Escape, or a press outside both the card and any calendar tile (a tile toggles its own card).
  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape' && !document.querySelector('dialog[open]')) onClose()
    }
    function onPointer(event: PointerEvent) {
      const target = event.target as Element
      if (ref.current?.contains(target) || target.closest('[data-cal-trigger], dialog')) return
      onClose()
    }
    document.addEventListener('keydown', onKey)
    document.addEventListener('pointerdown', onPointer)
    return () => {
      document.removeEventListener('keydown', onKey)
      document.removeEventListener('pointerdown', onPointer)
    }
  }, [onClose])

  // A keyboard opening moves focus in; closing hands it back to the tile.
  useEffect(() => {
    if (keyboard || phone) ref.current?.querySelector<HTMLElement>('[data-overview-heading]')?.focus()
    return () => {
      if (keyboard && anchor.isConnected) anchor.focus()
    }
  }, [keyboard, phone, anchor])

  const data = state.kind === 'loaded' ? state.data : null
  const meeting = data?.meeting
  const people = data?.people ?? []
  const max = phone ? MAX_PEOPLE_PHONE : MAX_PEOPLE
  const over = people.length > max
  const shown = over ? people.slice(0, max - 1) : people
  const hidden = over ? people.slice(max - 1) : []
  const canRespond = meeting ? meeting.canRespond : item.canRespond

  const card = (
    <section
      ref={ref}
      className={phone ? 'overview overview-sheet' : 'overview'}
      role="dialog"
      aria-labelledby={titleId}
    >
      <header className="overview-head">
        <h2 id={titleId} tabIndex={-1} data-overview-heading="">
          {item.title}
        </h2>
        <div className="chip-row">
          <StatusBadge status={item.status} />
          <SensitivityBadge sensitivity={item.sensitivity} />
          <RsvpChip response={meeting?.myResponse ?? item.myResponse} />
        </div>
      </header>

      <div className="overview-body">
        {state.kind === 'error' && <p className="overview-note">Không tải được thông tin cuộc họp. {state.message}</p>}
        <dl className="overview-facts">
          <Row label="Thời gian">
            <b className="overview-time">
              {formatClock(item.plannedStartTime)}–{formatClock(item.plannedEndTime)}
            </b>
            <span className="overview-muted">{formatWeekdayDate(item.plannedStartTime)}</span>
          </Row>
          <Row label="Hình thức">{MEETING_MODE_LABEL[item.mode]}</Row>
          {item.mode !== 'Online' && item.location && <Row label="Địa điểm">{item.location}</Row>}
          {item.mode !== 'InPerson' &&
            (meeting ? (
              <Row label="Liên kết tham gia">
                {meeting.onlineLink ? (
                  <a className="overview-link" href={meeting.onlineLink} target="_blank" rel="noreferrer">
                    Tham gia cuộc họp trực tuyến
                    <Icon name="externalLink" size={14} />
                  </a>
                ) : (
                  'Phòng họp trực tuyến TrustCabinet'
                )}
              </Row>
            ) : (
              <Row label="Liên kết tham gia">
                <span className="skeleton overview-skeleton" />
              </Row>
            ))}
          <Row label="Chủ tọa">
            {meeting ? (
              (meeting.chair?.fullName ?? <em className="value-empty">Chưa có</em>)
            ) : (
              <span className="skeleton overview-skeleton" />
            )}
          </Row>
        </dl>

        {meeting?.description && <p className="overview-desc">{meeting.description}</p>}

        {state.kind === 'loading' && (
          <div className="overview-people" aria-hidden="true">
            <span className="skeleton overview-skeleton-label" />
            <div className="overview-avatars">
              {[0, 1, 2].map((i) => (
                <span key={i} className="skeleton overview-skeleton-avatar" />
              ))}
            </div>
          </div>
        )}

        {people.length > 0 && (
          <div className="overview-people">
            <p className="overview-people-label">Thành phần tham dự · {people.length}</p>
            <ul className="overview-avatars">
              {shown.map((p) => (
                <li key={p.id} data-tooltip={p.displayName}>
                  <Avatar name={p.displayName} />
                  <span>{p.displayName}</span>
                </li>
              ))}
              {hidden.length > 0 && (
                <li>
                  <button
                    type="button"
                    className={expanded ? 'overview-more overview-more-on' : 'overview-more'}
                    aria-expanded={expanded}
                    aria-controls={listId}
                    aria-label={`Xem thêm ${hidden.length} người`}
                    onClick={() => setExpanded((v) => !v)}
                  >
                    +{hidden.length}
                  </button>
                  <span className="overview-muted">người khác</span>
                </li>
              )}
            </ul>
            {expanded && hidden.length > 0 && (
              <ul id={listId} className="overview-rest" aria-label="Những người còn lại">
                {hidden.map((p) => (
                  <li key={p.id} data-tooltip={p.displayName}>
                    <Avatar name={p.displayName} small />
                    <span>{p.displayName}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>

      {canRespond && (
        <div className="overview-task">
          <span id={`${titleId}-ask`} className="overview-task-title">
            <Icon name="calendar" size={16} />
            Xác nhận tham dự?
          </span>
          <RsvpButtons meetingId={item.id} labelledBy={`${titleId}-ask`} onAnswered={onAnswered} />
        </div>
      )}

      <footer className="overview-foot">
        <button type="button" className="btn btn-outline" onClick={onClose}>
          <Icon name="x" size={16} />
          Đóng
        </button>
        {state.kind === 'error' ? (
          <button type="button" className="btn btn-primary" onClick={() => setAttempt((n) => n + 1)}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        ) : (
          <Link className="btn btn-primary" to={routes.meeting(item.id)} state={back}>
            Xem chi tiết
            <Icon name="arrowRight" size={16} />
          </Link>
        )}
      </footer>
    </section>
  )

  if (!phone) return card
  return (
    <div className="overview-scrim" onPointerDown={(e) => e.target === e.currentTarget && onClose()}>
      {card}
    </div>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="overview-row">
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  )
}

function Avatar({ name, small = false }: { name: string; small?: boolean }) {
  const colors = avatarColors(name)
  return (
    <span
      className={small ? 'avatar overview-avatar-sm' : 'avatar overview-avatar'}
      style={{ background: colors.bg, color: colors.fg }}
      aria-hidden="true"
    >
      {initials(name)}
    </span>
  )
}
