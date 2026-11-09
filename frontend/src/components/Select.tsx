import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { Icon, type IconName } from './Icon'
import { useAnchoredPopover, useOutsidePress } from './useAnchoredPopover'

export interface SelectOption<T extends string> {
  value: T
  label: string
  /** A colour (a CSS value) for a dot before the label, where the value is a state — "● Có mặt". */
  dot?: string
  /** A muted word after the label, such as "Khớp giọng nhất" on the person a voice most likely belongs to. */
  hint?: string
  /** Shows the hint as a brand-tinted tag rather than muted text, for the one option the machine points at. */
  hintAccent?: boolean
  /** A second, muted line — an email that tells two people of the same name apart. Searched too. */
  sub?: string
}

/** Lower case without Vietnamese marks, so "loc" finds "Lộc" (đ becomes d). */
const fold = (text: string) =>
  text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/đ/g, 'd')

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
 *
 * `searchable` puts a search box at the top of the menu for a long list of people (SCRUM-150): it takes
 * the focus, matches the label and `sub` without regard to case or marks, and drives the same keys.
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
  icon,
  searchable = false,
  searchPlaceholder = 'Tìm…',
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
  /** A small icon button instead of the field (an action in a row's tools); its name and value stay in aria-label. */
  icon?: IconName
  /** A search box at the top of the menu, for a list too long to scroll through. */
  searchable?: boolean
  searchPlaceholder?: string
}) {
  const generatedId = useId()
  const listId = id ?? generatedId

  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [highlighted, setHighlighted] = useState(() => Math.max(0, options.findIndex((o) => o.value === value)))

  const rootRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLElement>(null)
  const searchRef = useRef<HTMLInputElement>(null)

  const selected = options.find((o) => o.value === value)
  // What the menu lists: everything, or what the search matches.
  const needle = fold(query.trim())
  const shown = needle ? options.filter((o) => fold(`${o.label} ${o.sub ?? ''}`).includes(needle)) : options

  const close = useCallback(() => setOpen(false), [])
  useOutsidePress(open, rootRef, close)
  useAnchoredPopover(open, triggerRef, menuRef)

  // The search box takes the focus as the menu opens; the trigger gets it back on close (commit, Escape).
  useEffect(() => {
    if (open && searchable) searchRef.current?.focus()
  }, [open, searchable])

  // A long list scrolls: keep the highlighted row in sight as the arrows move it.
  useEffect(() => {
    if (open) document.getElementById(`${listId}-${highlighted}`)?.scrollIntoView({ block: 'nearest' })
  }, [open, highlighted, listId])

  function openAt(index: number) {
    setQuery('')
    setHighlighted(index)
    setOpen(true)
  }

  function commit(index: number) {
    const option = shown[index]
    // Nothing matched: Enter keeps the menu open for another search.
    if (!option) return
    onChange(option.value)
    setOpen(false)
    triggerRef.current?.focus()
  }

  function onKeyDown(event: React.KeyboardEvent) {
    // Opening clears the search, so the picked row is found in the whole list.
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
          setHighlighted((i) => Math.min(shown.length - 1, Math.max(0, i + step)))
        }
        break
      }
      case 'Enter':
      case ' ':
        // A space in the search box is part of the name being typed.
        if (event.key === ' ' && event.target === searchRef.current) break
        event.preventDefault()
        if (open) commit(highlighted)
        else openAt(currentIndex)
        break
      case 'Escape':
        if (open) {
          event.preventDefault()
          // Escape belongs to this menu: preventDefault keeps a <dialog> around it from closing, and
          // stopPropagation keeps document-level key handlers out of it.
          event.stopPropagation()
          setOpen(false)
          triggerRef.current?.focus()
        }
        break
      case 'Tab':
        setOpen(false)
        break
    }
  }

  function renderOption(option: SelectOption<T>, index: number) {
    return (
      <li
        key={option.value}
        id={`${listId}-${index}`}
        role="option"
        aria-selected={option.value === value}
        className={index === highlighted ? 'select-option select-option-active' : 'select-option'}
        onMouseEnter={() => setHighlighted(index)}
        // Keeps the focus in the search box until the click lands.
        onMouseDown={(e) => searchable && e.preventDefault()}
        onClick={() => commit(index)}
      >
        {/* Label first, so it lines up with the value in the trigger; the check sits at the
            end of the row, where it marks one row without indenting all the others. */}
        <span className="select-option-label">
          {option.dot && <span className="select-dot" style={{ background: option.dot }} aria-hidden="true" />}
          {option.label}
          {option.hint && (
            <span className={option.hintAccent ? 'select-option-hint select-option-hint-accent' : 'select-option-hint'}>
              {option.hint}
            </span>
          )}
          {option.sub && <span className="select-option-sub">{option.sub}</span>}
        </span>
        <span className="select-check">{option.value === value && <Icon name="check" size={16} />}</span>
      </li>
    )
  }

  return (
    <div className={size === 'sm' ? 'select select-sm' : 'select'} ref={rootRef}>
      <button
        type="button"
        ref={triggerRef}
        className={icon ? 'select-trigger select-icon' : selected ? 'select-trigger' : 'select-trigger select-empty'}
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
        {icon ? (
          <Icon name={icon} size={16} />
        ) : (
          <>
        {/* Nothing picked yet: the accessible name stands in, muted so it does not read as a value. */}
        <span className={selected ? 'select-value' : 'select-placeholder'}>
          {(marker ?? selected?.dot) && (
            <span className="select-dot" style={{ background: marker ?? selected?.dot }} aria-hidden="true" />
          )}
          {/* Its own box, so a long value ends in "…" instead of being cut mid-letter. */}
          <span className="select-text">{selected?.label ?? placeholder ?? label}</span>
        </span>
        <Icon name="chevronDown" size={16} />
          </>
        )}
      </button>

      {open && searchable ? (
        <div ref={(el) => void (menuRef.current = el)} className="select-menu select-menu-search" popover="manual">
          <label className="search select-search">
            <Icon name="search" size={16} />
            <input
              ref={searchRef}
              type="search"
              value={query}
              onChange={(e) => {
                setQuery(e.target.value)
                setHighlighted(0)
              }}
              onKeyDown={onKeyDown}
              placeholder={searchPlaceholder}
              aria-label={searchPlaceholder}
              aria-controls={listId}
              aria-activedescendant={shown[highlighted] ? `${listId}-${highlighted}` : undefined}
            />
          </label>
          <ul className="select-list" role="listbox" id={listId} aria-label={label}>
            {shown.length === 0 ? (
              <li className="select-none">Không có kết quả khớp.</li>
            ) : (
              shown.map((option, index) => renderOption(option, index))
            )}
          </ul>
        </div>
      ) : (
        open && (
          <ul
            ref={(el) => void (menuRef.current = el)}
            className="select-menu"
            role="listbox"
            id={listId}
            aria-label={label}
            popover="manual"
          >
            {shown.map((option, index) => renderOption(option, index))}
          </ul>
        )
      )}
    </div>
  )
}
