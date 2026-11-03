import { Icon } from './Icon'
import { SHOW_TOOLTIP } from './TooltipLayer'

/**
 * A visible (i) for an explanation that does not fit on the line (ADR-0052). Plain text never
 * carries a hover-only tooltip: nobody hovers a sentence expecting more, and a touch screen cannot
 * hover at all. The (i) says "there is more here"; hovering or focusing it shows the text, and a
 * press (a tap) shows it at once.
 */
export function InfoTip({ text }: { text: string }) {
  return (
    <button
      type="button"
      className="info-tip"
      aria-label={text}
      data-tooltip={text}
      onClick={(event) => {
        // Inside a <label> or a link, the press is the tip's alone: it must not tick or navigate.
        event.preventDefault()
        event.stopPropagation()
        event.currentTarget.dispatchEvent(new Event(SHOW_TOOLTIP, { bubbles: true }))
      }}
    >
      <Icon name="info" size={14} />
    </button>
  )
}
