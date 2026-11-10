import { Fragment, useCallback, useRef, useState } from 'react'
import { formatDay, monthGridDays, startOfMonth, toDateInput } from '../features/calendar/dates'
import { parseDay } from '../lib/dayTime'
import { Icon } from './Icon'
import { MonthCalendar } from './MonthCalendar'
import { useAnchoredPopover, useOutsidePress } from './useAnchoredPopover'

type Side = 'from' | 'to'
export interface DayRange {
  /** `YYYY-MM-DD`, or '' for an open end. */
  from: string
  to: string
}

/** `YYYY-MM-DD` → a local Date, or null. */
function fromValue(value: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  return m ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])) : null
}

const shownText = (value: string) => {
  const day = fromValue(value)
  return day ? formatDay(day) : ''
}

/**
 * A period picked on one calendar: the first click is the start, the second the end, and the band follows the
 * pointer in between. Either end can also be typed as dd/mm/yyyy, or opened on its own to change just that end.
 * Same text rules as `DateField`: text that names no day goes back to the last good value on leaving the field.
 */
export function DateRangeField({
  value,
  onChange,
  invalid = false,
  labels = { from: 'Từ ngày', to: 'Đến ngày' },
}: {
  value: DayRange
  onChange: (value: DayRange) => void
  invalid?: boolean
  labels?: Record<Side, string>
}) {
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState<Side>('from')
  const [hover, setHover] = useState<Date | null>(null)
  const [shown, setShown] = useState(() => startOfMonth(fromValue(value.from) ?? new Date()))
  const [draft, setDraft] = useState<DayRange>({ from: shownText(value.from), to: shownText(value.to) })

  // The value can change from outside ("Xoá bộ lọc", Back): show it.
  const [shownValue, setShownValue] = useState(value)
  if (value.from !== shownValue.from || value.to !== shownValue.to) {
    setShownValue(value)
    setDraft({ from: shownText(value.from), to: shownText(value.to) })
  }

  const rootRef = useRef<HTMLDivElement>(null)
  const boxRef = useRef<HTMLDivElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)
  const inputs = { from: useRef<HTMLInputElement>(null), to: useRef<HTMLInputElement>(null) }

  const close = useCallback(() => {
    setOpen(false)
    setHover(null)
  }, [])
  useOutsidePress(open, rootRef, close)
  useAnchoredPopover(open, boxRef, panelRef, { matchWidth: false })

  function show(side: Side) {
    setActive(side)
    // The end being changed is on screen; a new end starts from the start's month.
    setShown(startOfMonth(fromValue(value[side]) ?? fromValue(value.from) ?? new Date()))
    setOpen(true)
  }

  function commitText(side: Side) {
    const text = draft[side].trim()
    if (!text) {
      if (value[side]) onChange({ ...value, [side]: '' })
      return
    }
    const day = parseDay(text)
    if (!day) {
      setDraft((d) => ({ ...d, [side]: shownText(value[side]) }))
      return
    }
    setDraft((d) => ({ ...d, [side]: formatDay(day) }))
    if (toDateInput(day) !== value[side]) onChange({ ...value, [side]: toDateInput(day) })
  }

  function pick(day: Date) {
    const picked = toDateInput(day)
    setHover(null)
    if (active === 'from' || !value.from || picked < value.from) {
      // A new start; an end before it no longer holds.
      const to = value.to && value.to >= picked ? value.to : ''
      onChange({ from: picked, to })
      if (active === 'from' && to) {
        setOpen(false)
        inputs.from.current?.focus()
      } else {
        setActive('to')
      }
      return
    }
    onChange({ ...value, to: picked })
    setOpen(false)
    inputs.to.current?.focus()
  }

  function onKeyDown(side: Side, event: React.KeyboardEvent) {
    if (event.key === 'Escape' && open) {
      event.preventDefault()
      event.stopPropagation()
      close()
    } else if (event.key === 'Enter') {
      event.preventDefault()
      commitText(side)
      close()
    } else if (event.key === 'ArrowDown' && !open) {
      event.preventDefault()
      show(side)
    }
  }

  // The band: the chosen period, or while the end is being picked, the start up to the day under the pointer.
  const start = value.from
  const end = open && active === 'to' && start && hover && toDateInput(hover) >= start ? toDateInput(hover) : value.to
  const band = start && end ? monthGridDays(shown).filter((d) => toDateInput(d) >= start && toDateInput(d) <= end) : []

  return (
    <div className="date-range" ref={rootRef}>
      <div className="date-range-box" ref={boxRef} role="group" aria-label="Khoảng thời gian">
        {(['from', 'to'] as const).map((side, i) => (
          <Fragment key={side}>
            {i === 1 && (
              <span className="date-range-dash" aria-hidden="true">
                –
              </span>
            )}
            <span className={open && active === side ? 'date-field-box date-range-active' : 'date-field-box'}>
              <input
                ref={inputs[side]}
                value={draft[side]}
                placeholder="dd/mm/yyyy"
                inputMode="numeric"
                autoComplete="off"
                aria-label={labels[side]}
                aria-invalid={invalid || undefined}
                aria-haspopup="dialog"
                aria-expanded={open && active === side}
                onChange={(e) => setDraft((d) => ({ ...d, [side]: e.target.value }))}
                onBlur={() => commitText(side)}
                onClick={() => !(open && active === side) && show(side)}
                onKeyDown={(e) => onKeyDown(side, e)}
              />
              <button
                type="button"
                className="icon-button"
                tabIndex={-1}
                aria-label={`Chọn ${labels[side].toLowerCase()} trên lịch`}
                onClick={() => (open && active === side ? close() : show(side))}
              >
                <Icon name="calendar" size={18} />
              </button>
            </span>
          </Fragment>
        ))}
      </div>

      {open && (
        <div
          ref={panelRef}
          className="date-pick-panel date-field-panel"
          role="dialog"
          aria-label={active === 'from' ? 'Chọn ngày bắt đầu' : 'Chọn ngày kết thúc'}
          popover="manual"
          onKeyDown={(e) => e.key === 'Escape' && (e.preventDefault(), close(), inputs[active].current?.focus())}
        >
          <MonthCalendar
            shown={shown}
            onShow={setShown}
            now={new Date()}
            selected={fromValue(value.from)}
            rangeEnd={fromValue(end)}
            viewed={band}
            onPick={pick}
            onHover={setHover}
          />
          <div className="date-field-foot date-range-foot">
            <span>{active === 'from' ? 'Chọn ngày bắt đầu' : 'Chọn ngày kết thúc'}</span>
            {(value.from || value.to) && (
              <button
                type="button"
                className="btn btn-ghost btn-sm"
                onClick={() => {
                  onChange({ from: '', to: '' })
                  setActive('from')
                  // The button goes with the values: keep focus in the field, where Escape still closes.
                  inputs.from.current?.focus()
                }}
              >
                Xoá
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
