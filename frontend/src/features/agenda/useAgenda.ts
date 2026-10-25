import { useCallback, useEffect, useRef, useState } from 'react'
import { getAgenda, reorderAgenda } from '../../api/agenda'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { AgendaMutationResponse, AgendaSnapshot } from '../../types/agenda'
import { useAccessLost } from '../auth/useAccessLost'
import { useMeetingEvents } from '../meetings/useMeetingEvents'

type State =
  | { kind: 'loading' }
  | { kind: 'loaded'; data: AgendaSnapshot }
  | { kind: 'error'; message: string; data: AgendaSnapshot | null }
  | { kind: 'gone' }

/**
 * A meeting's agenda (SCRUM-25, SCRUM-98), shared by the detail tab and the call room's panel.
 *
 * It re-reads on `agendaChanged`, on `participantsChanged` (removing someone clears the items they
 * present) and when the window regains focus — never two reads at once. Every write answers only a
 * message, so `run` shows it and re-reads; a failure (someone else's change, a lock, a lost role)
 * is said once and the re-read shows what is true now.
 */
export function useAgenda(meetingId: string) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const generation = useRef(0)
  const inFlight = useRef(false)
  const again = useRef(false)

  const read = useCallback(async () => {
    const current = ++generation.current
    try {
      const data = await getAgenda(meetingId)
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

  useMeetingEvents(meetingId, () => void load(), ['agendaChanged', 'participantsChanged'])

  useEffect(() => {
    const onFocus = () => void load()
    window.addEventListener('focus', onFocus)
    return () => window.removeEventListener('focus', onFocus)
  }, [load])

  /**
   * Runs one write, says how it went, then re-reads; resolves once the re-read is in, true when it was
   * saved. A `quiet` write
   * (a drop after dragging) shows nothing on success: the list itself already shows the result.
   */
  const run = useCallback(
    async (write: () => Promise<AgendaMutationResponse>, quiet = false) => {
      try {
        const saved = await write()
        if (!quiet) toast.show(saved.message)
        return true
      } catch (err) {
        if (!handleAccessLost(err)) toast.show(errorMessage(err), 'error')
        return false
      } finally {
        await load()
      }
    },
    [toast, handleAccessLost, load],
  )

  /**
   * Shows the new order at once; the server's answer (or a 409 MSG143 re-read) settles it. Until the
   * re-read is in, `reordering` holds further drags: the next one needs the new `orderVersion`.
   */
  const [reordering, setReordering] = useState(false)
  const reorder = useCallback(
    async (ids: string[]) => {
      if (state.kind !== 'loaded') return
      const { data } = state
      const byId = new Map(data.items.map((item) => [item.id, item]))
      // A read in flight would bring the old order back over the new one: drop it.
      generation.current++
      setState({ kind: 'loaded', data: { ...data, items: ids.map((id) => byId.get(id)!) } })
      setReordering(true)
      await run(() => reorderAgenda(meetingId, data.orderVersion, ids), true)
      setReordering(false)
    },
    [state, meetingId, run],
  )

  return { state, reload: load, run, reorder, reordering }
}
