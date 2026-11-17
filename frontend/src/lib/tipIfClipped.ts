/**
 * For an `onPointerOver`/`onFocus` on text that may end in "…": gives the element its whole text as a tooltip when
 * the text is actually cut, and none when it fits, so a short value never surprises with a hover (minimize-text §4).
 * React's handler runs before the shared TooltipLayer's document listener, so the tooltip is there when it looks.
 */
export function tipIfClipped(event: { currentTarget: HTMLElement }) {
  const el = event.currentTarget
  if (el.scrollWidth > el.clientWidth) el.dataset.tooltip = el.textContent ?? ''
  else delete el.dataset.tooltip
}
