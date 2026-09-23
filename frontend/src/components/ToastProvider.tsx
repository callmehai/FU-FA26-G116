import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import { ToastContext, type ToastTone } from './toastContext'

interface Toast {
  id: number
  message: string
  tone: ToastTone
}

const VISIBLE_MS = 4500

/**
 * Short confirmations that an action went through ("Đã lưu"), stacked in a corner and gone after a
 * few seconds. Errors the user must act on belong next to what failed, not here.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(0)

  const dismiss = useCallback((id: number) => setToasts((all) => all.filter((t) => t.id !== id)), [])

  const show = useCallback(
    (message: string, tone: ToastTone = 'success') => {
      const id = nextId.current++
      setToasts((all) => [...all, { id, message, tone }])
      window.setTimeout(() => dismiss(id), VISIBLE_MS)
    },
    [dismiss],
  )

  const value = useMemo(() => ({ show }), [show])

  return (
    <ToastContext value={value}>
      {children}
      <div className="toast-stack" role="status" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast toast-${toast.tone}`}>
            <i className="toast-dot" aria-hidden="true" />
            <span>{toast.message}</span>
            <button type="button" className="toast-close" onClick={() => dismiss(toast.id)} aria-label="Đóng">
              ×
            </button>
          </div>
        ))}
      </div>
    </ToastContext>
  )
}
