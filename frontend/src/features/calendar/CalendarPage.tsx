import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { Icon } from '../../components/Icon'
import { Select } from '../../components/Select'
import { backState } from '../../lib/backTarget'
import { routes } from '../../lib/routes'
import { useAuth } from '../auth/useAuth'
import { placeLabel } from '../meetings/place'
import { SensitivityBadge, StatusBadge } from '../meetings/StatusBadge'
import { STATUS_TONE } from '../meetings/statusTone'
import { useWeekSchedule, type ScheduleEntry, type WeekSchedule } from './useWeekSchedule'

/** The grid covers the whole day; it opens scrolled to the part that matters (see WeekGrid). */
const HOURS_PER_DAY = 24
const HOUR_HEIGHT = 56
const GRID_HEIGHT = HOURS_PER_DAY * HOUR_HEIGHT
/** Where an empty week, or a week whose first meeting starts early, opens. */
const WORKDAY_START_HOUR = 8
/** A meeting shorter than this would not fit its own time line. */
const MIN_EVENT_HEIGHT = 30

const WEEKDAYS = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy']

type Span = 'work' | 'full'
const SPAN_OPTIONS: { value: Span; label: string }[] = [
  { value: 'work', label: 'Tuần làm việc' },
  { value: 'full', label: 'Cả tuần' },
]

type Mode = 'calendar' | 'list'

const DATE = /^\d{4}-\d{2}-\d{2}$/

/**
 * "Lịch làm việc" (ADR-0012): the week's meetings, Monday first, as a time grid or as a
 * list grouped by day. The week, the span and the view live in the URL, so a reload or a shared
 * link opens the same thing.
 */
export function CalendarPage() {
  const { can } = useAuth()
  const [params, setParams] = useSearchParams()
  const now = useNow()
  const [attempt, setAttempt] = useState(0)
  const monday = readMonday(params.get('week'), now)
  const span: Span = params.get('span') === 'full' ? 'full' : 'work'
  const mode: Mode = params.get('mode') === 'list' ? 'list' : 'calendar'
  const days = Array.from({ length: span === 'full' ? 7 : 5 }, (_, i) => addDays(monday, i))
  const schedule = useWeekSchedule(days[0], endOfDay(days[days.length - 1]), attempt)

  function go(next: { week?: Date; span?: Span; mode?: Mode }) {
    const out = new URLSearchParams(params)
    const week = next.week ?? monday
    if (sameDay(week, startOfWeek(now))) out.delete('week')
    else out.set('week', toDateInput(week))
    const nextSpan = next.span ?? span
    if (nextSpan === 'work') out.delete('span')
    else out.set('span', nextSpan)
    const nextMode = next.mode ?? mode
    if (nextMode === 'calendar') out.delete('mode')
    else out.set('mode', nextMode)
    setParams(out, { replace: true })
  }

  return (
    <>
      <div className="calendar-toolbar">
        <button type="button" className="btn btn-outline" onClick={() => go({ week: startOfWeek(now) })}>
          Hôm nay
        </button>
        <div className="button-pair">
          <button
            type="button"
            className="btn btn-outline"
            onClick={() => go({ week: addDays(monday, -7) })}
            aria-label="Tuần trước"
            title="Tuần trước"
          >
            <Icon name="chevronLeft" size={18} />
          </button>
          <button
            type="button"
            className="btn btn-outline"
            onClick={() => go({ week: addDays(monday, 7) })}
            aria-label="Tuần sau"
            title="Tuần sau"
          >
            <Icon name="chevronRight" size={18} />
          </button>
        </div>
        <h1 className="calendar-range">{rangeLabel(days[0], days[days.length - 1])}</h1>

        <div className="calendar-toolbar-end">
          <div className="view-toggle" role="group" aria-label="Chế độ xem">
            <button type="button" aria-pressed={mode === 'calendar'} onClick={() => go({ mode: 'calendar' })}>
              Lịch
            </button>
            <button type="button" aria-pressed={mode === 'list'} onClick={() => go({ mode: 'list' })}>
              Danh sách
            </button>
          </div>
          <Select value={span} options={SPAN_OPTIONS} onChange={(s) => go({ span: s })} label="Số ngày hiển thị" />
          {can('MeetingCreator') && (
            <Link className="btn btn-primary" to={routes.meetingNew}>
              <Icon name="plus" size={18} />
              Tạo cuộc họp
            </Link>
          )}
        </div>
      </div>

      <ScheduleNotice schedule={schedule} onRetry={() => setAttempt((n) => n + 1)} />

      {mode === 'calendar' ? (
        <WeekGrid days={days} now={now} schedule={schedule} />
      ) : (
        <WeekList days={days} now={now} schedule={schedule} />
      )}
    </>
  )
}

/** Why the week shows nothing, when it is not simply free. */
function ScheduleNotice({ schedule, onRetry }: { schedule: WeekSchedule; onRetry: () => void }) {
  if (schedule.kind === 'error') {
    return (
      <div className="alert alert-error page-alert calendar-note" role="alert">
        <p>{schedule.message}</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={onRetry}>
          Thử lại
        </button>
      </div>
    )
  }
  return null
}

function WeekGrid({ days, now, schedule }: { days: Date[]; now: Date; schedule: WeekSchedule }) {
  const entries = schedule.kind === 'loaded' ? schedule.entries : []
  const nowTop = minutesOfDay(now) * (HOUR_HEIGHT / 60)
  const showsToday = days.some((d) => sameDay(d, now))
  const hours = Array.from({ length: HOURS_PER_DAY }, (_, i) => i)
  const scrollRef = useRef<HTMLDivElement>(null)

  // Open where the eye should land: the current time on this week (a third of the way down, so the
  // next hours show), otherwise the week's first meeting or the start of the working day. Once per
  // week and view — later reloads of the same week leave the user's scroll alone.
  const firstStart = entries.find((e) => days.some((d) => sameDay(e.start, d)))?.start
  const anchorMinutes = showsToday
    ? minutesOfDay(now)
    : Math.min(WORKDAY_START_HOUR * 60, firstStart ? minutesOfDay(firstStart) : Infinity)
  const weekKey = days[0].toISOString()
  const scrolledFor = useRef<string | null>(null)
  useLayoutEffect(() => {
    const el = scrollRef.current
    if (!el || schedule.kind === 'loading' || scrolledFor.current === weekKey) return
    scrolledFor.current = weekKey
    const head = el.querySelector<HTMLElement>('.calendar-day')?.offsetHeight ?? 0
    const target = anchorMinutes * (HOUR_HEIGHT / 60)
    el.scrollTop = Math.max(0, target - (showsToday ? (el.clientHeight - head) / 3 : 16))
  }, [weekKey, schedule.kind, anchorMinutes, showsToday])

  return (
    <div className="calendar" aria-busy={schedule.kind === 'loading'}>
      <div className="calendar-scroll" ref={scrollRef}>
        <div className="calendar-grid" style={{ gridTemplateColumns: `60px repeat(${days.length}, minmax(0, 1fr))` }}>
          <div className="calendar-corner" />
          {days.map((day) => (
            <DayHead key={day.toISOString()} day={day} today={sameDay(day, now)} />
          ))}

          <div className="calendar-hours" style={{ height: GRID_HEIGHT }}>
            {hours.map((h) => (
              <span key={h} style={{ top: h * HOUR_HEIGHT + 2 }}>
                {String(h).padStart(2, '0')}:00
              </span>
            ))}
            {showsToday && (
              <span className="calendar-now-label" style={{ top: nowTop - 11 }}>
                {formatClock(now)}
              </span>
            )}
          </div>
          {days.map((day) => {
            const today = sameDay(day, now)
            const dayEntries = laneLayout(entries.filter((e) => sameDay(e.start, day)))
            return (
              <div
                key={day.toISOString()}
                className={today ? 'calendar-column calendar-column-today' : 'calendar-column'}
                style={{ height: GRID_HEIGHT }}
              >
                {dayEntries.map(({ entry, lane, lanes }) => (
                  <GridEvent key={entry.meeting.id} entry={entry} lane={lane} lanes={lanes} />
                ))}
                {schedule.kind === 'loaded' && dayEntries.length === 0 && (
                  <span className="calendar-free" style={{ top: (WORKDAY_START_HOUR + 2) * HOUR_HEIGHT }}>
                    Không có lịch họp
                  </span>
                )}
                {today && <span className="calendar-now-line" style={{ top: nowTop }} />}
              </div>
            )
          })}
        </div>
      </div>
      <Legend />
    </div>
  )
}

function GridEvent({ entry, lane, lanes }: { entry: ScheduleEntry; lane: number; lanes: number }) {
  const top = minutesOfDay(entry.start) * (HOUR_HEIGHT / 60)
  // A meeting running past midnight stops at the bottom of its own day.
  const bottom = sameDay(entry.start, entry.end) ? minutesOfDay(entry.end) * (HOUR_HEIGHT / 60) : GRID_HEIGHT
  const height = Math.max(MIN_EVENT_HEIGHT, bottom - top)
  const { meeting } = entry
  const tall = height >= 72
  // Back from the meeting lands on this week, in this view.
  const back = backState('Lịch làm việc', useLocation())

  return (
    <Link
      to={routes.meeting(meeting.id)}
      state={back}
      className={`cal-event cal-event-${STATUS_TONE[meeting.status]}${tall ? '' : ' cal-event-short'}`}
      style={{ top, height, left: `calc(${(lane / lanes) * 100}% + 5px)`, width: `calc(${100 / lanes}% - 10px)` }}
      title={`${meeting.title} · ${timeRange(entry)}`}
    >
      <span className="cal-event-time">
        {timeRange(entry)}
        {meeting.sensitivity === 'Secret' && (
          <span className="cal-event-secret">
            <Icon name="lock" size={11} />
            Mật
          </span>
        )}
      </span>
      <span className="cal-event-title">{meeting.title}</span>
      {tall && <span className="cal-event-place">{placeLabel(entry.meeting)}</span>}
    </Link>
  )
}

function WeekList({ days, now, schedule }: { days: Date[]; now: Date; schedule: WeekSchedule }) {
  const entries = schedule.kind === 'loaded' ? schedule.entries : []

  return (
    <div className="agenda" aria-busy={schedule.kind === 'loading'}>
      {days.map((day) => {
        const today = sameDay(day, now)
        const dayEntries = entries.filter((e) => sameDay(e.start, day))
        return (
          <section key={day.toISOString()} className="agenda-day" aria-label={dayLabel(day)}>
            <header className="agenda-day-head">
              {today && <span className="agenda-today">Hôm nay</span>}
              <span className={today ? 'agenda-weekday agenda-weekday-muted' : 'agenda-weekday'}>
                {WEEKDAYS[day.getDay()]}
              </span>
              <span className="agenda-date">{formatDay(day)}</span>
              {dayEntries.length > 0 && <span className="agenda-count">{dayEntries.length} cuộc họp</span>}
            </header>

            {schedule.kind === 'loading' ? (
              <div className="agenda-row">
                <span className="agenda-time" />
                <div className="agenda-card agenda-card-skeleton">
                  <span className="skeleton" />
                  <span className="skeleton" />
                </div>
              </div>
            ) : schedule.kind === 'loaded' && dayEntries.length === 0 ? (
              <div className="agenda-row">
                <span className="agenda-time" />
                <p className="agenda-empty">Không có cuộc họp</p>
              </div>
            ) : (
              dayEntries.map((entry) => <AgendaItem key={entry.meeting.id} entry={entry} />)
            )}
          </section>
        )
      })}
    </div>
  )
}

function AgendaItem({ entry }: { entry: ScheduleEntry }) {
  const { meeting } = entry
  const tone = STATUS_TONE[meeting.status]
  const back = backState('Lịch làm việc', useLocation())
  return (
    <div className={`agenda-row agenda-${tone}`}>
      <span className="agenda-time">
        <b>{formatClock(entry.start)}</b>
        <span>{formatClock(entry.end)}</span>
      </span>
      <span className="agenda-dot" aria-hidden="true" />
      <article className="agenda-card">
        <div className="agenda-card-main">
          <h2 className={meeting.status === 'Rejected' ? 'agenda-title agenda-title-struck' : 'agenda-title'}>
            <Link to={routes.meeting(meeting.id)} state={back}>
              {meeting.title}
            </Link>
          </h2>
          <p className="agenda-facts">
            <span>
              <Icon name={meeting.mode === 'InPerson' ? 'mapPin' : 'video'} size={15} />
              {placeLabel(entry.meeting)}
            </span>
          </p>
          <p className="agenda-chips">
            <StatusBadge status={meeting.status} />
            <SensitivityBadge sensitivity={meeting.sensitivity} />
          </p>
        </div>
        <Link className="btn btn-outline btn-sm agenda-open" to={routes.meeting(meeting.id)} state={back}>
          Xem chi tiết
          <Icon name="chevronRight" size={16} />
        </Link>
      </article>
    </div>
  )
}

function DayHead({ day, today }: { day: Date; today: boolean }) {
  return (
    <div className={today ? 'calendar-day calendar-today' : 'calendar-day'}>
      <span className="calendar-weekday">
        {WEEKDAYS[day.getDay()]}
        {today && ' · Hôm nay'}
      </span>
      <span className="calendar-date">
        {day.getDate()}/{day.getMonth() + 1}
      </span>
    </div>
  )
}

/** What each colour means — the states differ by edge style too, not by colour alone. */
function Legend() {
  return (
    <div className="calendar-legend">
      <span className="legend-live">Đang diễn ra</span>
      <span className="legend-blue">Đã công bố</span>
      <span className="legend-emerald">Đã duyệt</span>
      <span className="legend-amber">Chờ duyệt</span>
      <span className="legend-slate">Nháp</span>
      <span className="legend-ended">Đã kết thúc</span>
      <span className="legend-red">Bị từ chối</span>
    </div>
  )
}

/**
 * Side-by-side lanes for meetings that overlap on the same day: each meeting takes the first lane
 * free at its start, and every meeting in a cluster of overlaps shares that cluster's lane count.
 */
function laneLayout(entries: ScheduleEntry[]) {
  const out: { entry: ScheduleEntry; lane: number; lanes: number }[] = []
  let cluster: { entry: ScheduleEntry; lane: number }[] = []
  let laneEnds: number[] = []
  let clusterEnd = -Infinity
  const flush = () => {
    const lanes = Math.max(1, laneEnds.length)
    for (const item of cluster) out.push({ ...item, lanes })
    cluster = []
    laneEnds = []
  }
  for (const entry of entries) {
    const start = entry.start.getTime()
    if (start >= clusterEnd) flush()
    let lane = laneEnds.findIndex((end) => end <= start)
    if (lane === -1) lane = laneEnds.push(0) - 1
    laneEnds[lane] = entry.end.getTime()
    clusterEnd = Math.max(clusterEnd, entry.end.getTime())
    cluster.push({ entry, lane })
  }
  flush()
  return out
}

function timeRange(entry: ScheduleEntry) {
  return `${formatClock(entry.start)} – ${formatClock(entry.end)}`
}

function minutesOfDay(date: Date) {
  return date.getHours() * 60 + date.getMinutes()
}

/** The current time, refreshed every minute so the red line moves. */
function useNow() {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 60_000)
    return () => clearInterval(timer)
  }, [])
  return now
}

/** The Monday of the week in `?week=`, or of this week when it is missing or unreadable. */
function readMonday(value: string | null, now: Date) {
  if (value && DATE.test(value)) {
    const [y, m, d] = value.split('-').map(Number)
    const date = new Date(y, m - 1, d)
    if (!Number.isNaN(date.getTime())) return startOfWeek(date)
  }
  return startOfWeek(now)
}

function startOfWeek(date: Date) {
  const day = new Date(date.getFullYear(), date.getMonth(), date.getDate())
  // getDay(): Sunday is 0. The Vietnamese week starts on Monday.
  return addDays(day, -((day.getDay() + 6) % 7))
}

function addDays(date: Date, n: number) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + n)
}

function endOfDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59, 999)
}

function sameDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()
}

function toDateInput(date: Date) {
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${m}-${d}`
}

/** "06/10/2026" */
function formatDay(date: Date) {
  return `${String(date.getDate()).padStart(2, '0')}/${String(date.getMonth() + 1).padStart(2, '0')}/${date.getFullYear()}`
}

function dayLabel(date: Date) {
  return `${WEEKDAYS[date.getDay()]}, ${formatDay(date)}`
}

/** "28/9 – 2/10/2026"; the year only once unless the week straddles New Year. */
function rangeLabel(first: Date, last: Date) {
  const head = `${first.getDate()}/${first.getMonth() + 1}`
  const tail = `${last.getDate()}/${last.getMonth() + 1}/${last.getFullYear()}`
  return first.getFullYear() === last.getFullYear() ? `${head} – ${tail}` : `${head}/${first.getFullYear()} – ${tail}`
}

function formatClock(date: Date) {
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}
