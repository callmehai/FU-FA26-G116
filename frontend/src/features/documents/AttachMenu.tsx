import { useCallback, useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import { Icon, type IconName } from '../../components/Icon'
import { useAnchoredPopover, useOutsidePress } from '../../components/useAnchoredPopover'
import type { DocumentSource } from './AddDocumentsDialog'

const ATTACH_SOURCES: { source: DocumentSource; icon: IconName; label: string }[] = [
  { source: 'computer', icon: 'upload', label: 'Tải tệp lên từ máy' },
  { source: 'meeting', icon: 'fileText', label: 'Chọn từ tài liệu cuộc họp' },
  { source: 'library', icon: 'folder', label: 'Chọn từ kho của tôi' },
]

/**
 * The paperclip: a menu of where to take documents from, as AI-eCabinet has it; each opens the add dialog there.
 * One component for an agenda item (UC-33b) and a draft vote (UC-76a, SCRUM-96), as BR-304 is one rule for both.
 */
export function AttachMenu({ title, onPick }: { title: string; onPick: (source: DocumentSource) => void }) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const menuId = useId()
  const close = useCallback(() => setOpen(false), [])
  useOutsidePress(open, rootRef, close)
  useAnchoredPopover(open, triggerRef, menuRef, { matchWidth: false })
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
    // Tab leaves the menu for the next control, so the menu goes with it.
    if (event.key === 'Tab') return setOpen(false)
    const items = [...event.currentTarget.querySelectorAll<HTMLElement>('[role="menuitem"]')]
    const at = items.indexOf(document.activeElement as HTMLElement)
    const next =
      event.key === 'ArrowDown'
        ? (at + 1) % items.length
        : event.key === 'ArrowUp'
          ? (at + items.length - 1) % items.length
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? items.length - 1
              : -1
    if (next < 0) return
    event.preventDefault()
    items[next]?.focus()
  }

  return (
    <div ref={rootRef}>
      <button
        ref={triggerRef}
        type="button"
        className="icon-button icon-button-sm agenda-attach-trigger"
        aria-label={`Thêm tài liệu: ${title}`}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        data-tooltip={open ? undefined : 'Thêm tài liệu'}
        onClick={() => setOpen((v) => !v)}
      >
        <Icon name="paperclip" size={16} />
      </button>
      {open && (
        <div
          ref={menuRef}
          id={menuId}
          className="select-menu"
          role="menu"
          aria-label="Thêm tài liệu từ"
          popover="manual"
          onKeyDown={onMenuKey}
        >
          {ATTACH_SOURCES.map(({ source, icon, label }) => (
            <button
              key={source}
              type="button"
              role="menuitem"
              tabIndex={-1}
              className="select-option agenda-attach-option"
              onClick={() => {
                // The dialog returns focus to whatever had it when it opened: the paperclip, not a gone item.
                triggerRef.current?.focus()
                setOpen(false)
                onPick(source)
              }}
            >
              <Icon name={icon} size={16} />
              {label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
