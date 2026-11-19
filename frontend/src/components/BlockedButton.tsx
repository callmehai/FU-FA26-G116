import { useId, type ButtonHTMLAttributes, type Ref } from 'react'
import { SHOW_TOOLTIP } from './TooltipLayer'

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  /** Why the button cannot be used now, in a few words ("Mở khi cuộc họp kết thúc"); empty when it can. */
  blocked?: string | false | null
  ref?: Ref<HTMLButtonElement>
}

/**
 * A button that may be unusable for now, and then says why — AI-eCabinet's BlockedReason and locked tab. A native
 * `disabled` button takes no focus and no tap, so its reason could only ever be hovered with a mouse. While `blocked`,
 * the button is `aria-disabled` instead: it looks disabled, does nothing, and stays reachable — its reason shows on
 * hover and keyboard focus (the app's one tooltip, `data-tooltip`), and at once on a press, which is how a touch
 * screen reaches it. Screen readers hear the reason as the button's description.
 *
 * `disabled` still means "busy for a moment" (a request in flight) and greys the button the native way; the server
 * re-checks whatever a blocked button guards.
 */
export function BlockedButton({ blocked, type = 'button', ...props }: Props) {
  const reasonId = useId()
  if (!blocked) return <button type={type} {...props} />

  // Neither the action nor the busy state applies while blocked: a native `disabled` would take the focus away again.
  const { disabled: _busy, onClick: _action, 'aria-describedby': describedBy, ...rest } = props
  return (
    <>
      <button
        {...rest}
        // Never a submit while blocked: Enter in a form must not send it either.
        type="button"
        aria-disabled="true"
        aria-describedby={describedBy ? `${describedBy} ${reasonId}` : reasonId}
        data-tooltip={blocked}
        onClick={(event) => {
          event.preventDefault()
          event.stopPropagation()
          event.currentTarget.dispatchEvent(new Event(SHOW_TOOLTIP, { bubbles: true }))
        }}
      />
      <span id={reasonId} hidden>
        {blocked}
      </span>
    </>
  )
}
