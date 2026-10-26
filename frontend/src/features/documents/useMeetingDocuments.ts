import { useCallback, useEffect, useRef, useState } from 'react'
import { getMeetingDocuments } from '../../api/documents'
import { errorMessage, isApiError } from '../../lib/errors'
import type { MeetingDocumentList } from '../../types/document'
import { useAccessLost } from '../auth/useAccessLost'
import { useMeetingEvents } from '../meetings/useMeetingEvents'

type State =
  | { kind: 'loading' }
  | { kind: 'loaded'; data: MeetingDocumentList }
  /** MSG157 must never read as an empty list; the last good list, if any, stays on screen. */
  | { kind: 'error'; message: string; data: MeetingDocumentList | null }
  /** 404 MSG156: the meeting can no longer be read. */
  | { kind: 'gone' }

/**
 * The meeting's documents (UC-32a), shared by the Tài liệu tab and the call room's panel. Re-reads on
 * `documentsChanged` (an upload), on `agendaChanged` (an item added, renamed or started) and when the
 * hub comes back after a gap. A read overtaken by a newer one is dropped.
 */
export function useMeetingDocuments(meetingId: string) {
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const generation = useRef(0)

  const load = useCallback(async () => {
    const current = ++generation.current
    try {
      const data = await getMeetingDocuments(meetingId)
      if (current === generation.current) setState({ kind: 'loaded', data })
    } catch (err) {
      if (current !== generation.current || handleAccessLost(err)) return
      // MSG156, or an id that is not a GUID (no body): the same "gone".
      if (isApiError(err, 404)) return setState({ kind: 'gone' })
      setState((s) => ({
        kind: 'error',
        message: errorMessage(err),
        data: s.kind === 'loaded' || s.kind === 'error' ? s.data : null,
      }))
    }
  }, [meetingId, handleAccessLost])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  useMeetingEvents(meetingId, () => void load(), ['documentsChanged', 'agendaChanged'])

  return { state, reload: load }
}
