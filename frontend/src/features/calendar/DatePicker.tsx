import { useEffect, useRef, useState } from 'react'
import { Icon } from '../../components/Icon'
import { Select } from '../../components/Select'
import { addDays, addMonths, dayLabel, monthGridDays, sameDay, startOfMonth, toDateInput } from './dates'

const PANEL_ID = 'calendar-date-picker'
const WEEKDAY_SHORT = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']
const MONTH_OPTIONS = Array.from({ length: 12 }, (_, i) => ({ value: String(i), label: `Tháng ${i + 1}` }))
/** Years offered around the current one; the month on screen is always added when it lies outside. */
const YEARS_AROUND = 5

/**
 * "Chọn ngày nhanh": the range title opens a small month calendar to jump to any day. Browsing it
 * changes nothing on the page; picking a day hands it to `onPick`, which opens the week or month
 * holding it. Non-modal, like the filter: an outside click or Escape closes it.
 */
export function DatePicker({
  label,
  now,
  anchor,
  viewed,
  onPick,
}: {
  label: string
  now: Date
  /** A day in the month the calendar opens on. */
  anchor: Date
  /** The days on screen in a week view, drawn as a band; empty in the month view. */
  viewed: Date[]
  onPick: (day: Date) => void
}) {
  const [open, setOpen] = useState(false)
  const [shown, setShown] = useState(() => startOfMonth(anchor))
  const rootRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!open) return
    function onPointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false)
    }
    function onKeyDown(event: KeyboardEvent) {
      // A month or year menu that is open takes Escape for itself first.
      if (event.key !== 'Escape' || event.defaultPrevented) return
      setOpen(false)
      buttonRef.current?.focus()
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  function toggle() {
    if (!open) setShown(startOfMonth(anchor))
    setOpen(!open)
  }

  function pick(day: Date) {
    setOpen(false)
    buttonRef.current?.focus()
    onPick(day)
  }

  const year = shown.getFullYear()
  const firstYear = Math.min(now.getFullYear() - YEARS_AROUND, year)
  const lastYear = Math.max(now.getFullYear() + YEARS_AROUND, year)
  const yearOptions = Array.from({ length: lastYear - firstYear + 1 }, (_, i) => {
    const value = String(firstYear + i)
    return { value, label: value }
  })
  const inView = new Set(viewed.map(toDateInput))
  const days = monthGridDays(shown)

  return (
    <div className="date-pick" ref={rootRef}>
      <h1 className="calendar-range">
        <button
          ref={buttonRef}
          type="button"
          className="calendar-range-button"
          aria-expanded={open}
          aria-controls={open ? PANEL_ID : undefined}
          title="Chọn ngày nhanh"
          onClick={toggle}
        >
          {label}
          <Icon name="chevronDown" size={18} />
        </button>
      </h1>

      {open && (
        <div className="date-pick-panel" id={PANEL_ID} role="group" aria-label="Chọn ngày nhanh">
          <p className="date-pick-title">Chọn ngày nhanh</p>
          <div className="date-pick-head">
            <Select
              value={String(shown.getMonth())}
              options={MONTH_OPTIONS}
              label="Tháng"
              onChange={(m) => setShown(new Date(year, Number(m), 1))}
            />
            <Select
              value={String(year)}
              options={yearOptions}
              label="Năm"
              onChange={(y) => setShown(new Date(Number(y), shown.getMonth(), 1))}
            />
            <button
              type="button"
              className="icon-button"
              aria-label="Tháng trước"
              title="Tháng trước"
              onClick={() => setShown(addMonths(shown, -1))}
            >
              <Icon name="chevronLeft" size={18} />
            </button>
            <button
              type="button"
              className="icon-button"
              aria-label="Tháng sau"
              title="Tháng sau"
              onClick={() => setShown(addMonths(shown, 1))}
            >
              <Icon name="chevronRight" size={18} />
            </button>
          </div>

          <div className="date-pick-grid">
            {WEEKDAY_SHORT.map((weekday) => (
              <span key={weekday} className="date-pick-weekday" aria-hidden="true">
                {weekday}
              </span>
            ))}
            {days.map((day, i) => {
              const viewedDay = inView.has(toDateInput(day))
              const today = sameDay(day, now)
              // The band over the days on screen is rounded where it starts and ends in each row.
              const cls = [
                'date-pick-day',
                day.getMonth() !== shown.getMonth() ? 'date-pick-outside' : '',
                today ? 'date-pick-today' : '',
                viewedDay ? 'date-pick-viewed' : '',
                viewedDay && (i % 7 === 0 || !inView.has(toDateInput(addDays(day, -1)))) ? 'date-pick-start' : '',
                viewedDay && (i % 7 === 6 || !inView.has(toDateInput(addDays(day, 1)))) ? 'date-pick-end' : '',
              ]
              return (
                <button
                  key={day.toISOString()}
                  type="button"
                  className={cls.filter(Boolean).join(' ')}
                  aria-label={`${dayLabel(day)}${today ? ', hôm nay' : ''}`}
                  aria-current={today ? 'date' : undefined}
                  onClick={() => pick(day)}
                >
                  <span className="date-pick-num">{day.getDate()}</span>
                </button>
              )
            })}
          </div>

          <div className="date-pick-legend">
            <span>
              <i className="date-pick-swatch-today" aria-hidden="true" />
              Hôm nay
            </span>
            {viewed.length > 0 && (
              <span>
                <i className="date-pick-swatch-viewed" aria-hidden="true" />
                Tuần đang xem
              </span>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
