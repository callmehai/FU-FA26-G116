import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { Icon } from '../../components/Icon'
import { Select } from '../../components/Select'
import { backState } from '../../lib/backTarget'
import { routes } from '../../lib/routes'
import { useAuth } from '../auth/useAuth'
import { placeLabel } from '../meetings/place'
import { MEETING_STATUS_LABEL } from '../../types/meeting'
import { SensitivityBadge, StatusBadge } from '../meetings/StatusBadge'
import { STATUS_TONE } from '../meetings/statusTone'
import { CalendarFilter } from './CalendarFilter'
import { NO_FILTER, activeGroups, matchesFilter, readFilter, writeFilter, type CalendarFilterValue } from './filter'
import { useSchedule, type Schedule, type ScheduleEntry } from './useSchedule'

/** The grid covers the whole day; it opens scrolled to the part that matters (see WeekGrid). */
const HOURS_PER_DAY = 24
const HOUR_HEIGHT = 56
const GRID_HEIGHT = HOURS_PER_DAY * HOUR_HEIGHT
/** Where an empty week, or a week whose first meeting starts early, opens. */
const WORKDAY_START_HOUR = 8
/** A meeting shorter than this would not fit its own time line. */
const MIN_EVENT_HEIGHT = 30

/** A month cell lists this many meetings; the rest fold into "+N cuộc họp". */
const MONTH_CELL_EVENTS = 3

const WEEKDAYS = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy']

type Span = 'work' | 'full' | 'month'
const SPAN_OPTIONS: { value: Span; label: string }[] = [
  { value: 'work', label: 'Tuần làm việc' },
  { value: 'full', label: 'Cả tuần' },
  { value: 'month', label: 'Tháng' },
]

type Mode = 'calendar' | 'list'

const DATE = /^\d{4}-\d{2}-\d{2}$/
const MONTH = /^\d{4}-\d{2}$/

/**
 * "Lịch làm việc" (ADR-0012, SCRUM-47): the meetings of a week or a month, Monday first, as a time
 * grid, a month grid or a list grouped by day, narrowed by the filter. The range, the span, the view
 * and the filter live in the URL, so a reload or a shared link opens the same thing.
 */
export function CalendarPage() {
  const { can } = useAuth()
  const [params, setParams] = useSearchParams()
  const now = useNow()
  const [attempt, setAttempt] = useState(0)
  const spanParam = params.get('span')
  const span: Span = spanParam === 'full' || spanParam === 'month' ? spanParam : 'work'
  const mode: Mode = params.get('mode') === 'list' ? 'list' : 'calendar'
  const monday = readMonday(params.get('week'), now)
  const month = readMonth(params.get('month'), now)
  const filter = readFilter(params)
  const byMonth = span === 'month'
  // The month grid runs from the Monday before the 1st to the Sunday after the last day; the list
  // shows the month's own days. Both read the grid's range, so switching view does not reload.
  const days = byMonth
    ? Array.from({ length: daysInMonth(month) }, (_, i) => addDays(month, i))
    : Array.from({ length: span === 'full' ? 7 : 5 }, (_, i) => addDays(monday, i))
  const gridDays = byMonth ? monthGridDays(month) : days
  const schedule = useSchedule(gridDays[0], endOfDay(gridDays[gridDays.length - 1]), attempt)
  // What this view draws: a meeting sits on its start day, so one that only runs into the range, or
  // one on the grid's padding days in the month list, is not on screen and must not be counted.
  const shownDays = new Set((mode === 'list' ? days : gridDays).map(toDateInput))
  const inView = schedule.kind === 'loaded' ? schedule.entries.filter((e) => shownDays.has(toDateInput(e.start))) : null
  const visible: Schedule = inView
    ? { kind: 'loaded', entries: inView.filter((e) => matchesFilter(e, filter)) }
    : schedule

  /** `push` keeps the current view in history: Back from a week opened on the month grid returns to it. */
  function go(
    next: { week?: Date; month?: Date; span?: Span; mode?: Mode; filter?: CalendarFilterValue },
    push = false,
  ) {
    const out = new URLSearchParams(params)
    const nextSpan = next.span ?? span
    out.delete('week')
    out.delete('month')
    if (nextSpan === 'month') {
      const nextMonth = next.month ?? month
      if (!sameMonth(nextMonth, now)) out.set('month', toMonthInput(nextMonth))
    } else {
      const week = next.week ?? monday
      if (!sameDay(week, startOfWeek(now))) out.set('week', toDateInput(week))
    }
    if (nextSpan === 'work') out.delete('span')
    else out.set('span', nextSpan)
    const nextMode = next.mode ?? mode
    if (nextMode === 'calendar') out.delete('mode')
    else out.set('mode', nextMode)
    // Rewritten on every move, so unreadable values in a hand-edited link do not linger.
    writeFilter(out, next.filter ?? filter)
    setParams(out, { replace: !push })
  }

  function changeSpan(next: Span) {
    if (next === 'month') {
      // The month holding most of the week on screen: the one its Thursday falls in.
      go({ span: next, month: byMonth ? month : startOfMonth(addDays(monday, 3)) })
    } else {
      // This week if the month is the current one, else its first week by the same Thursday rule —
      // the week holding the 4th — so month → week → month comes back to the same month.
      go({ span: next, week: byMonth ? startOfWeek(sameMonth(month, now) ? now : addDays(month, 3)) : monday })
    }
  }

  /** From the month grid: the week holding `day`, all seven days when `day` is a weekend. */
  function openWeek(day: Date) {
    const weekend = day.getDay() === 0 || day.getDay() === 6
    go({ span: weekend ? 'full' : 'work', week: startOfWeek(day), mode: 'calendar' }, true)
  }

  const unit = byMonth ? 'Tháng' : 'Tuần'
  const step = (n: number) => (byMonth ? go({ month: addMonths(month, n) }) : go({ week: addDays(monday, 7 * n) }))

  return (
    <>
      <div className="calendar-toolbar">
        <button
          type="button"
          className="btn btn-outline"
          onClick={() => (byMonth ? go({ month: startOfMonth(now) }) : go({ week: startOfWeek(now) }))}
        >
          Hôm nay
        </button>
        <div className="button-pair">
          <button
            type="button"
            className="btn btn-outline"
            onClick={() => step(-1)}
            aria-label={`${unit} trước`}
            title={`${unit} trước`}
          >
            <Icon name="chevronLeft" size={18} />
          </button>
          <button
            type="button"
            className="btn btn-outline"
            onClick={() => step(1)}
            aria-label={`${unit} sau`}
            title={`${unit} sau`}
          >
            <Icon name="chevronRight" size={18} />
          </button>
        </div>
        <h1 className="calendar-range">
          {byMonth
            ? `Tháng ${month.getMonth() + 1}/${month.getFullYear()}`
            : rangeLabel(days[0], days[days.length - 1])}
        </h1>

        <div className="calendar-toolbar-end">
          <div className="view-toggle" role="group" aria-label="Chế độ xem">
            {/* Below 1400px each button shows only its icon (global.css); the name stays on aria-label. */}
            <button
              type="button"
              aria-pressed={mode === 'calendar'}
              aria-label="Lịch"
              title="Lịch"
              onClick={() => go({ mode: 'calendar' })}
            >
              <span className="view-toggle-icon">
                <Icon name="calendar" size={18} />
              </span>
              <span className="view-toggle-label">Lịch</span>
            </button>
            <button
              type="button"
              aria-pressed={mode === 'list'}
              aria-label="Danh sách"
              title="Danh sách"
              onClick={() => go({ mode: 'list' })}
            >
              <span className="view-toggle-icon">
                <Icon name="list" size={18} />
              </span>
              <span className="view-toggle-label">Danh sách</span>
            </button>
          </div>
          <Select value={span} options={SPAN_OPTIONS} onChange={changeSpan} label="Kiểu lịch" />
          <CalendarFilter value={filter} entries={inView} onChange={(next) => go({ filter: next })} />
          {can('MeetingCreator') && (
            <Link className="btn btn-primary" to={routes.meetingNew}>
              <Icon name="plus" size={18} />
              Tạo cuộc họp
            </Link>
          )}
        </div>
      </div>

      <ScheduleNotice
        schedule={schedule}
        filteredOut={
          activeGroups(filter) > 0 && !!inView?.length && visible.kind === 'loaded' && visible.entries.length === 0
        }
        onRetry={() => setAttempt((n) => n + 1)}
        onClearFilter={() => go({ filter: NO_FILTER })}
      />

      {mode === 'list' ? (
        <DayList days={days} now={now} schedule={visible} />
      ) : byMonth ? (
        <MonthGrid month={month} days={gridDays} now={now} schedule={visible} onOpenWeek={openWeek} />
      ) : (
        <WeekGrid days={days} now={now} schedule={visible} />
      )}
    </>
  )
}

/** Why the range shows nothing, when it is not simply free. */
function ScheduleNotice({
  schedule,
  filteredOut,
  onRetry,
  onClearFilter,
}: {
  schedule: Schedule
  /** The filter hides every meeting in the range. */
  filteredOut: boolean
  onRetry: () => void
  onClearFilter: () => void
}) {
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
  if (filteredOut) {
    return (
      <div className="alert alert-info page-alert calendar-note" role="status">
        <p>Không có cuộc họp nào khớp bộ lọc trong khoảng này.</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={onClearFilter}>
          Bỏ lọc
        </button>
      </div>
    )
  }
  return null
}

function WeekGrid({ days, now, schedule }: { days: Date[]; now: Date; schedule: Schedule }) {
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

/**
 * The month grid (SCRUM-47): seven columns Monday first, one row per week. Each day lists its first
 * meetings by start time; the day number and "+N cuộc họp" open that week on the time grid.
 */
function MonthGrid({
  month,
  days,
  now,
  schedule,
  onOpenWeek,
}: {
  month: Date
  days: Date[]
  now: Date
  schedule: Schedule
  onOpenWeek: (day: Date) => void
}) {
  const entries = schedule.kind === 'loaded' ? schedule.entries : []

  return (
    <div className="calendar" aria-busy={schedule.kind === 'loading'}>
      <div className="calendar-scroll month-scroll">
        <div className="month" style={{ gridTemplateRows: `auto repeat(${days.length / 7}, minmax(120px, 1fr))` }}>
          {days.slice(0, 7).map((day) => (
            <div key={day.getDay()} className="month-weekday">
              {WEEKDAYS[day.getDay()]}
            </div>
          ))}
          {days.map((day) => {
            const today = sameDay(day, now)
            const weekday = day.getDay()
            const dayEntries = entries.filter((e) => sameDay(e.start, day))
            const shown = dayEntries.slice(0, MONTH_CELL_EVENTS)
            const hidden = dayEntries.length - shown.length
            const cls = [
              'month-cell',
              weekday === 0 || weekday === 6 ? 'month-weekend' : '',
              day.getMonth() !== month.getMonth() ? 'month-outside' : '',
              today ? 'month-today' : '',
            ]
            return (
              <div
                key={day.toISOString()}
                className={cls.filter(Boolean).join(' ')}
                role="group"
                aria-label={dayLabel(day)}
              >
                {/* "+N shares the day number's line, so a busy day is no taller than a full one. */}
                <div className="month-cell-head">
                  <button
                    type="button"
                    className="month-day"
                    onClick={() => onOpenWeek(day)}
                    title="Xem tuần này"
                    aria-label={`${dayLabel(day)}${today ? ', hôm nay' : ''}: xem tuần`}
                    aria-current={today ? 'date' : undefined}
                  >
                    {day.getDate()}
                  </button>
                  {hidden > 0 && (
                    <button
                      type="button"
                      className="month-more"
                      onClick={() => onOpenWeek(day)}
                      aria-label={`${hidden} cuộc họp nữa ngày ${formatDay(day)}: xem tuần`}
                      title={`${hidden} cuộc họp nữa`}
                    >
                      +{hidden}
                      <span className="month-more-word"> cuộc họp</span>
                    </button>
                  )}
                </div>
                {shown.map((entry) => (
                  <MonthEvent key={entry.meeting.id} entry={entry} />
                ))}
              </div>
            )
          })}
        </div>
      </div>
      <Legend />
    </div>
  )
}

function MonthEvent({ entry }: { entry: ScheduleEntry }) {
  const { meeting } = entry
  // Back from the meeting lands on this month, in this view.
  const back = backState('Lịch làm việc', useLocation())
  return (
    <Link
      to={routes.meeting(meeting.id)}
      state={back}
      className={`month-event cal-event-${STATUS_TONE[meeting.status]}`}
      title={`${meeting.title} · ${timeRange(entry)} · ${MEETING_STATUS_LABEL[meeting.status]}`}
    >
      <span className="month-event-time">{formatClock(entry.start)}</span>
      <span className="month-event-title">{meeting.title}</span>
      {meeting.sensitivity === 'Secret' && (
        <span className="cal-event-secret">
          <Icon name="lock" size={11} />
          Mật
        </span>
      )}
    </Link>
  )
}

function DayList({ days, now, schedule }: { days: Date[]; now: Date; schedule: Schedule }) {
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

/** The 1st of the month in `?month=`, or of this month when it is missing or unreadable. */
function readMonth(value: string | null, now: Date) {
  if (value && MONTH.test(value)) {
    const [y, m] = value.split('-').map(Number)
    const date = new Date(y, m - 1, 1)
    // Rejects month 13 and two-digit years, which Date would roll over or map to 19xx.
    if (date.getFullYear() === y && date.getMonth() === m - 1) return date
  }
  return startOfMonth(now)
}

/** The Monday of the week in `?week=`, or of this week when it is missing or unreadable. */
function readMonday(value: string | null, now: Date) {
  if (value && DATE.test(value)) {
    const [y, m, d] = value.split('-').map(Number)
    const date = new Date(y, m - 1, d)
    // Rejects 31/02 and two-digit years, which Date would roll over or map to 19xx.
    if (date.getFullYear() === y && date.getMonth() === m - 1 && date.getDate() === d) return startOfWeek(date)
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

function startOfMonth(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), 1)
}

function addMonths(month: Date, n: number) {
  return new Date(month.getFullYear(), month.getMonth() + n, 1)
}

function daysInMonth(month: Date) {
  return new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate()
}

/** Whole weeks, Monday to Sunday, covering the month: five or six rows, four for a February starting on Monday. */
function monthGridDays(month: Date) {
  const first = startOfWeek(month)
  const weeks = Math.ceil((daysInMonth(month) + ((month.getDay() + 6) % 7)) / 7)
  return Array.from({ length: weeks * 7 }, (_, i) => addDays(first, i))
}

function sameMonth(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth()
}

function endOfDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59, 999)
}

function sameDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()
}

function toMonthInput(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
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
