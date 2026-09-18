import { useEffect, useId, useRef, useState } from 'react'
import { Icon } from './Icon'

export interface SelectOption<T extends string> {
  value: T
  label: string
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
 */
export function Select<T extends string>({
  value,
  options,
  onChange,
  label,
  id,
}: {
  value: T
  options: SelectOption<T>[]
  onChange: (value: T) => void
  /** Accessible name, used when the control has no visible <label> wrapping it. */
  label: string
  id?: string
}) {
  const generatedId = useId()
  const listId = id ?? generatedId

  const [open, setOpen] = useState(false)
  const [highlighted, setHighlighted] = useState(() => Math.max(0, options.findIndex((o) => o.value === value)))

  const rootRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)

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
    <div className="select" ref={rootRef}>
      <button
        type="button"
        ref={triggerRef}
        className="select-trigger"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listId}
        aria-label={label}
        onClick={() => (open ? setOpen(false) : openAt(Math.max(0, options.findIndex((o) => o.value === value))))}
        onKeyDown={onKeyDown}
      >
        <span>{selected?.label ?? label}</span>
        <Icon name="chevronDown" size={16} />
      </button>

      {open && (
        <ul className="select-menu" role="listbox" id={listId} aria-label={label}>
          {options.map((option, index) => (
            <li
              key={option.value}
              role="option"
              aria-selected={option.value === value}
              className={index === highlighted ? 'select-option select-option-active' : 'select-option'}
              onMouseEnter={() => setHighlighted(index)}
              onClick={() => commit(index)}
            >
              <span className="select-check">{option.value === value ? '✓' : ''}</span>
              {option.label}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
