import { addDays, endOfDay, startOfWeek } from '../calendar/dates'
import {
  MEETING_STATUSES,
  SENSITIVITIES,
  type MeetingListQuery,
  type MeetingSortBy,
  type MeetingTiming,
  type MeetingStatus,
  type Sensitivity,
  type SortDirection,
} from '../../types/meeting'

/**
 * The meeting list's criteria live in the URL, so a filtered page survives a reload, can be shared as
 * a link, and Back returns to it (BR-32: criteria are kept across pages).
 *
 * Whatever is in the URL is read defensively: an unknown value is dropped rather than sent, so a
 * hand-edited or outdated link falls back to the default instead of failing with a 400.
 *
 * As in AI-eCabinet, the list opens on what is happening now: no `status` in the URL means
 * "Đang diễn ra", and `status=all` is the explicit "every status".
 */
export interface ListState {
  q: string
  status: MeetingStatus | ''
  sensitivity: Sensitivity | ''
  when: When | ''
  page: number
  pageSize: number
  sortBy: MeetingSortBy
  sortDir: SortDirection
}

/**
 * The "Thời điểm" presets, AI-eCabinet's: the two windows people ask for, and before or after now.
 * No free date range — the calendar is where a particular day is looked up.
 */
export const WHEN_OPTIONS = ['today', 'week', 'upcoming', 'past'] as const
export type When = (typeof WHEN_OPTIONS)[number]

export const WHEN_LABEL: Record<When, string> = {
  today: 'Hôm nay',
  week: 'Tuần này',
  upcoming: 'Chưa diễn ra',
  past: 'Đã diễn ra',
}

const ALL_STATUSES = 'all'

export const PAGE_SIZES = [10, 20, 50] as const
const SORT_COLUMNS: readonly MeetingSortBy[] = ['title', 'plannedStartTime', 'createdAt']

/** A name sorts A→Z first; a time sorts newest first. Same defaults as the backend. */
export function defaultDirection(column: MeetingSortBy): SortDirection {
  return column === 'title' ? 'asc' : 'desc'
}

export const DEFAULT_STATE: ListState = {
  q: '',
  status: 'InProgress',
  sensitivity: '',
  when: '',
  page: 1,
  pageSize: 20,
  sortBy: 'plannedStartTime',
  sortDir: 'desc',
}

function oneOf<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((candidate) => candidate === value)
}

export function readListState(params: URLSearchParams): ListState {
  const sortBy = oneOf(params.get('sortBy'), SORT_COLUMNS) ?? DEFAULT_STATE.sortBy
  const page = Number(params.get('page'))
  const pageSize = Number(params.get('pageSize'))
  const status = params.get('status')

  return {
    q: params.get('q') ?? '',
    status: status === ALL_STATUSES ? '' : (oneOf(status, MEETING_STATUSES) ?? DEFAULT_STATE.status),
    sensitivity: oneOf(params.get('sensitivity'), SENSITIVITIES) ?? '',
    when: oneOf(params.get('when'), WHEN_OPTIONS) ?? '',
    page: Number.isInteger(page) && page >= 1 ? page : 1,
    pageSize: (PAGE_SIZES as readonly number[]).includes(pageSize) ? pageSize : DEFAULT_STATE.pageSize,
    sortBy,
    sortDir: oneOf(params.get('sortDir'), ['asc', 'desc'] as const) ?? defaultDirection(sortBy),
  }
}

/** Only what differs from the default goes into the URL, so a plain list keeps a plain address. */
export function writeListState(state: ListState): URLSearchParams {
  const params = new URLSearchParams()
  const q = state.q.trim()
  if (q) params.set('q', q)
  if (state.status !== DEFAULT_STATE.status) params.set('status', state.status || ALL_STATUSES)
  if (state.sensitivity) params.set('sensitivity', state.sensitivity)
  if (state.when) params.set('when', state.when)
  if (state.sortBy !== DEFAULT_STATE.sortBy || state.sortDir !== DEFAULT_STATE.sortDir) {
    params.set('sortBy', state.sortBy)
    params.set('sortDir', state.sortDir)
  }
  if (state.pageSize !== DEFAULT_STATE.pageSize) params.set('pageSize', String(state.pageSize))
  if (state.page > 1) params.set('page', String(state.page))
  return params
}

/** Whether anything narrows the list — decides between "no meetings yet" and "nothing matches". */
export function hasFilters(state: ListState) {
  return Boolean(state.q.trim() || state.status || state.sensitivity || state.when)
}

/** "Bỏ lọc": every meeting, keeping how the user sorts and pages. */
export function clearedState(state: ListState): ListState {
  return { ...DEFAULT_STATE, status: '', pageSize: state.pageSize, sortBy: state.sortBy, sortDir: state.sortDir }
}

/**
 * The request for the current state. "Hôm nay" and "Tuần này" are whole local days (Monday to
 * Sunday), sent as a range; before or after now is the server's `timing`, against its own clock.
 */
export function toQuery(state: ListState, now = new Date()): MeetingListQuery {
  const range =
    state.when === 'today'
      ? { from: now, to: now }
      : state.when === 'week'
        ? { from: startOfWeek(now), to: addDays(startOfWeek(now), 6) }
        : null
  const timing: MeetingTiming | undefined = state.when === 'upcoming' || state.when === 'past' ? state.when : undefined
  return {
    q: state.q.trim() || undefined,
    status: state.status || undefined,
    sensitivity: state.sensitivity || undefined,
    from: range ? startOfDay(range.from).toISOString() : undefined,
    to: range ? endOfDay(range.to).toISOString() : undefined,
    timing,
    page: state.page,
    pageSize: state.pageSize,
    sortBy: state.sortBy,
    sortDir: state.sortDir,
  }
}

function startOfDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate())
}
