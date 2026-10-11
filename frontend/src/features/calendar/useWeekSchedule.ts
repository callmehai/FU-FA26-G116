import { useEffect, useState } from 'react'
import { listMeetings } from '../../api/meetings'
import { errorMessage } from '../../lib/errors'
import type { MeetingListItem } from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'

/** One meeting on the calendar. A meeting is one sitting, so it carries its own times (ADR-0013). */
export interface ScheduleEntry {
  meeting: MeetingListItem
  start: Date
  end: Date
}

export type WeekSchedule =
  | { kind: 'loading' }
  | { kind: 'loaded'; entries: ScheduleEntry[] }
  | { kind: 'error'; message: string }

/** The API's page limit. A week rarely holds more; past this the calendar would need paging. */
const PAGE_SIZE = 100

/**
 * The meetings between `from` and `to`, for the calendar's grid and list views. One request:
 * GET /api/meetings?from&to returns the meetings whose own schedule overlaps the range (BR-30),
 * after the server's visibility rule, with the times, mode and place the calendar shows.
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
          sortBy: 'plannedStartTime',
          sortDir: 'asc',
        })
        if (cancelled) return
        const entries = page.items.map((meeting) => ({
          meeting,
          start: new Date(meeting.plannedStartTime),
          end: new Date(meeting.plannedEndTime),
        }))
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
