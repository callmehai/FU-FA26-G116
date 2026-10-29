import { useCallback, useId, useRef, useState } from 'react'
import { Icon } from './Icon'
import { useAnchoredPopover, useOutsidePress } from './useAnchoredPopover'

export interface SelectOption<T extends string> {
  value: T
  label: string
  /** A colour (a CSS value) for a dot before the label, where the value is a state — "● Có mặt". */
  dot?: string
  /** A muted word after the label, such as "Khớp giọng nhất" on the person a voice most likely belongs to. */
  hint?: string
}

/**
 * Dropdown built from a button and a listbox instead of a native `<select>`.
 *
 * A native select renders its popup with the operating system, which ignores the page's stylesheet:
 * on macOS in dark mode the menu came out with its own colours and did not match anything around
 * it. Owning the markup is the only way to keep the popup on the product's palette.
 *
 * Keyboard behaviour follows the ARIA listbox pattern: arrows move the highlight, Enter or Space
 * commits it, Escape closes without changing the value, and focus returns to the trigger.
 *
 * The menu opens in the browser's top layer (Popover API), positioned against the trigger and
 * flipped upwards when there is no room below — see `useAnchoredPopover` for why.
 */
export function Select<T extends string>({
  value,
  options,
  onChange,
  label,
  id,
  placeholder,
  size,
  disabled = false,
  marker,
  tooltip,
}: {
  /** Null while nothing is picked yet: the trigger then shows `placeholder` (or `label`), muted. */
  value: T | null
  options: SelectOption<T>[]
  onChange: (value: T) => void
  /** Accessible name, used when the control has no visible <label> wrapping it. */
  label: string
  id?: string
  placeholder?: string
  /** 'sm' is the 32px control that sits inside a table row (40px on a touch screen). */
  size?: 'sm'
  disabled?: boolean
  /** A colour for a dot that stays before the trigger's text whatever is picked — whose voice it is. */
  marker?: string
  /** Shown on hover or focus of the trigger, for a fact the closed control does not say. */
  tooltip?: string
}) {
  const generatedId = useId()
  const listId = id ?? generatedId

  const [open, setOpen] = useState(false)
  const [highlighted, setHighlighted] = useState(() => Math.max(0, options.findIndex((o) => o.value === value)))

  const rootRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLUListElement>(null)

  const selected = options.find((o) => o.value === value)

  const close = useCallback(() => setOpen(false), [])
  useOutsidePress(open, rootRef, close)
  useAnchoredPopover(open, triggerRef, menuRef)

  function openAt(index: number) {
    setHighlighted(index)
    setOpen(true)
  }

  function commit(index: number) {
    const option = options[index]
    if (option) onChange(option.value)
    setOpen(false)
    triggerRef.current?.focus()
  }

  function onKeyDown(event: React.KeyboardEvent) {
    const currentIndex = Math.max(0, options.findIndex((o) => o.value === value))

    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowUp': {
        event.preventDefault()
        const step = event.key === 'ArrowDown' ? 1 : -1
        if (!open) {
          openAt(currentIndex)
        } else {
          // Clamp rather than wrap: reaching an end and staying there is easier to track by feel.
          setHighlighted((i) => Math.min(options.length - 1, Math.max(0, i + step)))
        }
        break
      }
      case 'Enter':
      case ' ':
        event.preventDefault()
        if (open) commit(highlighted)
        else openAt(currentIndex)
        break
      case 'Escape':
        if (open) {
          event.preventDefault()
          setOpen(false)
        }
        break
      case 'Tab':
        setOpen(false)
        break
    }
  }

  return (
    <div className={size === 'sm' ? 'select select-sm' : 'select'} ref={rootRef}>
      <button
        type="button"
        ref={triggerRef}
        className={selected ? 'select-trigger' : 'select-trigger select-empty'}
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listId}
        // An aria-label replaces the button's text, so it must carry the value too.
        aria-label={selected ? `${label}: ${selected.label}` : label}
        // Focus stays on the trigger while the menu is open; this points at the highlighted option.
        aria-activedescendant={open ? `${listId}-${highlighted}` : undefined}
        onClick={() => (open ? setOpen(false) : openAt(Math.max(0, options.findIndex((o) => o.value === value))))}
        onKeyDown={onKeyDown}
        data-tooltip={open ? undefined : tooltip}
      >
        {/* Nothing picked yet: the accessible name stands in, muted so it does not read as a value. */}
        <span className={selected ? 'select-value' : 'select-placeholder'}>
          {(marker ?? selected?.dot) && (
            <span className="select-dot" style={{ background: marker ?? selected?.dot }} aria-hidden="true" />
          )}
          {selected?.label ?? placeholder ?? label}
        </span>
        <Icon name="chevronDown" size={16} />
      </button>

      {open && (
        <ul ref={menuRef} className="select-menu" role="listbox" id={listId} aria-label={label} popover="manual">
          {options.map((option, index) => (
            <li
              key={option.value}
              id={`${listId}-${index}`}
              role="option"
              aria-selected={option.value === value}
              className={index === highlighted ? 'select-option select-option-active' : 'select-option'}
              onMouseEnter={() => setHighlighted(index)}
              onClick={() => commit(index)}
            >
              {/* Label first, so it lines up with the value in the trigger; the check sits at the
                  end of the row, where it marks one row without indenting all the others. */}
              <span className="select-option-label">
                {option.dot && <span className="select-dot" style={{ background: option.dot }} aria-hidden="true" />}
                {option.label}
                {option.hint && <span className="select-option-hint">{option.hint}</span>}
              </span>
              <span className="select-check">{option.value === value && <Icon name="check" size={16} />}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
