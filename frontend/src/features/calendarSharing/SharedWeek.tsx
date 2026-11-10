import { useEffect, useRef, useState } from 'react'
import { getSharedWeek } from '../../api/calendarSharing'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { SegmentedControl } from '../../components/SegmentedControl'
import { errorMessage } from '../../lib/errors'
import type { SharedWeek as Week, SharedWeekDay, SharedWeekPerson } from '../../types/calendarSharing'
import { Person } from '../participants/Person'
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

/**
 * One person's day as a bar from the start to the end of the working day: busy stretches in the person's colour, the
 * rest free. A day outside the share is hatched "Chưa biết" — never drawn as free (BR-415).
 */
function DayBar({
  day,
  colour,
  span,
}: {
  day: SharedWeekDay | undefined
  colour: string
  span: { from: number; to: number }
}) {
  if (!day || !day.known)
    return (
      <div className="week-bar week-bar-unknown" data-tooltip="Ngoài thời gian chia sẻ: không biết bận hay rảnh">
        Chưa biết
      </div>
    )
  const length = span.to - span.from
  const text = day.busy.map((b) => `${clock(b.start)}–${clock(b.end)}`).join(', ')
  return (
    <div className="week-bar week-bar-hours" role="img" aria-label={day.busy.length ? `Bận ${text}` : 'Rảnh cả ngày'}>
      {day.busy.map((b) => {
        const left = ((minutes(clock(b.start)) - span.from) / length) * 100
        const width = ((minutes(clock(b.end)) - minutes(clock(b.start))) / length) * 100
        return (
          <span
            key={b.start}
            className="week-busy"
            style={{ left: `${left}%`, width: `${width}%`, background: `var(${colour})` }}
            data-tooltip={`Bận ${clock(b.start)}–${clock(b.end)}`}
          />
        )
      })}
    </div>
  )
}

/**
 * UC-85c — busy hours of everyone who shared with the caller, one week (Mon–Fri, the working day), one row a person.
 * Rows rather than AI-eCabinet's day columns: each person's own row has room to say "Chưa biết" for a day outside the
 * share, and to stay listed as "Đã dừng" when a share stopped, without ever looking free. No meeting is named (BR-427).
 * On a phone one day shows at a time, with the busy times written under each bar, since a tap gives no tooltip.
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

  const shownDays = phone ? [days[dayIndex]] : days
  return (
    <>
      {toolbar}
      {phone && (
        <SegmentedControl
          label="Ngày trong tuần"
          value={String(dayIndex)}
          options={days.map((d, i) => ({ value: String(i), label: `${WEEKDAY_SHORT[i]} ${d.slice(8, 10)}` }))}
          onChange={(v) => setDayIndex(Number(v))}
        />
      )}
      <div className="card week-card" aria-busy={loading}>
        <table className={`week-table${loading && data ? ' data-table-stale' : ''}`}>
          <thead>
            <tr>
              <th className="week-person-col">Người chia sẻ</th>
              {shownDays.map((d) => (
                <th key={d} className={d === vietnamToday() ? 'week-today' : undefined}>
                  {WEEKDAY_SHORT[days.indexOf(d)]}{' '}
                  <span className="week-date">
                    {d.slice(8, 10)}/{d.slice(5, 7)}
                  </span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {!data
              ? [0, 1, 2].map((i) => (
                  <tr key={i}>
                    <td colSpan={shownDays.length + 1}>
                      <span className="skeleton" />
                    </td>
                  </tr>
                ))
              : data.people.map((person, i) => (
                  <PersonRow
                    key={person.shareId}
                    person={person}
                    colour={PERSON_COLOURS[i % PERSON_COLOURS.length]}
                    days={shownDays}
                    span={span}
                    phone={phone}
                  />
                ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function PersonRow({
  person,
  colour,
  days,
  span,
  phone,
}: {
  person: SharedWeekPerson
  colour: string
  days: string[]
  span: { from: number; to: number }
  phone: boolean
}) {
  const stopped = person.status === 'Stopped'
  return (
    <tr className={stopped ? 'week-row-stopped' : undefined}>
      <th scope="row" className="week-person-col">
        <span className="week-person">
          <span
            className="week-swatch"
            style={{ background: stopped ? 'var(--line)' : `var(${colour})` }}
            aria-hidden="true"
          />
          <Person name={person.owner.fullName} email={null} />
        </span>
        <span className="week-person-meta">
          {stopped ? (
            <>
              Đã dừng <InfoTip text={MSG439_STOPPED} />
            </>
          ) : (
            `${LEVEL_LABEL[person.level]} · đến ${person.endDate.slice(8, 10)}/${person.endDate.slice(5, 7)}`
          )}
        </span>
      </th>
      {stopped ? (
        <td colSpan={days.length}>
          <div className="week-bar week-bar-unknown">Không còn xem được</div>
        </td>
      ) : (
        days.map((d) => {
          const day = person.days.find((x) => x.date === d)
          return (
            <td key={d}>
              <DayBar day={day} colour={colour} span={span} />
              {phone && day?.known && (
                <span className="week-busy-text">
                  {day.busy.length
                    ? day.busy.map((b) => `${clock(b.start)}–${clock(b.end)}`).join(', ')
                    : 'Rảnh cả ngày'}
                </span>
              )}
            </td>
          )
        })
      )}
    </tr>
  )
}
