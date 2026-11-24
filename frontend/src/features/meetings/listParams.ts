import { addDays, endOfDay, startOfWeek } from '../calendar/dates'
import {
  MEETING_STATUSES,
  SENSITIVITIES,
  type MeetingListQuery,
  type MeetingListScope,
  type MeetingSortBy,
  type MeetingTiming,
  type MeetingStatus,
  type Sensitivity,
  type SortDirection,
} from '../../types/meeting'

/**
 * The meeting list's criteria live in the URL, so a filtered page survives a reload, can be shared as
 * a link, and Back returns to it (criteria are kept across pages).
 *
 * Whatever is in the URL is read defensively: an unknown value is dropped rather than sent, so a
 * hand-edited or outdated link falls back to the default instead of failing with a 400.
 *
 * The list opens unfiltered, as AI-eCabinet's does: a URL with no `when` means any time. Opening on
 * today (PR #287) was dropped by the PO on 2026-11-20 — a quiet day showed an empty list and read as
 * "no meetings". `when=all` and `status=all` from older links still read as any time / every status.
 */
export interface ListState {
  q: string
  status: MeetingStatus | ''
  sensitivity: Sensitivity | ''
  when: When | ''
  /** `todo` = "Cần xử lý": only the meetings waiting on the caller, whatever the other filters. */
  scope: MeetingListScope | ''
  page: number
  pageSize: number
  sortBy: MeetingSortBy
  sortDir: SortDirection
  /** The user picked the order (a header click), so it is kept in the URL; otherwise it follows the view. */
  sortSet: boolean
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

const SCOPES: readonly MeetingListScope[] = ['todo']

export const PAGE_SIZES = [10, 20, 50] as const
const SORT_COLUMNS: readonly MeetingSortBy[] = ['title', 'plannedStartTime', 'createdAt']

/** A name sorts A→Z first; a time sorts newest first. Same defaults as the backend. */
export function defaultDirection(column: MeetingSortBy): SortDirection {
  return column === 'title' ? 'asc' : 'desc'
}

export const DEFAULT_STATE: ListState = {
  q: '',
  status: '',
  sensitivity: '',
  when: '',
  scope: '',
  page: 1,
  pageSize: 20,
  sortBy: 'plannedStartTime',
  sortDir: 'desc',
  sortSet: false,
}

/**
 * The order a view opens in. Today's meetings and "Cần xử lý" are worked through from the soonest, so their times
 * run forward (09:00 before 15:30, the approval due in two days before one in February); any other list shows the
 * newest first.
 */
function naturalDirection(state: Pick<ListState, 'sortBy' | 'when' | 'scope'>): SortDirection {
  if (state.sortBy === 'plannedStartTime' && (state.when === 'today' || state.scope === 'todo')) return 'asc'
  return defaultDirection(state.sortBy)
}

function oneOf<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((candidate) => candidate === value)
}

export function readListState(params: URLSearchParams): ListState {
  const sortBy = oneOf(params.get('sortBy'), SORT_COLUMNS) ?? DEFAULT_STATE.sortBy
  const page = Number(params.get('page'))
  const pageSize = Number(params.get('pageSize'))
  const status = oneOf(params.get('status'), MEETING_STATUSES) ?? ''
  const scope = oneOf(params.get('scope'), SCOPES) ?? ''
  const picked = oneOf(params.get('sortDir'), ['asc', 'desc'] as const)
  const state: Omit<ListState, 'sortDir'> = {
    q: params.get('q') ?? '',
    status,
    sensitivity: oneOf(params.get('sensitivity'), SENSITIVITIES) ?? '',
    when: oneOf(params.get('when'), WHEN_OPTIONS) ?? '',
    scope,
    page: Number.isInteger(page) && page >= 1 ? page : 1,
    pageSize: (PAGE_SIZES as readonly number[]).includes(pageSize) ? pageSize : DEFAULT_STATE.pageSize,
    sortBy,
    sortSet: picked !== undefined,
  }
  return { ...state, sortDir: picked ?? naturalDirection(state) }
}

/** Only what differs from the default goes into the URL, so a plain list keeps a plain address. */
export function writeListState(state: ListState): URLSearchParams {
  const params = new URLSearchParams()
  const q = state.q.trim()
  if (q) params.set('q', q)
  if (state.status) params.set('status', state.status)
  if (state.sensitivity) params.set('sensitivity', state.sensitivity)
  if (state.when) params.set('when', state.when)
  if (state.scope) params.set('scope', state.scope)
  if (state.sortSet || state.sortBy !== DEFAULT_STATE.sortBy) {
    params.set('sortBy', state.sortBy)
    params.set('sortDir', state.sortDir)
  }
  if (state.pageSize !== DEFAULT_STATE.pageSize) params.set('pageSize', String(state.pageSize))
  if (state.page > 1) params.set('page', String(state.page))
  return params
}

/** Whether anything narrows the list — decides between "no meetings yet" and "nothing matches". */
export function hasFilters(state: ListState) {
  return Boolean(state.q.trim() || state.status || state.sensitivity || state.when || state.scope)
}

/** "Bỏ lọc": every meeting, keeping how the user sorts and pages. */
export function clearedState(state: ListState): ListState {
  return {
    ...DEFAULT_STATE,
    pageSize: state.pageSize,
    sortBy: state.sortBy,
    sortDir: state.sortDir,
    sortSet: state.sortSet,
  }
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
    scope: state.scope || undefined,
    page: state.page,
    pageSize: state.pageSize,
    sortBy: state.sortBy,
    sortDir: state.sortDir,
  }
}

function startOfDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate())
}

/**
 * The four cards above the list, AI-eCabinet's: each one is exactly a set of criteria, so its count is
 * the list's own `totalCount` under them and a click shows the very rows it counted.
 */
export const STAT_CARDS = ['today', 'published', 'todo', 'draft'] as const
export type StatCard = (typeof STAT_CARDS)[number]

type StatCriteria = Pick<ListState, 'status' | 'when' | 'scope'>

const STAT_CRITERIA: Record<StatCard, StatCriteria> = {
  today: { status: '', when: 'today', scope: '' },
  published: { status: 'Published', when: '', scope: '' },
  todo: { status: '', when: '', scope: 'todo' },
  draft: { status: 'Draft', when: '', scope: '' },
}

/**
 * A card's list: its criteria alone — the search and the other filters go, so the list matches the number — in
 * the order that card opens in.
 */
export function statState(card: StatCard, state: ListState): ListState {
  const criteria = { ...clearedState(state), ...STAT_CRITERIA[card], sortBy: DEFAULT_STATE.sortBy, sortSet: false }
  return { ...criteria, sortDir: naturalDirection(criteria) }
}

/** The request behind a card's number: one row, only its total is read. */
export function statQuery(criteria: StatCriteria | StatCard, now = new Date()): MeetingListQuery {
  const picked = typeof criteria === 'string' ? STAT_CRITERIA[criteria] : criteria
  return toQuery({ ...DEFAULT_STATE, ...picked, pageSize: 1 }, now)
}

/**
 * Which card the list is showing. "Cần xử lý" is a scope and stays on under other filters, as on
 * AI-eCabinet; any other card is on only while the list is exactly its criteria.
 */
export function activeStat(state: ListState): StatCard | null {
  if (state.scope === 'todo') return 'todo'
  if (state.q.trim() || state.sensitivity) return null
  return (
    STAT_CARDS.find((card) => {
      const want = STAT_CRITERIA[card]
      return want.scope === '' && want.status === state.status && want.when === state.when
    }) ?? null
  )
}
