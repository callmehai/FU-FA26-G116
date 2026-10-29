import { useEffect, useRef } from 'react'

/** Long enough that passing the pointer over a toolbar shows nothing, short enough to feel quick. */
const DELAY_MS = 400
const GAP = 6

/**
 * The app's one tooltip, for any element carrying `data-tooltip` — mostly icon-only buttons, whose
 * name otherwise only screen readers hear. It sits in the top layer, so a scrolling table or a
 * modal dialog never clips it, and shows on hover after a short pause or at once on keyboard focus.
 * The text is a visual aid only: the element keeps its own accessible name (aria-label).
 *
 * Mounted once in the shell.
 */
export function TooltipLayer() {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const tip = ref.current
    if (!tip) return
    let timer: number | undefined
    let target: HTMLElement | null = null

    function hide() {
      window.clearTimeout(timer)
      target = null
      if (tip?.matches(':popover-open')) tip.hidePopover()
    }

    function show(el: HTMLElement) {
      if (!tip || !el.isConnected) return
      const text = el.dataset.tooltip
      if (!text) return
      tip.textContent = text
      tip.showPopover?.()
      const box = el.getBoundingClientRect()
      const width = tip.offsetWidth
      const height = tip.offsetHeight
      const left = Math.min(Math.max(8, box.left + box.width / 2 - width / 2), window.innerWidth - width - 8)
      const above = box.top - height - GAP
      tip.style.left = `${left}px`
      tip.style.top = `${above >= 8 ? above : box.bottom + GAP}px`
    }

    function onOver(event: Event) {
      const el = (event.target as Element | null)?.closest<HTMLElement>('[data-tooltip]') ?? null
      if (el === target) return
      hide()
      if (!el) return
      target = el
      // Keyboard focus is deliberate: no wait.
      const delay = event.type === 'focusin' && el.matches(':focus-visible') ? 0 : DELAY_MS
      timer = window.setTimeout(() => target === el && show(el), delay)
    }

    function onOut(event: Event) {
      const next = (event as PointerEvent | FocusEvent).relatedTarget as Element | null
      if (target && next && target.contains(next)) return
      hide()
    }

    document.addEventListener('pointerover', onOver)
    document.addEventListener('pointerout', onOut)
    document.addEventListener('focusin', onOver)
    document.addEventListener('focusout', onOut)
    document.addEventListener('pointerdown', hide)
    document.addEventListener('keydown', hide)
    window.addEventListener('scroll', hide, true)
    return () => {
      hide()
      document.removeEventListener('pointerover', onOver)
      document.removeEventListener('pointerout', onOut)
      document.removeEventListener('focusin', onOver)
      document.removeEventListener('focusout', onOut)
      document.removeEventListener('pointerdown', hide)
      document.removeEventListener('keydown', hide)
      window.removeEventListener('scroll', hide, true)
    }
  }, [])

  return <div ref={ref} className="tooltip" role="tooltip" popover="manual" />
}
