import { useEffect, useRef, useState } from 'react'
import { Icon } from '../../components/Icon'
import { MonthCalendar } from '../../components/MonthCalendar'
import { startOfMonth } from './dates'

const PANEL_ID = 'calendar-date-picker'

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

  return (
    <div className="date-pick" ref={rootRef}>
      <h1 className="calendar-range">
        <button
          ref={buttonRef}
          type="button"
          className="calendar-range-button"
          aria-expanded={open}
          aria-controls={open ? PANEL_ID : undefined}
          data-tooltip="Chọn ngày nhanh"
          onClick={toggle}
        >
          {label}
          <Icon name="chevronDown" size={18} />
        </button>
      </h1>

      {open && (
        <div className="date-pick-panel" id={PANEL_ID} role="group" aria-label="Chọn ngày nhanh">
          <p className="date-pick-title">Chọn ngày nhanh</p>
          <MonthCalendar shown={shown} onShow={setShown} now={now} viewed={viewed} onPick={pick} />

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
