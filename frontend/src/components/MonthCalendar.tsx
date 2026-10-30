import { addDays, addMonths, dayLabel, monthGridDays, sameDay, toDateInput } from '../features/calendar/dates'
import { Icon } from './Icon'
import { Select } from './Select'

const WEEKDAY_SHORT = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']
const MONTH_OPTIONS = Array.from({ length: 12 }, (_, i) => ({ value: String(i), label: `Tháng ${i + 1}` }))
/** Years offered around the current one; the month on screen is always added when it lies outside. */
const YEARS_AROUND = 5

/**
 * One month, Monday first, with month and year menus and previous/next buttons. Shared by the
 * calendar's "Chọn ngày nhanh" and the date field of a form, so both read the same way.
 */
export function MonthCalendar({
  shown,
  onShow,
  now,
  selected,
  viewed = [],
  onPick,
}: {
  /** The first day of the month on screen. */
  shown: Date
  onShow: (month: Date) => void
  now: Date
  /** The day the field holds, drawn filled. */
  selected?: Date | null
  /** The days on screen in a week view, drawn as a band. */
  viewed?: Date[]
  onPick: (day: Date) => void
}) {
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
    <>
      <div className="date-pick-head">
        <Select
          value={String(shown.getMonth())}
          options={MONTH_OPTIONS}
          label="Tháng"
          onChange={(m) => onShow(new Date(year, Number(m), 1))}
        />
        <Select
          value={String(year)}
          options={yearOptions}
          label="Năm"
          onChange={(y) => onShow(new Date(Number(y), shown.getMonth(), 1))}
        />
        <button
          type="button"
          className="icon-button"
          aria-label="Tháng trước"
          data-tooltip="Tháng trước"
          onClick={() => onShow(addMonths(shown, -1))}
        >
          <Icon name="chevronLeft" size={18} />
        </button>
        <button
          type="button"
          className="icon-button"
          aria-label="Tháng sau"
          data-tooltip="Tháng sau"
          onClick={() => onShow(addMonths(shown, 1))}
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
          const picked = !!selected && sameDay(day, selected)
          // The band over the days on screen is rounded where it starts and ends in each row.
          const cls = [
            'date-pick-day',
            day.getMonth() !== shown.getMonth() ? 'date-pick-outside' : '',
            today ? 'date-pick-today' : '',
            picked ? 'date-pick-selected' : '',
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
              aria-pressed={selected === undefined ? undefined : picked}
              onClick={() => onPick(day)}
            >
              <span className="date-pick-num">{day.getDate()}</span>
            </button>
          )
        })}
      </div>
    </>
  )
}
