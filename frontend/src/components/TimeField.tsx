import { useCallback, useId, useLayoutEffect, useRef, useState } from 'react'
import { parseTime } from '../lib/dayTime'
import { Icon } from './Icon'
import { useAnchoredPopover, useOutsidePress } from './useAnchoredPopover'

const STEP = 15
const pad = (n: number) => String(n).padStart(2, '0')
const toText = (minutes: number) => `${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}`
const toMinutes = (time: string) => {
  const m = /^(\d{2}):(\d{2})$/.exec(time)
  return m ? Number(m[1]) * 60 + Number(m[2]) : null
}

/** "30 phút", "1 giờ", "1 giờ 30 phút" — how long a meeting lasts if it ends at that option. */
function lengthLabel(minutes: number) {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  if (h === 0) return `${m} phút`
  return m === 0 ? `${h} giờ` : `${h} giờ ${m} phút`
}

/**
 * A time of day on the 24-hour clock, typed or picked from a list every 15 minutes. Replaces
 * `<input type="time">`, which follows the browser's language: on an English Chrome it asked for
 * AM/PM in a Vietnamese form. Given `from` (the start, on the same day), the list begins after it
 * and says how long the meeting would last, as Google Calendar's end-time list does.
 */
export function TimeField({
  id,
  name,
  value,
  onChange,
  from,
  invalid = false,
  disabled = false,
}: {
  id?: string
  name?: string
  /** `HH:mm`, or '' while empty. */
  value: string
  onChange: (value: string) => void
  from?: string
  invalid?: boolean
  disabled?: boolean
}) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const listId = `${inputId}-times`
  const [draft, setDraft] = useState(value)
  const [open, setOpen] = useState(false)
  const [highlighted, setHighlighted] = useState(0)
  // Set once the arrows have moved the highlight: Enter then takes that row, not the typed text.
  const [browsing, setBrowsing] = useState(false)

  const rootRef = useRef<HTMLDivElement>(null)
  const boxRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLUListElement>(null)

  // The value can change from outside (a reset, the start moving the end): show it.
  const [shownValue, setShownValue] = useState(value)
  if (value !== shownValue) {
    setShownValue(value)
    setDraft(value)
  }

  const start = from ? toMinutes(from) : null
  const first = start === null ? 0 : start + STEP
  const options = Array.from({ length: Math.max(0, Math.ceil((24 * 60 - first) / STEP)) }, (_, i) => first + i * STEP)

  const close = useCallback(() => setOpen(false), [])
  useOutsidePress(open, rootRef, close)
  useAnchoredPopover(open, boxRef, listRef)

  // Bring the highlighted row into the middle of the list, as the list first opens and as it moves.
  useLayoutEffect(() => {
    if (!open) return
    const row = listRef.current?.children[highlighted] as HTMLElement | undefined
    row?.scrollIntoView({ block: browsing ? 'nearest' : 'center' })
  }, [open, highlighted, browsing])

  /** The row for the value, or the first one after it — what the list should land on. */
  function nearest(time: string) {
    const target = toMinutes(time) ?? (start === null ? 8 * 60 : start + 60)
    const index = options.findIndex((m) => m >= target)
    return index === -1 ? options.length - 1 : index
  }

  function show() {
    setHighlighted(nearest(value))
    setBrowsing(false)
    setOpen(true)
  }

  function commit(time: string) {
    setDraft(time)
    if (time !== value) onChange(time)
    setOpen(false)
  }

  function commitText() {
    if (!draft.trim()) {
      if (value) onChange('')
      return
    }
    const time = parseTime(draft)
    if (time) commit(time)
    else setDraft(value)
  }

  function onKeyDown(event: React.KeyboardEvent) {
    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowUp': {
        event.preventDefault()
        if (!open) return show()
        const step = event.key === 'ArrowDown' ? 1 : -1
        setBrowsing(true)
        setHighlighted((i) => Math.min(options.length - 1, Math.max(0, i + step)))
        break
      }
      case 'Enter':
        event.preventDefault()
        if (open && browsing && options[highlighted] !== undefined) commit(toText(options[highlighted]))
        else commitText()
        setOpen(false)
        break
      case 'Escape':
        if (open) {
          // Inside a dialog, Escape would otherwise close the dialog too.
          event.preventDefault()
          event.stopPropagation()
          setOpen(false)
        }
        break
      case 'Tab':
        setOpen(false)
        break
    }
  }

  return (
    <div className="time-field" ref={rootRef}>
      <div className="date-field-box" ref={boxRef}>
        <input
          ref={inputRef}
          id={inputId}
          name={name}
          value={draft}
          placeholder="hh:mm"
          inputMode="numeric"
          autoComplete="off"
          role="combobox"
          aria-invalid={invalid || undefined}
          aria-expanded={open}
          aria-controls={listId}
          aria-autocomplete="none"
          aria-activedescendant={open ? `${listId}-${highlighted}` : undefined}
          disabled={disabled}
          onChange={(e) => {
            setDraft(e.target.value)
            const typed = parseTime(e.target.value)
            if (open && typed) {
              setBrowsing(false)
              setHighlighted(nearest(typed))
            }
          }}
          onBlur={commitText}
          onClick={() => !open && show()}
          onKeyDown={onKeyDown}
        />
        <button
          type="button"
          className="icon-button"
          tabIndex={-1}
          aria-label="Chọn giờ"
          disabled={disabled}
          onClick={() => {
            if (open) return setOpen(false)
            show()
            inputRef.current?.focus()
          }}
        >
          <Icon name="clock" size={18} />
        </button>
      </div>

      {open && (
        <ul ref={listRef} id={listId} className="select-menu time-field-menu" role="listbox" popover="manual">
          {options.map((minutes, index) => {
            const time = toText(minutes)
            return (
              <li
                key={time}
                id={`${listId}-${index}`}
                role="option"
                aria-selected={time === value}
                className={index === highlighted ? 'select-option select-option-active' : 'select-option'}
                onMouseEnter={() => setHighlighted(index)}
                // Before the input's blur, so the click is not lost to commitText re-rendering the list.
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => commit(time)}
              >
                <span className="select-option-label">
                  {time}
                  {start !== null && <span className="select-option-hint">{lengthLabel(minutes - start)}</span>}
                </span>
                <span className="select-check">{time === value && <Icon name="check" size={16} />}</span>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
