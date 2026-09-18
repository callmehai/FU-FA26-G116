import { useEffect, useState } from 'react'
import { getHealth } from '../api/health'

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; version: string }
  | { kind: 'error' }

/** Backend status indicator in the top bar — doubles as the reference example for API calls. */
export function HealthBadge() {
  const [state, setState] = useState<State>({ kind: 'loading' })

  useEffect(() => {
    let cancelled = false

    getHealth()
      .then((data) => {
        if (!cancelled) setState({ kind: 'ok', version: data.version })
      })
      .catch((err) => {
        console.error('Health check thất bại:', err)
        if (!cancelled) setState({ kind: 'error' })
      })

    // The component can unmount before the fetch settles (StrictMode runs effects twice in dev).
    return () => {
      cancelled = true
    }
  }, [])

  const { dot, label } =
    state.kind === 'ok'
      ? { dot: 'dot-ok', label: `Máy chủ hoạt động · v${state.version}` }
      : state.kind === 'error'
        ? { dot: 'dot-bad', label: 'Không kết nối được máy chủ' }
        : { dot: 'dot-wait', label: 'Đang kiểm tra máy chủ…' }

  return (
    <div className="status">
      <span className={`dot ${dot}`} aria-hidden="true" />
      <span>{label}</span>
    </div>
  )
}
