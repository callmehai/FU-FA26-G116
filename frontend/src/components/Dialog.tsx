import { useId, useLayoutEffect, useRef, type ReactNode } from 'react'
import { Icon } from './Icon'

/**
 * Modal dialog on the native `<dialog>` element, which brings focus trapping, the backdrop and
 * Escape handling with it.
 *
 * Open by mounting it and close by unmounting it: whoever renders it owns the open state, and a
 * form inside starts fresh every time. Escape asks `onClose` rather than closing directly, so a
 * dialog in the middle of a request can refuse by passing a no-op.
 *
 * A sheet (a list read beside the page, nothing typed into it) also closes on a click outside it, as AI-eCabinet's
 * sheets do; a dialog holding a form does not, so a stray click never throws away what was typed.
 */
export function Dialog({
  title,
  description,
  onClose,
  children,
  footer,
  size = 'md',
  dismissOnBackdrop = size === 'sheet',
}: {
  title: string
  description?: ReactNode
  onClose: () => void
  children?: ReactNode
  footer?: ReactNode
  /** `lg` holds a whole form (create or edit a meeting); on a phone it takes the screen. `sheet` slides in from the
   * right edge at full height, for a list read beside the page. */
  size?: 'md' | 'lg' | 'xl' | 'sheet'
  /** A click on the backdrop closes it, through `onClose`. Defaults to sheets only. */
  dismissOnBackdrop?: boolean
}) {
  const ref = useRef<HTMLDialogElement>(null)
  // Pressed outside and let go outside: a text selection dragged out of the dialog does not close it.
  const pressedOutside = useRef(false)
  const outside = (event: { target: EventTarget; clientX: number; clientY: number }) => {
    const dialog = ref.current
    if (!dialog || event.target !== dialog) return false
    const r = dialog.getBoundingClientRect()
    return event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom
  }
  const titleId = useId()

  // A layout effect, not a passive one: its cleanup runs before React removes the <dialog> from the
  // page. `close()` on a dialog that is already detached cannot hand focus back to the button that
  // opened it, and keyboard users are left on <body>.
  useLayoutEffect(() => {
    const dialog = ref.current
    if (dialog && !dialog.open) {
      dialog.showModal()
      // showModal focuses the first focusable element, which is the close button in the header.
      // Start on the first field instead; with no field, on the first footer button — "Huỷ",
      // the safe choice for a confirmation.
      dialog
        .querySelector<HTMLElement>('.dialog-body :is(input, textarea, button), .dialog-foot button')
        ?.focus()
    }
    return () => dialog?.close()
  }, [])

  return (
    <dialog
      ref={ref}
      className={size === 'md' ? 'dialog' : `dialog dialog-${size}`}
      aria-labelledby={titleId}
      onPointerDown={(event) => {
        pressedOutside.current = dismissOnBackdrop && outside(event)
      }}
      onClick={(event) => {
        if (pressedOutside.current && outside(event)) onClose()
        pressedOutside.current = false
      }}
      onCancel={(event) => {
        // A dialog opened inside this one (a ConfirmDialog) handles its own Escape; React bubbles
        // that `cancel` up here, where it must not close — or reopen — anything.
        if (event.target !== event.currentTarget) return
        event.preventDefault()
        onClose()
      }}
    >
      <header className="dialog-head">
        <h2 id={titleId}>{title}</h2>
        {description && <p className="dialog-desc">{description}</p>}
        <button type="button" className="icon-button dialog-close" onClick={onClose} aria-label="Đóng">
          <Icon name="x" size={16} />
        </button>
      </header>
      {children && <div className="dialog-body">{children}</div>}
      {footer && <footer className="dialog-foot">{footer}</footer>}
    </dialog>
  )
}
