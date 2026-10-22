import { SENSITIVITIES, type MeetingStatus, type Sensitivity } from '../../types/meeting'
import type { ScheduleEntry } from './useSchedule'

/**
 * The calendar's "Trạng thái" filter works on status groups, as AI-eCabinet's does (its
 * calendarFilters.ts): a draft sits with the ended meetings in the grey group, since neither is
 * on anyone's agenda.
 */
export const GROUPS = ['waiting', 'approved', 'published', 'live', 'ended', 'rejected'] as const
export type StatusGroup = (typeof GROUPS)[number]

export const GROUP_OF: Record<MeetingStatus, StatusGroup> = {
  Draft: 'ended',
  WaitingApproval: 'waiting',
  Rejected: 'rejected',
  Approved: 'approved',
  Published: 'published',
  InProgress: 'live',
  Ended: 'ended',
}

export const GROUP_LABEL: Record<StatusGroup, string> = {
  waiting: 'Chờ duyệt',
  approved: 'Đã duyệt',
  published: 'Đã công bố',
  live: 'Đang diễn ra',
  ended: 'Đã kết thúc',
  rejected: 'Bị từ chối',
}

/** The colour of each group's dot: the status chip's tone. */
export const GROUP_TONE: Record<StatusGroup, string> = {
  waiting: 'amber',
  approved: 'emerald',
  published: 'blue',
  live: 'live',
  ended: 'slate',
  rejected: 'red',
}

/** What the calendar opens on, as in eCab: the meetings that will happen. The rest is one click away. */
export const DEFAULT_GROUPS: readonly StatusGroup[] = ['approved', 'published', 'live']

export interface CalendarFilterValue {
  groups: StatusGroup[]
  /** One level at a time, or none. */
  sensitivity: Sensitivity | null
}

export const DEFAULT_FILTER: CalendarFilterValue = { groups: [...DEFAULT_GROUPS], sensitivity: null }
/** "Bỏ lọc" from the empty state: every meeting the caller may see. */
export const NO_FILTER: CalendarFilterValue = { groups: [...GROUPS], sensitivity: null }

const same = (a: readonly StatusGroup[], b: readonly StatusGroup[]) =>
  a.length === b.length && b.every((g) => a.includes(g))
export const isDefaultGroups = (groups: readonly StatusGroup[]) => same(groups, DEFAULT_GROUPS)
const isAllGroups = (groups: readonly StatusGroup[]) => same(groups, GROUPS)

/**
 * `?status=waiting,live&sensitivity=Secret`. No `status` means the default groups; `status=all`
 * every group. Unknown values are dropped, and a list that drops to nothing reads as the default.
 */
export function readFilter(params: URLSearchParams): CalendarFilterValue {
  const raw = params.get('status')
  const picked = new Set((raw ?? '').split(','))
  const groups = raw === 'all' ? [...GROUPS] : GROUPS.filter((g) => picked.has(g))
  const sensitivity = SENSITIVITIES.find((s) => s === params.get('sensitivity')) ?? null
  return { groups: groups.length > 0 ? groups : [...DEFAULT_GROUPS], sensitivity }
}

export function writeFilter(out: URLSearchParams, filter: CalendarFilterValue) {
  if (isDefaultGroups(filter.groups)) out.delete('status')
  else out.set('status', isAllGroups(filter.groups) ? 'all' : filter.groups.join(','))
  if (filter.sensitivity) out.set('sensitivity', filter.sensitivity)
  else out.delete('sensitivity')
}

/** How many filters differ from what the calendar opens on. */
export function activeGroups(filter: CalendarFilterValue) {
  return (isDefaultGroups(filter.groups) ? 0 : 1) + (filter.sensitivity ? 1 : 0)
}

export function matchesStatus(entry: ScheduleEntry, filter: CalendarFilterValue) {
  return filter.groups.includes(GROUP_OF[entry.meeting.status])
}

export function matchesSensitivity(entry: ScheduleEntry, filter: CalendarFilterValue) {
  return !filter.sensitivity || entry.meeting.sensitivity === filter.sensitivity
}

export function matchesFilter(entry: ScheduleEntry, filter: CalendarFilterValue) {
  return matchesStatus(entry, filter) && matchesSensitivity(entry, filter)
}

/** Adds or drops one group, in the menu's order. The last one stays, so the filter never hides everything. */
export function toggleGroup(groups: readonly StatusGroup[], group: StatusGroup): StatusGroup[] {
  const next = groups.includes(group) ? groups.filter((g) => g !== group) : [...groups, group]
  return next.length > 0 ? GROUPS.filter((g) => next.includes(g)) : [...groups]
}
