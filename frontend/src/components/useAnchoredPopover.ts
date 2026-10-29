import { type RefObject, useEffect, useLayoutEffect } from 'react'

/**
 * Lifts a `popover="manual"` element into the browser's top layer and keeps it pinned under (or,
 * without room below, over) its anchor while anything scrolls.
 *
 * Inside a modal dialog nothing else works: the dialog's scrolling body clips an absolutely
 * positioned panel, and z-index cannot lift anything above a `showModal()` dialog — only another
 * top-layer element sits over it.
 */
export function useAnchoredPopover(
  open: boolean,
  anchorRef: RefObject<HTMLElement | null>,
  panelRef: RefObject<HTMLElement | null>,
  { matchWidth = true }: { matchWidth?: boolean } = {},
) {
  useLayoutEffect(() => {
    const panel = panelRef.current
    const anchor = anchorRef.current
    if (!open || !panel || !anchor) return

    panel.showPopover?.()

    function place() {
      if (!panel || !anchor) return
      const box = anchor.getBoundingClientRect()
      const gap = 4
      const below = window.innerHeight - box.bottom
      const upwards = below < panel.offsetHeight + gap * 2 && box.top > below
      // Never past the right edge: on a phone the panel is wider than a half-width field.
      const left = Math.max(gap, Math.min(box.left, window.innerWidth - panel.offsetWidth - gap))

      panel.style.left = `${left}px`
      if (matchWidth) panel.style.minWidth = `${box.width}px`
      panel.style.top = upwards ? 'auto' : `${box.bottom + gap}px`
      panel.style.bottom = upwards ? `${window.innerHeight - box.top + gap}px` : 'auto'
    }

    place()
    // Capture phase: a scroll inside the dialog body does not bubble to window.
    window.addEventListener('scroll', place, true)
    window.addEventListener('resize', place)
    return () => {
      window.removeEventListener('scroll', place, true)
      window.removeEventListener('resize', place)
      if (panel.matches(':popover-open')) panel.hidePopover()
    }
  }, [open, anchorRef, panelRef, matchWidth])
}

/** Closes on a pointer press outside `rootRef` — which a button or an input cannot detect on its own. */
export function useOutsidePress(open: boolean, rootRef: RefObject<HTMLElement | null>, onOutside: () => void) {
  useEffect(() => {
    if (!open) return
    function onPointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) onOutside()
    }
    document.addEventListener('pointerdown', onPointerDown)
    return () => document.removeEventListener('pointerdown', onPointerDown)
  }, [open, rootRef, onOutside])
}
