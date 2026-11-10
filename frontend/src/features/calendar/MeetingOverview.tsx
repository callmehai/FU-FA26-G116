import { useEffect, useId, useState, type ReactNode } from 'react'
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
import { CalendarPopover } from './CalendarPopover'
import { usePhone } from './usePhone'

/** "+N" takes one slot: 4 on a wide screen (3 people and "+N"), 3 on a phone. */
const MAX_PEOPLE = 4
const MAX_PEOPLE_PHONE = 3

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
  return (
    <CalendarPopover anchor={anchor} keyboard={keyboard} label={item.title} onClose={onClose}>
      <OverviewContent item={item} onClose={onClose} onAnswered={onAnswered} />
    </CalendarPopover>
  )
}

/**
 * The overview itself, for the popover above or beside a "+N" list (MoreMeetings), which passes `onBack` to return
 * to the list where the two do not fit side by side.
 */
export function OverviewContent({
  item,
  onClose,
  onAnswered,
  onBack,
}: {
  item: MeetingListItem
  onClose: () => void
  onAnswered: (response: 'Accepted' | 'Declined') => void
  onBack?: () => void
}) {
  const titleId = useId()
  const listId = useId()
  const handleAccessLost = useAccessLost()
  const back = backState('Lịch làm việc', useLocation())
  const phone = usePhone()
  const [state, setState] = useState<
    { kind: 'loading' } | { kind: 'loaded'; data: Loaded } | { kind: 'error'; message: string }
  >({
    kind: 'loading',
  })
  const [attempt, setAttempt] = useState(0)
  const [expanded, setExpanded] = useState(false)

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

  const data = state.kind === 'loaded' ? state.data : null
  const meeting = data?.meeting
  const people = data?.people ?? []
  const max = phone ? MAX_PEOPLE_PHONE : MAX_PEOPLE
  const over = people.length > max
  const shown = over ? people.slice(0, max - 1) : people
  const hidden = over ? people.slice(max - 1) : []
  const canRespond = meeting ? meeting.canRespond : item.canRespond

  return (
    <section className="overview-pane" aria-labelledby={titleId}>
      {onBack && (
        <button type="button" className="overview-back" onClick={onBack}>
          <Icon name="chevronLeft" size={14} />
          Quay lại danh sách
        </button>
      )}
      <header className="overview-head">
        <h2 id={titleId} tabIndex={-1} data-overview-heading="">
          {item.title}
        </h2>
        <div className="chip-row">
          <StatusBadge status={item.status} />
          {/* "Thường" is the usual case: like the call room, only a restricted level is labelled. */}
          {item.sensitivity !== 'Normal' && <SensitivityBadge sensitivity={item.sensitivity} />}
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
          <Row label="Chủ toạ">
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
          {/* The card reads `canRespond` and the answer from its own copy of the meeting, so it re-reads
              it: the buttons leave and the chip shows the answer, as on the list. */}
          <RsvpButtons
            meetingId={item.id}
            labelledBy={`${titleId}-ask`}
            onAnswered={(response) => {
              setAttempt((n) => n + 1)
              onAnswered(response)
            }}
          />
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
