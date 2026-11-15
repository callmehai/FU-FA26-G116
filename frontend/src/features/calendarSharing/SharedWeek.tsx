import { useEffect, useRef, useState } from 'react'
import { getSharedWeek } from '../../api/calendarSharing'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { SegmentedControl } from '../../components/SegmentedControl'
import { errorMessage } from '../../lib/errors'
import type { SharedWeek as Week, SharedWeekPerson } from '../../types/calendarSharing'
import { usePhone } from '../calendar/usePhone'
import { LEVEL_LABEL, MSG439_STOPPED, addDaysTo, periodText, vietnamToday } from './shareFormat'

/** One colour per person, kept off red (danger) and away from the brand blue of buttons. */
const PERSON_COLOURS = [
  '--chip-emerald-fg',
  '--chip-violet-fg',
  '--chip-amber-fg',
  '--chip-indigo-fg',
  '--chip-blue-fg',
  '--chip-slate-fg',
]
const WEEKDAY_SHORT = ['T2', 'T3', 'T4', 'T5', 'T6']

const minutes = (hhmm: string) => Number(hhmm.slice(0, 2)) * 60 + Number(hhmm.slice(3, 5))
/** "2026-11-09T09:00:00+07:00" → "09:00": the server sends Vietnam time, so the wall clock is in the string. */
const clock = (iso: string) => iso.slice(11, 16)

/** Monday of the week holding `day`, as the server counts it (Sunday belongs to the week before). */
function mondayOf(day: string) {
  const weekday = new Date(`${day}T00:00:00Z`).getUTCDay()
  return addDaysTo(day, weekday === 0 ? -6 : 1 - weekday)
}

interface Block {
  person: SharedWeekPerson
  colour: string
  from: number
  to: number
}

/**
 * Busy blocks of one day laid out as a meeting calendar does: blocks that overlap share the column side by side, each
 * a lane, so two people busy at once both show. Greedy lanes per cluster of overlapping blocks.
 */
function layoutDay(blocks: Block[]) {
  const sorted = [...blocks].sort((a, b) => a.from - b.from || b.to - a.to)
  const placed: { block: Block; lane: number; lanes: number }[] = []
  let cluster: { block: Block; lane: number; lanes: number }[] = []
  let clusterEnd = -1
  let laneEnds: number[] = []
  const close = () => {
    for (const item of cluster) item.lanes = laneEnds.length
    placed.push(...cluster)
    cluster = []
    laneEnds = []
  }
  for (const block of sorted) {
    if (block.from >= clusterEnd) close()
    let lane = laneEnds.findIndex((end) => end <= block.from)
    if (lane === -1) lane = laneEnds.push(block.to) - 1
    else laneEnds[lane] = block.to
    cluster.push({ block, lane, lanes: 0 })
    clusterEnd = Math.max(clusterEnd, block.to)
  }
  close()
  return placed
}

const HOUR_PX = 56

/**
 * UC-85c — busy hours of everyone who shared with the caller, one week (Mon–Fri, the working day), as AI-eCabinet's
 * shared week: the working hours down the side, a column a day, each busy stretch a block in its person's colour with
 * the name and time written in it, so free time is the empty space and people busy at once sit side by side. People
 * are chips above the grid, each one shown or hidden by a tap, and say their level and end date, or "Đã dừng" for a
 * stopped share. A day outside someone's share is "Chưa biết" under that day, never drawn as free (BR-415). No
 * meeting is named (BR-427). On a phone one day shows at a time.
 */
export function SharedWeek({
  week,
  onWeek,
  onAsk,
}: {
  /** Any day of the week to show, `YYYY-MM-DD`. */
  week: string
  onWeek: (day: string) => void
  onAsk: () => void
}) {
  const phone = usePhone()
  const [data, setData] = useState<Week | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [attempt, setAttempt] = useState(0)
  const [dayIndex, setDayIndex] = useState(() => Math.min(4, Math.max(0, new Date().getDay() - 1)))
  const [hidden, setHidden] = useState<ReadonlySet<string>>(() => new Set())
  const latest = useRef(0)

  useEffect(() => {
    const ticket = ++latest.current
    void Promise.resolve().then(async () => {
      setLoading(true)
      try {
        const next = await getSharedWeek(week)
        if (ticket !== latest.current) return
        setData(next)
        setError(null)
      } catch (err) {
        if (ticket === latest.current) setError(errorMessage(err))
      } finally {
        if (ticket === latest.current) setLoading(false)
      }
    })
  }, [week, attempt])

  const monday = mondayOf(week)
  const days = [0, 1, 2, 3, 4].map((i) => addDaysTo(monday, i))
  const thisWeek = mondayOf(vietnamToday()) === monday
  const span = data ? { from: minutes(data.dayStart), to: minutes(data.dayEnd) } : { from: 480, to: 1020 }

  const toolbar = (
    <div className="toolbar week-toolbar">
      <button type="button" className="btn btn-outline" onClick={() => onWeek(vietnamToday())} disabled={thisWeek}>
        Tuần này
      </button>
      <div className="week-nav">
        <button
          type="button"
          className="icon-button"
          aria-label="Tuần trước"
          onClick={() => onWeek(addDaysTo(monday, -7))}
        >
          <Icon name="chevronLeft" size={18} />
        </button>
        <button
          type="button"
          className="icon-button"
          aria-label="Tuần sau"
          onClick={() => onWeek(addDaysTo(monday, 7))}
        >
          <Icon name="chevronRight" size={18} />
        </button>
      </div>
      <h2 className="week-title">{periodText(days[0], days[4])}</h2>
      {data && (
        <span className="week-hours">
          {data.dayStart}–{data.dayEnd}
          <InfoTip text="Chỉ hiện giờ bận, không có tên hay nội dung cuộc họp. Họp Mật không bao giờ hiện." />
        </span>
      )}
    </div>
  )

  if (error && !data)
    return (
      <>
        {toolbar}
        <div className="alert alert-error page-alert" role="alert">
          <p>{error}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => setAttempt((n) => n + 1)}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      </>
    )

  if (data && data.people.length === 0)
    return (
      <>
        {toolbar}
        <div className="card share-empty">
          <p className="empty-title">Chưa ai chia sẻ lịch với bạn.</p>
          <button type="button" className="btn btn-primary" onClick={onAsk}>
            <Icon name="send" size={16} />
            Xin xem lịch
          </button>
        </div>
      </>
    )

  const people = (data?.people ?? []).map((person, i) => ({
    person,
    colour: PERSON_COLOURS[i % PERSON_COLOURS.length],
  }))
  const shown = people.filter(({ person }) => person.status === 'Effective' && !hidden.has(person.shareId))
  const hours = Array.from({ length: Math.ceil((span.to - span.from) / 60) }, (_, i) => span.from + i * 60)
  const height = ((span.to - span.from) / 60) * HOUR_PX
  const top = (m: number) => ((m - span.from) / 60) * HOUR_PX
  const toggle = (shareId: string) =>
    setHidden((set) => {
      const next = new Set(set)
      if (next.has(shareId)) next.delete(shareId)
      else next.add(shareId)
      return next
    })

  const shownDays = phone ? [days[dayIndex]] : days
  return (
    <>
      {toolbar}
      {data && (
        <div className="shared-people" role="group" aria-label="Người chia sẻ">
          {people.map(({ person, colour }) => {
            const stopped = person.status === 'Stopped'
            const on = !stopped && !hidden.has(person.shareId)
            return (
              <button
                key={person.shareId}
                type="button"
                className={`chip-toggle shared-person${stopped ? ' shared-person-stopped' : ''}`}
                aria-pressed={on}
                disabled={stopped}
                onClick={() => toggle(person.shareId)}
              >
                <span
                  className="week-swatch"
                  style={{ background: on ? `var(${colour})` : 'var(--line)' }}
                  aria-hidden="true"
                />
                <span className="shared-person-name">{person.owner.fullName}</span>
                <span className="shared-person-meta">
                  {stopped
                    ? 'Đã dừng'
                    : `${LEVEL_LABEL[person.level]} · đến ${person.endDate.slice(8, 10)}/${person.endDate.slice(5, 7)}`}
                </span>
              </button>
            )
          })}
          {people.some(({ person }) => person.status === 'Stopped') && <InfoTip text={MSG439_STOPPED} />}
        </div>
      )}
      {phone && (
        <SegmentedControl
          label="Ngày trong tuần"
          value={String(dayIndex)}
          options={days.map((d, i) => ({ value: String(i), label: `${WEEKDAY_SHORT[i]} ${d.slice(8, 10)}` }))}
          onChange={(v) => setDayIndex(Number(v))}
        />
      )}
      <div className={`card shared-grid${loading && data ? ' data-table-stale' : ''}`} aria-busy={loading}>
        <div className="shared-grid-head" style={{ gridTemplateColumns: `48px repeat(${shownDays.length}, minmax(0, 1fr))` }}>
          <span />
          {shownDays.map((d) => {
            const unknown = shown.filter(({ person }) => !person.days.find((x) => x.date === d)?.known)
            return (
              <div key={d} className={d === vietnamToday() ? 'shared-day shared-day-today' : 'shared-day'}>
                <span className="shared-day-name">
                  {WEEKDAY_SHORT[days.indexOf(d)]} {d.slice(8, 10)}/{d.slice(5, 7)}
                </span>
                {unknown.length > 0 && (
                  <span
                    className="shared-day-unknown"
                    data-tooltip={`Ngoài thời gian chia sẻ: ${unknown.map(({ person }) => person.owner.fullName).join(', ')}`}
                  >
                    Chưa biết: {unknown.map(({ person }) => person.owner.fullName).join(', ')}
                  </span>
                )}
              </div>
            )
          })}
        </div>
        <div className="shared-grid-body" style={{ gridTemplateColumns: `48px repeat(${shownDays.length}, minmax(0, 1fr))` }}>
          <div className="shared-hours" style={{ height }}>
            {hours.map((m) => (
              <span key={m} style={{ top: top(m) }}>
                {String(m / 60).padStart(2, '0')}:00
              </span>
            ))}
          </div>
          {shownDays.map((d) => {
            const blocks = shown.flatMap(({ person, colour }) =>
              (person.days.find((x) => x.date === d && x.known)?.busy ?? []).map((b) => ({
                person,
                colour,
                from: minutes(clock(b.start)),
                to: minutes(clock(b.end)),
              })),
            )
            // Nobody shown shares this day: it is unknown, not free, so it is never left white (BR-415).
            const unknown =
              shown.length > 0 && shown.every(({ person }) => !person.days.find((x) => x.date === d)?.known)
            return (
              <div
                key={d}
                className={unknown ? 'shared-column shared-column-unknown' : 'shared-column'}
                style={{ height, backgroundSize: `100% ${HOUR_PX}px` }}
                aria-label={`${WEEKDAY_SHORT[days.indexOf(d)]} ${d.slice(8, 10)}/${d.slice(5, 7)}`}
                role="group"
              >
                {!data && <span className="skeleton shared-skeleton" />}
                {unknown && <span className="shared-unknown-text">Chưa biết</span>}
                {layoutDay(blocks).map(({ block, lane, lanes }) => {
                  const range = `${pad(block.from)}–${pad(block.to)}`
                  const tall = block.to - block.from >= 45
                  return (
                    <div
                      key={`${block.person.shareId}-${block.from}`}
                      className={`shared-busy${tall ? '' : ' shared-busy-short'}`}
                      role="img"
                      aria-label={`${block.person.owner.fullName} bận ${range}`}
                      data-tooltip={`${block.person.owner.fullName} · bận ${range}`}
                      style={{
                        top: top(block.from) + 1,
                        height: Math.max(18, top(block.to) - top(block.from) - 2),
                        left: `calc(${(lane / lanes) * 100}% + 2px)`,
                        width: `calc(${100 / lanes}% - 4px)`,
                        borderColor: `var(${block.colour})`,
                        color: `var(${block.colour})`,
                        background: `color-mix(in srgb, var(${block.colour}) 12%, var(--surface))`,
                      }}
                    >
                      <b>{block.person.owner.fullName}</b>
                      <span>{range}</span>
                    </div>
                  )
                })}
              </div>
            )
          })}
        </div>
      </div>
    </>
  )
}

/** Minutes of the day → "HH:MM". */
function pad(m: number) {
  return `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`
}
