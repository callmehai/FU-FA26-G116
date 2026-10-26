import { useId, useLayoutEffect, useRef, type ReactNode } from 'react'
import { Icon } from './Icon'

/**
 * Modal dialog on the native `<dialog>` element, which brings focus trapping, the backdrop and
 * Escape handling with it.
 *
 * Open by mounting it and close by unmounting it: whoever renders it owns the open state, and a
 * form inside starts fresh every time. Escape asks `onClose` rather than closing directly, so a
 * dialog in the middle of a request can refuse by passing a no-op.
 */
export function Dialog({
  title,
  description,
  onClose,
  children,
  footer,
  size = 'md',
}: {
  title: string
  description?: ReactNode
  onClose: () => void
  children?: ReactNode
  footer?: ReactNode
  /** `lg` holds a whole form (create or edit a meeting); on a phone it takes the screen. */
  size?: 'md' | 'lg'
}) {
  const ref = useRef<HTMLDialogElement>(null)
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
      className={size === 'lg' ? 'dialog dialog-lg' : 'dialog'}
      aria-labelledby={titleId}
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
