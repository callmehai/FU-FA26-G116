import { useCallback, useEffect, useRef, useState } from 'react'
import { getMeeting } from '../../api/meetings'
import { errorMessage, isApiError } from '../../lib/errors'
import type { Meeting } from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'

export type MeetingState =
  | { kind: 'loading' }
  | { kind: 'loaded'; meeting: Meeting }
  /** Unknown id, or a meeting the caller has no relationship with — the backend does not say which. */
  | { kind: 'notFound' }
  | { kind: 'error'; message: string }

/**
 * GET /api/meetings/{id} for a page. `reload` fetches again without flashing the loading state, so
 * a page can refresh what it shows after a conflict. `replace` shows a meeting the server just
 * returned — a transition answers with the updated meeting, so no second request is needed.
 */
export function useMeeting(id: string | undefined) {
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<MeetingState>({ kind: 'loading' })
  /**
   * Bumped by every load, by `replace` and by a change of meeting, so only the newest answer is
   * shown: a reload still in flight for the meeting just left never overwrites the one opened next.
   */
  const generation = useRef(0)

  const load = useCallback(async () => {
    const current = ++generation.current
    if (!id) return setState({ kind: 'notFound' })
    try {
      const meeting = await getMeeting(id)
      if (current === generation.current) setState({ kind: 'loaded', meeting })
    } catch (err) {
      if (current !== generation.current || handleAccessLost(err)) return
      // A path segment that is not a GUID never matches the route on the backend: same 404.
      setState(isApiError(err, 404) ? { kind: 'notFound' } : { kind: 'error', message: errorMessage(err) })
    }
  }, [id, handleAccessLost])

  useEffect(() => {
    const loads = generation
    // Defer past the effect's synchronous phase, as the other pages do.
    void Promise.resolve().then(load)
    // StrictMode runs effects twice in dev, and a new id makes every answer in flight stale.
    return () => {
      loads.current++
    }
  }, [load])

  const retry = useCallback(() => {
    setState({ kind: 'loading' })
    void load()
  }, [load])

  const replace = useCallback((meeting: Meeting) => {
    generation.current++
    setState({ kind: 'loaded', meeting })
  }, [])

  return { state, reload: load, retry, replace }
}
