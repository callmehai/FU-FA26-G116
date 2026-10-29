import { useCallback, useId, useRef, useState } from 'react'
import { formatDay, startOfMonth, toDateInput } from '../features/calendar/dates'
import { parseDay } from '../lib/dayTime'
import { Icon } from './Icon'
import { MonthCalendar } from './MonthCalendar'
import { useAnchoredPopover, useOutsidePress } from './useAnchoredPopover'

/** `YYYY-MM-DD` → a local Date, or null. */
function fromValue(value: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  return m ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])) : null
}

/**
 * A day typed as dd/mm/yyyy or picked from a month calendar. Replaces `<input type="date">`, whose
 * text and popup follow the browser's language: on an English Chrome it showed "10/29/2026" and an
 * English calendar in a Vietnamese form. Text that names no day goes back to the last good value
 * on leaving the field, as Google Calendar does.
 */
export function DateField({
  id,
  name,
  value,
  onChange,
  invalid = false,
  disabled = false,
}: {
  id?: string
  name?: string
  /** `YYYY-MM-DD`, or '' while empty. */
  value: string
  onChange: (value: string) => void
  invalid?: boolean
  disabled?: boolean
}) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const panelId = `${inputId}-calendar`
  const selected = fromValue(value)
  const [draft, setDraft] = useState(() => (selected ? formatDay(selected) : ''))
  const [open, setOpen] = useState(false)
  const [shown, setShown] = useState(() => startOfMonth(selected ?? new Date()))

  const rootRef = useRef<HTMLDivElement>(null)
  const boxRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  // The value can change from outside (a prefilled day, a reset): show it. Adjusted during render,
  // as React recommends, rather than in an effect that would draw the stale text first.
  const [shownValue, setShownValue] = useState(value)
  if (value !== shownValue) {
    setShownValue(value)
    setDraft(selected ? formatDay(selected) : '')
  }

  const close = useCallback(() => setOpen(false), [])
  useOutsidePress(open, rootRef, close)
  useAnchoredPopover(open, boxRef, panelRef, { matchWidth: false })

  function show() {
    setShown(startOfMonth(selected ?? new Date()))
    setOpen(true)
  }

  function commitText() {
    if (!draft.trim()) {
      if (value) onChange('')
      return
    }
    const day = parseDay(draft)
    if (!day) {
      setDraft(selected ? formatDay(selected) : '')
      return
    }
    setDraft(formatDay(day))
    if (toDateInput(day) !== value) onChange(toDateInput(day))
  }

  function pick(day: Date) {
    setDraft(formatDay(day))
    onChange(toDateInput(day))
    setOpen(false)
    inputRef.current?.focus()
  }

  function onKeyDown(event: React.KeyboardEvent) {
    if (event.key === 'Escape' && open) {
      // Inside a dialog, Escape would otherwise close the dialog too.
      event.preventDefault()
      event.stopPropagation()
      setOpen(false)
      inputRef.current?.focus()
    } else if (event.key === 'Enter' && event.target === inputRef.current) {
      event.preventDefault()
      commitText()
      setOpen(false)
    } else if (event.key === 'ArrowDown' && event.target === inputRef.current && !open) {
      event.preventDefault()
      show()
    }
  }

  return (
    <div className="date-field" ref={rootRef} onKeyDown={onKeyDown}>
      <div className="date-field-box" ref={boxRef}>
        <input
          ref={inputRef}
          id={inputId}
          name={name}
          value={draft}
          placeholder="dd/mm/yyyy"
          inputMode="numeric"
          autoComplete="off"
          aria-invalid={invalid || undefined}
          aria-haspopup="dialog"
          aria-expanded={open}
          aria-controls={open ? panelId : undefined}
          disabled={disabled}
          onChange={(e) => setDraft(e.target.value)}
          onBlur={commitText}
          onClick={() => !open && show()}
        />
        <button
          type="button"
          className="icon-button"
          tabIndex={-1}
          aria-label="Chọn ngày trên lịch"
          disabled={disabled}
          onClick={() => (open ? setOpen(false) : show())}
        >
          <Icon name="calendar" size={18} />
        </button>
      </div>

      {open && (
        <div
          ref={panelRef}
          id={panelId}
          className="date-pick-panel date-field-panel"
          role="dialog"
          aria-label="Chọn ngày"
          popover="manual"
        >
          <MonthCalendar shown={shown} onShow={setShown} now={new Date()} selected={selected} onPick={pick} />
          <div className="date-field-foot">
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => pick(new Date())}>
              Hôm nay
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
