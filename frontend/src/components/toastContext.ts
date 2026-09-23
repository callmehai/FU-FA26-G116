import { createContext, useContext } from 'react'

export type ToastTone = 'success' | 'error' | 'info'

export interface ToastApi {
  show: (message: string, tone?: ToastTone) => void
}

/** Kept apart from the provider so the module exports no component — fast refresh needs that. */
export const ToastContext = createContext<ToastApi | null>(null)

export function useToast() {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>')
  return ctx
}
