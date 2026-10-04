import { endOfDateInput, startOfDateInput } from '../../lib/dateInput'
import {
  MEETING_STATUSES,
  SENSITIVITIES,
  type MeetingListQuery,
  type MeetingSortBy,
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
 */
export interface ListState {
  q: string
  status: MeetingStatus | ''
  sensitivity: Sensitivity | ''
  /** `YYYY-MM-DD` in the viewer's timezone, as a date input holds it. */
  from: string
  to: string
  page: number
  pageSize: number
  sortBy: MeetingSortBy
  sortDir: SortDirection
}

export const PAGE_SIZES = [10, 20, 50] as const
// No `startsAt`: the schedule moved to meeting sessions (SCRUM-70). An old link sorting by it falls
// back to the default rather than earning a 400.
const SORT_COLUMNS: readonly MeetingSortBy[] = ['title', 'createdAt']
const DATE = /^\d{4}-\d{2}-\d{2}$/

/** A name sorts A→Z first; a time sorts newest first. Same defaults as the backend. */
export function defaultDirection(column: MeetingSortBy): SortDirection {
  return column === 'title' ? 'asc' : 'desc'
}

export const DEFAULT_STATE: ListState = {
  q: '',
  status: '',
  sensitivity: '',
  from: '',
  to: '',
  page: 1,
  pageSize: 20,
  sortBy: 'createdAt',
  sortDir: 'desc',
}

function oneOf<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((candidate) => candidate === value)
}

export function readListState(params: URLSearchParams): ListState {
  const sortBy = oneOf(params.get('sortBy'), SORT_COLUMNS) ?? DEFAULT_STATE.sortBy
  const page = Number(params.get('page'))
  const pageSize = Number(params.get('pageSize'))
  const from = params.get('from') ?? ''
  const to = params.get('to') ?? ''

  return {
    q: params.get('q') ?? '',
    status: oneOf(params.get('status'), MEETING_STATUSES) ?? '',
    sensitivity: oneOf(params.get('sensitivity'), SENSITIVITIES) ?? '',
    from: DATE.test(from) ? from : '',
    to: DATE.test(to) ? to : '',
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
  if (state.status) params.set('status', state.status)
  if (state.sensitivity) params.set('sensitivity', state.sensitivity)
  if (state.from) params.set('from', state.from)
  if (state.to) params.set('to', state.to)
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
  return Boolean(state.q.trim() || state.status || state.sensitivity || state.from || state.to)
}

/** A "to" before the "from" can only return nothing; the page says so instead of asking the server. */
export function dateRangeError(state: ListState): string | null {
  return state.from && state.to && state.from > state.to ? 'Ngày kết thúc phải từ ngày bắt đầu trở đi.' : null
}

/**
 * The request for the current state. The date range covers whole local days: "from" starts at
 * 00:00 of its day and "to" runs to the end of its day, so picking the same day twice finds that day.
 */
export function toQuery(state: ListState): MeetingListQuery {
  return {
    q: state.q.trim() || undefined,
    status: state.status || undefined,
    sensitivity: state.sensitivity || undefined,
    from: startOfDateInput(state.from) ?? undefined,
    to: endOfDateInput(state.to) ?? undefined,
    page: state.page,
    pageSize: state.pageSize,
    sortBy: state.sortBy,
    sortDir: state.sortDir,
  }
}
