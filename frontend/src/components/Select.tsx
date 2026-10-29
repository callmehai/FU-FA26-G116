import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react'
import { Icon } from './Icon'

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
 * The menu opens in the browser's top layer (Popover API), positioned against the trigger. Inside
 * a modal dialog nothing else works: the dialog's scrolling body would clip an absolutely
 * positioned menu, and z-index cannot lift anything above a `showModal()` dialog — only another
 * top-layer element sits over it. It flips upwards when there is no room below.
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

  // Close when the click lands outside, which a button cannot detect on its own.
  useEffect(() => {
    if (!open) return

    function onPointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false)
    }

    document.addEventListener('pointerdown', onPointerDown)
    return () => document.removeEventListener('pointerdown', onPointerDown)
  }, [open])

  // Lift the menu into the top layer and keep it pinned to the trigger while anything scrolls.
  useLayoutEffect(() => {
    const menu = menuRef.current
    const trigger = triggerRef.current
    if (!open || !menu || !trigger) return

    menu.showPopover?.()

    function place() {
      if (!menu || !trigger) return
      const box = trigger.getBoundingClientRect()
      const gap = 4
      const below = window.innerHeight - box.bottom
      const upwards = below < menu.offsetHeight + gap * 2 && box.top > below

      menu.style.left = `${box.left}px`
      menu.style.minWidth = `${box.width}px`
      menu.style.top = upwards ? 'auto' : `${box.bottom + gap}px`
      menu.style.bottom = upwards ? `${window.innerHeight - box.top + gap}px` : 'auto'
    }

    place()
    // Capture phase: a scroll inside the dialog body does not bubble to window.
    window.addEventListener('scroll', place, true)
    window.addEventListener('resize', place)
    return () => {
      window.removeEventListener('scroll', place, true)
      window.removeEventListener('resize', place)
      if (menu.matches(':popover-open')) menu.hidePopover()
    }
  }, [open])

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
