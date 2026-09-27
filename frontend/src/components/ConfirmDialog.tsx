import { useState, type ReactNode } from 'react'
import { errorMessage } from '../lib/errors'
import { Dialog } from './Dialog'

/**
 * Asks before an action takes effect, then runs it.
 *
 * A failure stays inside the dialog instead of becoming a toast: the dialog would cover a toast,
 * and the user is still looking here. The dialog closes only when `onConfirm` resolves, and
 * cannot be dismissed while the request is in flight.
 */
export function ConfirmDialog({
  title,
  children,
  confirmLabel,
  pendingLabel,
  tone = 'primary',
  blocker,
  onConfirm,
  onClose,
}: {
  title: string
  children: ReactNode
  confirmLabel: string
  pendingLabel: string
  tone?: 'primary' | 'danger'
  /**
   * Why the action cannot go ahead, when that is known before asking: shown where a failure
   * would be, and the confirm button stays disabled so no request is sent.
   */
  blocker?: string
  /** Throw to keep the dialog open; the thrown error is shown via `errorMessage`. */
  onConfirm: () => Promise<void>
  onClose: () => void
}) {
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function confirm() {
    setPending(true)
    setError(null)
    try {
      await onConfirm()
      onClose()
    } catch (err) {
      setError(errorMessage(err))
      setPending(false)
    }
  }

  const close = () => {
    if (!pending) onClose()
  }

  return (
    <Dialog
      title={title}
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-ghost" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button
            type="button"
            className={`btn ${tone === 'danger' ? 'btn-danger' : 'btn-primary'}`}
            onClick={confirm}
            disabled={pending || blocker !== undefined}
          >
            {pending ? pendingLabel : confirmLabel}
          </button>
        </>
      }
    >
      <p className="dialog-text">{children}</p>
      {(error ?? blocker) && (
        <p className="alert alert-error dialog-error" role="alert">
          {error ?? blocker}
        </p>
      )}
    </Dialog>
  )
}
