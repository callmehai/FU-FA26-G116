import { MEETING_STATUS_LABEL, SENSITIVITIES, type MeetingStatus, type Sensitivity } from '../../types/meeting'
import type { ScheduleEntry } from './useSchedule'

export const STATUSES = Object.keys(MEETING_STATUS_LABEL) as MeetingStatus[]

/** An empty group does not filter on that group. */
export interface CalendarFilterValue {
  statuses: MeetingStatus[]
  sensitivities: Sensitivity[]
}

export const NO_FILTER: CalendarFilterValue = { statuses: [], sensitivities: [] }

/** `?status=Draft,Approved&sensitivity=Secret`; unknown or repeated values are dropped. */
export function readFilter(params: URLSearchParams): CalendarFilterValue {
  return {
    statuses: readList(params.get('status'), STATUSES),
    sensitivities: readList(params.get('sensitivity'), SENSITIVITIES),
  }
}

export function writeFilter(out: URLSearchParams, filter: CalendarFilterValue) {
  for (const [key, values] of [
    ['status', filter.statuses],
    ['sensitivity', filter.sensitivities],
  ] as const) {
    if (values.length > 0) out.set(key, values.join(','))
    else out.delete(key)
  }
}

export function activeGroups(filter: CalendarFilterValue) {
  return (filter.statuses.length > 0 ? 1 : 0) + (filter.sensitivities.length > 0 ? 1 : 0)
}

export function matchesStatus(entry: ScheduleEntry, filter: CalendarFilterValue) {
  return filter.statuses.length === 0 || filter.statuses.includes(entry.meeting.status)
}

export function matchesSensitivity(entry: ScheduleEntry, filter: CalendarFilterValue) {
  return filter.sensitivities.length === 0 || filter.sensitivities.includes(entry.meeting.sensitivity)
}

export function matchesFilter(entry: ScheduleEntry, filter: CalendarFilterValue) {
  return matchesStatus(entry, filter) && matchesSensitivity(entry, filter)
}

function readList<T extends string>(value: string | null, allowed: readonly T[]): T[] {
  const picked = new Set((value ?? '').split(','))
  // In the order of `allowed`, so the URL a chip writes does not depend on the click order.
  return allowed.filter((item) => picked.has(item))
}

/** Adds or removes `item`, keeping the order of `all`. */
export function toggle<T>(list: T[], item: T, all: readonly T[]) {
  return list.includes(item) ? list.filter((x) => x !== item) : all.filter((x) => x === item || list.includes(x))
}
