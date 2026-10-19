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

export type Schedule =
  { kind: 'loading' } | { kind: 'loaded'; entries: ScheduleEntry[] } | { kind: 'error'; message: string }

/** The API's page limit. A month can hold more, so the range is read page by page. */
const PAGE_SIZE = 100

/**
 * The meetings between `from` and `to`, for the calendar's grids and list. GET /api/meetings?from&to
 * returns the meetings whose own schedule overlaps the range (BR-30), after the server's visibility
 * rule (BR-29), with the times, place and the caller's roles the calendar shows and filters on.
 * Every page is read: the calendar shows the whole range, never a first page of it.
 */
export function useSchedule(from: Date, to: Date, attempt: number): Schedule {
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<Schedule>({ kind: 'loading' })
  const fromIso = from.toISOString()
  const toIso = to.toISOString()

  useEffect(() => {
    let cancelled = false
    void Promise.resolve().then(async () => {
      if (cancelled) return
      setState((s) => (s.kind === 'loaded' ? s : { kind: 'loading' }))
      try {
        // Keyed by id: a meeting created while the pages load can shift a row onto the next page.
        const byId = new Map<string, MeetingListItem>()
        for (let page = 1; ; page++) {
          const result = await listMeetings({
            from: fromIso,
            to: toIso,
            page,
            pageSize: PAGE_SIZE,
            sortBy: 'plannedStartTime',
            sortDir: 'asc',
          })
          if (cancelled) return
          for (const meeting of result.items) byId.set(meeting.id, meeting)
          if (page >= result.totalPages) break
        }
        const entries = [...byId.values()].map((meeting) => ({
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
