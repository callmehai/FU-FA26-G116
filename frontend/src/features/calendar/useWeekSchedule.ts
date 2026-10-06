import { useEffect, useState } from 'react'
import { getMeeting, listMeetings } from '../../api/meetings'
import { listMeetingSessions } from '../../api/meetingSessions'
import { errorMessage, isApiError } from '../../lib/errors'
import type { Meeting } from '../../types/meeting'
import type { MeetingSession } from '../../types/meetingSession'
import { useAccessLost } from '../auth/useAccessLost'

/** One session on the calendar, with the meeting it belongs to. */
export interface ScheduleEntry {
  session: MeetingSession
  meeting: Meeting
  start: Date
  end: Date
}

export type WeekSchedule =
  | { kind: 'loading' }
  | { kind: 'loaded'; entries: ScheduleEntry[] }
  /** The backend has no session endpoint yet (SCRUM-68 not deployed): nothing can be placed. */
  | { kind: 'unavailable' }
  | { kind: 'error'; message: string }

/** A week rarely holds more; past this the calendar would need its own endpoint anyway. */
const PAGE_SIZE = 50

/**
 * The sessions between `from` and `to`, for the calendar's grid and list views.
 *
 * There is no "sessions in a date range" endpoint, so this composes three that exist:
 * GET /api/meetings?from&to finds the meetings with a session in the range (after the server's
 * visibility rule), then each meeting's sessions and details are read. That is 2N + 1 requests —
 * fine for a working week, and the reason a range endpoint is worth asking the backend for.
 *
 * Cancelled sessions are left out. A session endpoint that answers 404 for every meeting the list
 * just returned means the endpoint itself is missing, which is reported as `unavailable` rather
 * than as an empty week.
 */
export function useWeekSchedule(from: Date, to: Date, attempt: number): WeekSchedule {
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<WeekSchedule>({ kind: 'loading' })
  const fromIso = from.toISOString()
  const toIso = to.toISOString()

  useEffect(() => {
    let cancelled = false
    void Promise.resolve().then(async () => {
      if (cancelled) return
      setState((s) => (s.kind === 'loaded' ? s : { kind: 'loading' }))
      try {
        const page = await listMeetings({
          from: fromIso,
          to: toIso,
          page: 1,
          pageSize: PAGE_SIZE,
          sortBy: 'createdAt',
          sortDir: 'desc',
        })
        const results = await Promise.all(
          page.items.map(async (item) => {
            try {
              const [sessions, meeting] = await Promise.all([listMeetingSessions(item.id), getMeeting(item.id)])
              return { sessions, meeting }
            } catch (err) {
              // Deleted, or no longer readable, between the list and this call.
              if (isApiError(err, 404)) return null
              throw err
            }
          }),
        )
        if (cancelled) return
        if (page.items.length > 0 && results.every((r) => r === null)) {
          setState({ kind: 'unavailable' })
          return
        }
        const start = new Date(fromIso).getTime()
        const end = new Date(toIso).getTime()
        const entries = results
          .flatMap((r) => (r ? r.sessions.map((session) => ({ session, meeting: r.meeting })) : []))
          .filter(({ session }) => session.status !== 'Cancelled')
          .map((e) => ({ ...e, start: new Date(e.session.plannedStartsAt), end: new Date(e.session.plannedEndsAt) }))
          .filter((e) => e.end.getTime() > start && e.start.getTime() <= end)
          .sort((a, b) => a.start.getTime() - b.start.getTime())
        setState({ kind: 'loaded', entries })
      } catch (err) {
        if (cancelled || handleAccessLost(err)) return
        setState({ kind: 'error', message: errorMessage(err) })
      }
    })
    return () => {
      cancelled = true
    }
  }, [fromIso, toIso, attempt, handleAccessLost])

  return state
}
