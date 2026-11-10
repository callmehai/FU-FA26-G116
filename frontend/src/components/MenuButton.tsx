import { useCallback, useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { Icon, type IconName } from './Icon'
import { useOutsidePress } from './useAnchoredPopover'

export interface MenuItem {
  key: string
  /** What stands in front of the label: a file-type tag, an icon. */
  lead: ReactNode
  label: string
  onSelect: () => void
}

/**
 * A button that opens a short menu of choices ("Tải xuống ▾" → Word, PDF), in the page's select-menu look. Arrow keys
 * move through the items, Escape closes it and returns focus, Tab leaves it.
 */
export function MenuButton({
  label,
  icon,
  items,
  disabled,
  iconOnly = false,
}: {
  label: string
  icon: IconName
  items: MenuItem[]
  disabled?: boolean
  /** The icon alone ("…" at the end of a row), named by `label`; the menu then opens towards the left. */
  iconOnly?: boolean
}) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const menuId = useId()
  const close = useCallback(() => setOpen(false), [])
  useOutsidePress(open, rootRef, close)
  useEffect(() => {
    if (open) menuRef.current?.querySelector<HTMLElement>('[role="menuitem"]')?.focus()
  }, [open])

  function onMenuKey(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      event.preventDefault()
      setOpen(false)
      triggerRef.current?.focus()
      return
    }
    if (event.key === 'Tab') return setOpen(false)
    const options = [...event.currentTarget.querySelectorAll<HTMLElement>('[role="menuitem"]')]
    const at = options.indexOf(document.activeElement as HTMLElement)
    const next =
      event.key === 'ArrowDown'
        ? (at + 1) % options.length
        : event.key === 'ArrowUp'
          ? (at + options.length - 1) % options.length
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? options.length - 1
              : -1
    if (next < 0) return
    event.preventDefault()
    options[next]?.focus()
  }

  return (
    <div ref={rootRef} className={iconOnly ? 'menu-button menu-button-end' : 'menu-button'}>
      <button
        ref={triggerRef}
        type="button"
        className={iconOnly ? 'icon-button' : 'btn btn-outline btn-sm'}
        aria-label={iconOnly ? label : undefined}
        data-tooltip={iconOnly && !open ? label : undefined}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        disabled={disabled}
        onClick={() => setOpen((v) => !v)}
      >
        <Icon name={icon} size={iconOnly ? 18 : 16} />
        {!iconOnly && (
          <>
            {label}
            <Icon name="chevronDown" size={14} />
          </>
        )}
      </button>
      {open && (
        // Laid out under the button inside the page, not in the top layer: it scrolls with the page in one piece instead
        // of being placed again on every scroll, which trailed behind the button.
        <div ref={menuRef} id={menuId} className="select-menu menu-button-menu" role="menu" aria-label={label} onKeyDown={onMenuKey}>
          {items.map((item) => (
            <button
              key={item.key}
              type="button"
              role="menuitem"
              tabIndex={-1}
              className="select-option menu-button-option"
              onClick={() => {
                triggerRef.current?.focus()
                setOpen(false)
                item.onSelect()
              }}
            >
              {item.lead}
              {item.label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
