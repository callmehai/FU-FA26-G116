import { useCallback, useEffect, useLayoutEffect, useRef, type ReactNode } from 'react'
import { usePhone } from './usePhone'

/** Gap to the anchor, and the least room kept to the viewport's edges. */
const SIDE_OFFSET = 8
const EDGE = 8

/**
 * The calendar's one floating layer, as AI-eCabinet's calendar draws it: beside its anchor (a tile or a "+N") on a
 * wide screen, to the right when there is room, else to the left, kept inside the viewport; a bottom sheet over a
 * scrim on a phone. It follows its content's size, so a list that grows an overview beside it stays on screen.
 *
 * Escape and a press elsewhere close it; a press on another calendar trigger (`data-cal-trigger`) is left to that
 * trigger, which toggles its own layer. A keyboard opening moves focus in; closing hands it back to the anchor.
 */
export function CalendarPopover({
  anchor,
  keyboard,
  label,
  className,
  keepPlace = false,
  onClose,
  children,
}: {
  anchor: HTMLElement
  keyboard: boolean
  label: string
  className?: string
  /**
   * Keep the place the card opened at while its content changes (a "+N" list showing an overview beside it or
   * instead of it): it grows to the right when there is room, else to the left from its right edge (`data-grow`),
   * so the list never jumps.
   */
  keepPlace?: boolean
  onClose: () => void
  children: ReactNode
}) {
  const ref = useRef<HTMLDivElement>(null)
  const home = useRef<{ left: number; width: number } | null>(null)
  const phone = usePhone()

  const place = useCallback(() => {
    const card = ref.current
    if (!card || phone) return
    const box = anchor.getBoundingClientRect()
    const width = card.offsetWidth
    const height = card.offsetHeight
    const right = box.right + SIDE_OFFSET
    let left: number
    if (keepPlace && home.current) {
      const toRight = home.current.left + width + EDGE <= window.innerWidth
      left = toRight ? home.current.left : Math.max(EDGE, home.current.left + home.current.width - width)
      card.dataset.grow = toRight ? 'right' : 'left'
    } else {
      left = right + width + EDGE <= window.innerWidth ? right : Math.max(EDGE, box.left - SIDE_OFFSET - width)
      home.current = { left, width }
      delete card.dataset.grow
    }
    const top = Math.min(Math.max(EDGE, box.top), window.innerHeight - height - EDGE)
    card.style.left = `${left}px`
    card.style.top = `${Math.max(EDGE, top)}px`
  }, [anchor, phone, keepPlace])

  useLayoutEffect(() => {
    place()
    const card = ref.current
    const observer = new ResizeObserver(place)
    if (card) observer.observe(card)
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      observer.disconnect()
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [place])

  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape' && !document.querySelector('dialog[open]')) onClose()
    }
    function onPointer(event: PointerEvent) {
      const target = event.target as Element
      if (ref.current?.contains(target) || target.closest('[data-cal-trigger], dialog')) return
      onClose()
    }
    document.addEventListener('keydown', onKey)
    document.addEventListener('pointerdown', onPointer)
    return () => {
      document.removeEventListener('keydown', onKey)
      document.removeEventListener('pointerdown', onPointer)
    }
  }, [onClose])

  // Onto an overview's heading; in a list, the first row from the keyboard, else the sheet itself, so no row
  // looks picked on a phone.
  useEffect(() => {
    const card = ref.current
    if (card && (keyboard || phone)) {
      const heading = card.querySelector<HTMLElement>('[data-overview-heading]')
      ;(heading ?? (keyboard ? card.querySelector<HTMLElement>('button, a[href]') : card))?.focus()
    }
    return () => {
      if (keyboard && anchor.isConnected) anchor.focus()
    }
  }, [keyboard, phone, anchor])

  const card = (
    <div
      ref={ref}
      className={['overview', phone ? 'overview-sheet' : '', className].filter(Boolean).join(' ')}
      role="dialog"
      aria-label={label}
      tabIndex={-1}
    >
      {children}
    </div>
  )
  if (!phone) return card
  return (
    <div className="overview-scrim" onPointerDown={(e) => e.target === e.currentTarget && onClose()}>
      {card}
    </div>
  )
}
