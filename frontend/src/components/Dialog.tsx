import { useEffect, useId, useRef, type ReactNode } from 'react'
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
}: {
  title: string
  description?: ReactNode
  onClose: () => void
  children?: ReactNode
  footer?: ReactNode
}) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()

  useEffect(() => {
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
      className="dialog"
      aria-labelledby={titleId}
      onCancel={(event) => {
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
