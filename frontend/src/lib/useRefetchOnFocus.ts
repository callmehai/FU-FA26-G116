import { useEffect, useRef } from 'react'

/** How long a screen may show what it read before coming back to the tab reads it again. */
const DEFAULT_MIN_INTERVAL_MS = 30_000

/**
 * Calls `refresh` when the tab comes back to the foreground (window focus or the page turning visible) and the
 * screen was last read at least `minIntervalMs` ago. It catches what another user changed without telling this one:
 * a list a change never notifies about would otherwise stay as it was until a reload. Pass `enabled: false` while the
 * screen holds unsaved edits, so a re-read never clobbers them; the next focus after the edits are saved or discarded
 * reads again.
 */
export function useRefetchOnFocus(
  refresh: () => void,
  { enabled = true, minIntervalMs = DEFAULT_MIN_INTERVAL_MS }: { enabled?: boolean; minIntervalMs?: number } = {},
) {
  const refreshRef = useRef(refresh)
  const enabledRef = useRef(enabled)
  useEffect(() => {
    refreshRef.current = refresh
    enabledRef.current = enabled
  })

  useEffect(() => {
    // The screen read itself as it mounted.
    let last = Date.now()
    const onForeground = () => {
      if (document.visibilityState !== 'visible' || !enabledRef.current) return
      const now = Date.now()
      if (now - last < minIntervalMs) return
      last = now
      refreshRef.current()
    }
    window.addEventListener('focus', onForeground)
    document.addEventListener('visibilitychange', onForeground)
    return () => {
      window.removeEventListener('focus', onForeground)
      document.removeEventListener('visibilitychange', onForeground)
    }
  }, [minIntervalMs])
}
