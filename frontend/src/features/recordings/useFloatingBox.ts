import { useCallback, useEffect, useRef, useState, type PointerEvent } from 'react'

/**
 * A fixed box the user moves anywhere on the screen and widens from either side, as AI-eCabinet's floating mic box.
 * It is placed from the window's bottom-right corner, so the default place holds on any screen; where it was left and
 * how wide are remembered on this device. Nothing else in the corner moves it: the upload tray and the toasts may pass
 * over it, and the user drags it out of their way.
 */

export type BoxRect = { right: number; bottom: number; width: number | null }
export type Edge = 'w' | 'e'

const MARGIN = 8
/** Pressing a control does not move the box; a button marked `data-drag` (the bubble) does. */
const CONTROL = 'button:not([data-drag]), a, label, input, textarea, [data-edge]'

function load(key: string, fallback: BoxRect): BoxRect {
  try {
    const stored = JSON.parse(localStorage.getItem(key) ?? 'null') as BoxRect | null
    return stored && Number.isFinite(stored.right) && Number.isFinite(stored.bottom)
      ? { right: stored.right, bottom: stored.bottom, width: stored.width ?? null }
      : fallback
  } catch {
    return fallback
  }
}

/** Pulls the box back inside the window: after a drag, a resize of the window, or a change of what it holds. */
function keepIn(rect: BoxRect, el: HTMLElement | null): BoxRect {
  const width = rect.width ?? el?.offsetWidth ?? 0
  const height = el?.offsetHeight ?? 0
  return {
    ...rect,
    right: Math.min(Math.max(MARGIN, rect.right), Math.max(MARGIN, window.innerWidth - width - MARGIN)),
    bottom: Math.min(Math.max(MARGIN, rect.bottom), Math.max(MARGIN, window.innerHeight - height - MARGIN)),
  }
}

type Gesture = { edge: Edge | null; x: number; y: number; from: { right: number; bottom: number; width: number } }

export function useFloatingBox(key: string, fallback: BoxRect, minWidth: number, maxWidth: number) {
  const ref = useRef<HTMLDivElement | null>(null)
  const [rect, setRect] = useState(() => load(key, fallback))
  const [moving, setMoving] = useState(false)
  const gesture = useRef<Gesture | null>(null)
  const latest = useRef(rect)
  useEffect(() => {
    latest.current = rect
  })

  useEffect(() => {
    const fit = () => setRect((r) => keepIn(r, ref.current))
    fit()
    window.addEventListener('resize', fit)
    return () => window.removeEventListener('resize', fit)
  }, [])

  const begin = (event: PointerEvent<HTMLElement>, edge: Edge | null) => {
    const el = ref.current
    if (!el) return
    const { right, bottom } = latest.current
    gesture.current = { edge, x: event.clientX, y: event.clientY, from: { right, bottom, width: el.offsetWidth } }
    event.currentTarget.setPointerCapture(event.pointerId)
    if (!edge) setMoving(true)
  }

  const finish = useCallback(() => {
    if (!gesture.current) return
    gesture.current = null
    setMoving(false)
    try {
      localStorage.setItem(key, JSON.stringify(latest.current))
    } catch {
      // A private window: the place lasts for this page.
    }
  }, [key])

  const onPointerMove = useCallback(
    (event: PointerEvent<HTMLElement>) => {
      const g = gesture.current
      if (!g) return
      // Released somewhere the box never heard of (capture lost, let go off the page): the drag is over.
      if (event.buttons === 0) return finish()
      const dx = event.clientX - g.x
      const { from } = g
      if (!g.edge) {
        const dy = event.clientY - g.y
        setRect((r) => keepIn({ ...r, right: from.right - dx, bottom: from.bottom - dy }, ref.current))
        return
      }
      const width = Math.round(Math.min(maxWidth, Math.max(minWidth, from.width + (g.edge === 'w' ? -dx : dx))))
      // Widening from the right side moves the right edge; from the left, the box grows leftwards.
      const right = g.edge === 'e' ? from.right - (width - from.width) : from.right
      setRect(keepIn({ right, bottom: from.bottom, width }, ref.current))
    },
    [finish, maxWidth, minWidth],
  )

  const end = { onPointerMove, onPointerUp: finish, onPointerCancel: finish, onLostPointerCapture: finish }
  return {
    ref,
    rect,
    moving,
    /** After the box changes size: pull it back inside the window. */
    refit: useCallback(() => setRect((r) => keepIn(r, ref.current)), []),
    move: {
      ...end,
      onPointerDown: (event: PointerEvent<HTMLElement>) => {
        if (event.button !== 0 || (event.target as Element).closest(CONTROL)) return
        begin(event, null)
      },
    },
    edge: (edge: Edge) => ({
      ...end,
      'data-edge': edge,
      onPointerDown: (event: PointerEvent<HTMLElement>) => {
        if (event.button !== 0) return
        event.stopPropagation()
        begin(event, edge)
      },
    }),
  }
}
