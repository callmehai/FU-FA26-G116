import { useCallback, useEffect, useRef, useState } from 'react'
import { getSpeakingQueue } from '../../api/speaking'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { SpeakingQueue } from '../../types/speaking'
import { useAccessLost } from '../auth/useAccessLost'
import { useMeetingEvents } from '../meetings/useMeetingEvents'

/** Events can be lost (no outbox), so a queue with hands up re-reads on its own this often while visible. */
const POLL_MS = 15_000

export type SpeakingState =
  | { kind: 'loading' }
  | { kind: 'loaded'; data: SpeakingQueue }
  | { kind: 'error'; message: string; data: SpeakingQueue | null }
  | { kind: 'gone' }

/**
 * A meeting's queue of raised hands (SCRUM-27, UC-79), for the panel on the detail page and in the call room, and for
 * the room's own "Giơ tay" button and "Đến lượt bạn" banner.
 *
 * It re-reads on `speakingChanged` and `speakingInvited`, on `participantsChanged` (the Chair handed over, or the
 * caller was removed), on `meetingEnded` (which closes every request without a `speakingChanged`), when the window
 * regains focus, and every 15 s while a hand is up and the tab is visible — never two reads at once. Every write
 * answers the whole queue, which is shown at once; a refusal (someone else invited first, the request moved on) is
 * said once and the re-read shows what is true now.
 */
export function useSpeaking(meetingId: string) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<SpeakingState>({ kind: 'loading' })
  const [pending, setPending] = useState(false)
  const generation = useRef(0)
  const inFlight = useRef(false)
  const again = useRef(false)

  const read = useCallback(async () => {
    const current = ++generation.current
    try {
      const data = await getSpeakingQueue(meetingId)
      if (current === generation.current) setState({ kind: 'loaded', data })
    } catch (err) {
      if (current !== generation.current || handleAccessLost(err)) return
      if (isApiError(err, 404)) return setState({ kind: 'gone' })
      setState((s) => ({
        kind: 'error',
        message: errorMessage(err),
        data: s.kind === 'loaded' || s.kind === 'error' ? s.data : null,
      }))
    }
  }, [meetingId, handleAccessLost])

  /** A call while a read runs asks for one more read after it, instead of a second one alongside. */
  const load = useCallback(async () => {
    if (inFlight.current) {
      again.current = true
      return
    }
    inFlight.current = true
    try {
      do {
        again.current = false
        await read()
      } while (again.current)
    } finally {
      inFlight.current = false
    }
  }, [read])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  useMeetingEvents(meetingId, () => void load(), [
    'speakingChanged',
    'speakingInvited',
    'participantsChanged',
    'meetingEnded',
  ])

  const live = state.kind === 'loaded' && state.data.requests.length > 0
  useEffect(() => {
    const onFocus = () => void load()
    window.addEventListener('focus', onFocus)
    const timer = live
      ? window.setInterval(() => document.visibilityState === 'visible' && void load(), POLL_MS)
      : undefined
    return () => {
      window.removeEventListener('focus', onFocus)
      window.clearInterval(timer)
    }
  }, [live, load])

  /** Runs one write and shows the queue it answers; on a refusal says why and re-reads. True when it was saved. */
  const run = useCallback(
    async (write: () => Promise<SpeakingQueue>) => {
      setPending(true)
      try {
        const data = await write()
        // A read started before the write would land after it with the older queue.
        generation.current++
        setState({ kind: 'loaded', data })
        return true
      } catch (err) {
        // 403 MSG457 is "no longer the Chair" (UC-79 EX-4): the caller still reads the meeting, so it is not access
        // lost; the re-read takes the Chair's controls away.
        if (isApiError(err, 403, 'MSG457') || !handleAccessLost(err)) toast.show(errorMessage(err), 'error')
        await load()
        return false
      } finally {
        setPending(false)
      }
    },
    [toast, handleAccessLost, load],
  )

  const data = state.kind === 'loaded' || state.kind === 'error' ? state.data : null
  const mine = data?.requests.find((r) => r.isMine) ?? null

  return { state, data, mine, pending, reload: load, run }
}

export type Speaking = ReturnType<typeof useSpeaking>
