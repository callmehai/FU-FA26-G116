import { useCallback, useEffect, useRef, useState } from 'react'
import { getAttendance, saveAttendance } from '../../api/attendance'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { AttendancePage, AttendanceRow, AttendanceStatus } from '../../types/attendance'
import { useAccessLost } from '../auth/useAccessLost'
import { useMeetingEvents } from '../meetings/useMeetingEvents'

/** The largest page the API serves; a roster longer than that is read page by page. */
const PAGE_SIZE = 100
/** Events can be lost (no outbox), so an open panel re-reads on its own this often while visible. */
const POLL_MS = 15_000

export type AttendanceData = Omit<AttendancePage, 'items' | 'page' | 'pageSize'> & { rows: AttendanceRow[] }

type State =
  | { kind: 'loading' }
  | { kind: 'loaded'; data: AttendanceData }
  | { kind: 'error'; message: string; data: AttendanceData | null }
  | { kind: 'gone' }

/**
 * The whole roster's attendance (SCRUM-26), shared by the attendees tab and the call room's panel.
 *
 * It re-reads on `attendanceChanged` and `participantsChanged` (a roster change adds or removes
 * rows), when the window regains focus, and every 15 s while the meeting is in progress and the tab
 * is visible — never two reads at once. Nothing is read while `enabled` is false (a meeting that has
 * not started has no attendance to show). `record` saves one person at once, as AI-eCabinet does:
 * picking a value is the save, there is no draft and no Save button.
 */
export function useAttendance(meetingId: string, enabled = true) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<State>({ kind: 'loading' })
  /** Rows being saved, with the value picked, shown at once while the request runs. */
  const [pending, setPending] = useState<ReadonlyMap<string, AttendanceStatus>>(new Map())
  const generation = useRef(0)
  const inFlight = useRef(false)
  const again = useRef(false)

  /** One read of every page; a read overtaken by a newer one or by a save is dropped. */
  const read = useCallback(async () => {
    const current = ++generation.current
    try {
      const first = await getAttendance(meetingId, 1, PAGE_SIZE)
      const rows = [...first.items]
      for (let page = 2; page <= Math.ceil(first.total / PAGE_SIZE); page++) {
        rows.push(...(await getAttendance(meetingId, page, PAGE_SIZE)).items)
      }
      if (current !== generation.current) return
      const { meetingStatus, serverTime, permissions, total, totals } = first
      setState({
        kind: 'loaded',
        data: { meetingId: first.meetingId, meetingStatus, serverTime, permissions, total, totals, rows },
      })
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
    if (!enabled) return
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
  }, [enabled, read])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  useMeetingEvents(meetingId, () => void load(), ['attendanceChanged', 'participantsChanged'])

  const live = state.kind === 'loaded' && state.data.meetingStatus === 'InProgress'
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

  const record = useCallback(
    async (row: AttendanceRow, attendance: AttendanceStatus) => {
      setPending((p) => new Map(p).set(row.id, attendance))
      try {
        const saved = await saveAttendance(meetingId, [
          { id: row.id, kind: row.kind, attendance, expectedVersion: row.rowVersion },
        ])
        // A read that started before this save would bring back the old token: drop it, then re-read.
        generation.current++
        const fresh = new Map(saved.entries.map((e) => [e.id, e]))
        setState((s) =>
          s.kind === 'loaded'
            ? { ...s, data: { ...s.data, rows: s.data.rows.map((r) => ({ ...r, ...fresh.get(r.id) })) } }
            : s,
        )
      } catch (err) {
        // 403 here means "no longer the Secretary" — the meeting itself can still be read.
        if (isApiError(err, 403)) toast.show('Bạn không còn quyền điểm danh cuộc họp này.', 'error')
        else toast.show(errorMessage(err), 'error')
      } finally {
        setPending((p) => {
          const next = new Map(p)
          next.delete(row.id)
          return next
        })
        // MSG151, MSG149 and MSG150 all say "re-read before deciding again"; so does a success.
        void load()
      }
    },
    [meetingId, toast, load],
  )

  return { state, pending, record, reload: load }
}
