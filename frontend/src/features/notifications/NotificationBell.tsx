import { useEffect, useRef } from 'react'
import { Icon } from '../../components/Icon'
import { NotificationPanel } from './NotificationPanel'
import { useNotifications } from './notificationsContext'

const PANEL_ID = 'notification-panel'

/**
 * The bell in the app bar (SCRUM-14): the unread count as a badge, and a panel that drops down
 * under it. The panel is non-modal — the page behind stays usable — and closes on an outside click,
 * on Escape, and once a notification has been opened.
 */
export function NotificationBell() {
  const { unreadCount, arrivals, panelOpen, setPanelOpen } = useNotifications()
  const rootRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!panelOpen) return
    function onPointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setPanelOpen(false)
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape') return
      setPanelOpen(false)
      buttonRef.current?.focus()
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [panelOpen, setPanelOpen])

  const count = unreadCount ?? 0
  const label = count > 0 ? `Thông báo, ${count} chưa đọc` : 'Thông báo'

  return (
    <div className="notif" ref={rootRef}>
      <button
        ref={buttonRef}
        type="button"
        className={`icon-button notif-bell${panelOpen ? ' notif-bell-open' : ''}`}
        title="Thông báo"
        aria-label={label}
        aria-expanded={panelOpen}
        aria-controls={panelOpen ? PANEL_ID : undefined}
        onClick={() => setPanelOpen(!panelOpen)}
      >
        {/* Re-keyed on every arrival, so the ring animation restarts for each new notification. */}
        <span key={arrivals} className={arrivals > 0 ? 'notif-ring' : undefined}>
          <Icon name="bell" />
        </span>
        {count > 0 && (
          <span className="notif-badge" aria-hidden="true">
            {count > 99 ? '99+' : count}
          </span>
        )}
      </button>

      {panelOpen && <NotificationPanel id={PANEL_ID} onClose={() => setPanelOpen(false)} />}
    </div>
  )
}
