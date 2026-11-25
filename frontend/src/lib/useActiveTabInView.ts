import { useLayoutEffect, useRef, useState } from 'react'

/** As wide as the fade on a narrow tab bar's right edge (`.tabs` in global.css): a tab under it reads as cut off. */
const FADE = 32

/**
 * On a phone the tab bar scrolls sideways, and a link straight to a later tab (`?tab=recordings`) opened with that tab
 * off screen (QA FE-09). Returns the bar's ref: the selected tab is scrolled into the bar on arrival and on every
 * change. Only the bar scrolls, never the page, which `scrollIntoView` would also move.
 */
export function useActiveTabInView(active: string) {
  const [bar, setBar] = useState<HTMLElement | null>(null)
  const arrived = useRef(false)

  useLayoutEffect(() => {
    if (!bar) return
    const reveal = (behavior: ScrollBehavior) => {
      const tab = bar.querySelector<HTMLElement>('[aria-selected="true"]')
      if (!tab) return
      const edges = bar.getBoundingClientRect()
      const box = tab.getBoundingClientRect()
      const fade = bar.scrollWidth > bar.clientWidth ? FADE : 0
      const by =
        box.left < edges.left
          ? box.left - edges.left
          : box.right > edges.right - fade
            ? box.right - edges.right + fade
            : 0
      if (by !== 0) bar.scrollBy({ left: by, behavior })
    }
    // The first one is where the page opens, so it is already there; a tap on a half-hidden tab slides the bar.
    reveal(arrived.current ? 'smooth' : 'instant')
    arrived.current = true

    // The tabs change width after that first measure: it often runs in the fallback font, ~16px narrower than
    // Be Vietnam Pro once that arrives (QA FE-09 AC20), and turning a phone resizes the bar. One ResizeObserver on
    // the bar and its tabs catches both, where `document.fonts.ready` would miss rotation and a font subset that
    // starts loading later. It reports every size once on `observe`, which the scroll above has already handled, so
    // that report is skipped rather than cutting a tap's slide short.
    let first = true
    const resized = new ResizeObserver(() => {
      if (first) first = false
      else reveal('instant')
    })
    resized.observe(bar)
    for (const tab of bar.children) resized.observe(tab)
    return () => resized.disconnect()
  }, [bar, active])

  return setBar
}
