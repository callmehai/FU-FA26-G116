import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { listMeetings } from '../../api/meetings'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { Pagination } from '../../components/Pagination'
import { Select } from '../../components/Select'
import { backState } from '../../lib/backTarget'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatClock, formatDate, formatWeekdayDate } from '../../lib/format'
import { routes } from '../../lib/routes'
import {
  MEETING_STATUSES,
  MEETING_STATUS_LABEL,
  SENSITIVITIES,
  SENSITIVITY_LABEL,
  type MeetingListResponse,
  type MeetingSortBy,
} from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'
import { useAuth } from '../auth/useAuth'
import {
  DEFAULT_STATE,
  PAGE_SIZES,
  dateRangeError,
  defaultDirection,
  hasFilters,
  readListState,
  toQuery,
  writeListState,
  type ListState,
} from './listParams'
import { placeLabel } from './place'
import { SensitivityBadge, StatusBadge } from './StatusBadge'
import { StatusSummary } from './StatusSummary'

// An empty value means "no filter", which is why these lists lead with an all-pass entry.
const STATUS_OPTIONS = [
  { value: '' as const, label: 'Mọi trạng thái' },
  ...MEETING_STATUSES.map((s) => ({ value: s, label: MEETING_STATUS_LABEL[s] })),
]

const SENSITIVITY_OPTIONS = [
  { value: '' as const, label: 'Mọi mức độ' },
  ...SENSITIVITIES.map((s) => ({ value: s, label: SENSITIVITY_LABEL[s] })),
]

/** Long enough that typing a word sends one request, short enough to feel live. */
const SEARCH_DELAY_MS = 300
const COLUMN_COUNT = 7
const SKELETON_ROWS = 5

type Load =
  | { kind: 'loading' }
  | { kind: 'loaded'; data: MeetingListResponse }
  | { kind: 'error'; message: string; invalidQuery: boolean }

/**
 * UC-17 View Meeting List (SCRUM-18). Search, filters, sorting and paging are all done by the server
 * after it has dropped every meeting the caller may not see, so nothing here filters rows itself.
 * The criteria live in the URL (see listParams.ts).
 */
export function MeetingsPage() {
  const { can } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const handleAccessLost = useAccessLost()
  const [searchParams, setSearchParams] = useSearchParams()
  // The detail page's back link returns to this list with its filters, sort and page.
  const back = backState('Cuộc họp', location)
  const state = readListState(searchParams)
  const queryKey = writeListState(state).toString()
  const rangeError = dateRangeError(state)

  const [load, setLoad] = useState<Load>({ kind: 'loading' })
  /** The last good page, kept on screen while the next one loads so the table does not flash empty. */
  const [shown, setShown] = useState<MeetingListResponse | null>(null)
  const [refreshing, setRefreshing] = useState(false)
  const [attempt, setAttempt] = useState(0)

  // Filter changes replace the history entry: typing a search should not leave twenty Back steps.
  const update = useCallback(
    (patch: Partial<ListState>) => {
      // Any change of criteria starts again from page 1, unless the change is the page itself.
      const next = { ...readListState(searchParams), page: 1, ...patch }
      setSearchParams(writeListState(next), { replace: true })
    },
    [searchParams, setSearchParams],
  )

  const [search, setSearch] = useSearchBox(state.q, (q) => update({ q }))

  useEffect(() => {
    if (rangeError) return
    let cancelled = false
    // Deferred past the effect's synchronous phase, as the other pages do.
    void Promise.resolve().then(async () => {
      if (cancelled) return
      setRefreshing(true)
      try {
        const data = await listMeetings(toQuery(readListState(new URLSearchParams(queryKey))))
        if (cancelled) return
        // Past the last page (rows removed, a stale link): go to the real last page instead.
        if (data.items.length === 0 && data.totalPages > 0 && data.page > data.totalPages) {
          const params = new URLSearchParams(queryKey)
          setSearchParams(writeListState({ ...readListState(params), page: data.totalPages }), { replace: true })
          return
        }
        setShown(data)
        setLoad({ kind: 'loaded', data })
      } catch (err) {
        if (cancelled || handleAccessLost(err)) return
        setLoad({
          kind: 'error',
          message: errorMessage(err),
          invalidQuery: isApiError(err, 400, 'MSG32'),
        })
      } finally {
        if (!cancelled) setRefreshing(false)
      }
    })
    // StrictMode runs effects twice in dev, and criteria change faster than responses arrive: only
    // the newest request may write state.
    return () => {
      cancelled = true
    }
  }, [queryKey, rangeError, attempt, handleAccessLost, setSearchParams])

  function sortBy(column: MeetingSortBy) {
    const sortDir = state.sortBy === column ? (state.sortDir === 'asc' ? 'desc' : 'asc') : defaultDirection(column)
    update({ sortBy: column, sortDir })
  }

  function clearFilters() {
    setSearch('')
    setSearchParams(
      writeListState({ ...DEFAULT_STATE, pageSize: state.pageSize, sortBy: state.sortBy, sortDir: state.sortDir }),
      { replace: true },
    )
  }

  const filtered = hasFilters(state)
  const data = shown
  const canCreate = can('MeetingCreator')

  let body: ReactNode
  if (rangeError) {
    body = (
      <EmptyRow>
        Không có cuộc họp nào trong khoảng thời gian này — ngày kết thúc đang đứng trước ngày bắt đầu.
      </EmptyRow>
    )
  } else if (load.kind === 'error') {
    body = (
      <EmptyRow>
        <p className="cell-error">{load.invalidQuery ? 'Bộ lọc trên đường dẫn không hợp lệ.' : load.message}</p>
        {load.invalidQuery ? (
          <button type="button" className="btn btn-outline btn-sm" onClick={clearFilters}>
            Đặt lại bộ lọc
          </button>
        ) : (
          <button type="button" className="btn btn-outline btn-sm" onClick={() => setAttempt((n) => n + 1)}>
            Thử lại
          </button>
        )}
      </EmptyRow>
    )
  } else if (!data) {
    body = Array.from({ length: SKELETON_ROWS }, (_, i) => (
      <tr key={i} className="row-skeleton" aria-hidden="true">
        {Array.from({ length: COLUMN_COUNT }, (_, j) => (
          <td key={j}>
            <span className="skeleton" />
          </td>
        ))}
      </tr>
    ))
  } else if (data.items.length === 0) {
    body = filtered ? (
      <EmptyRow>
        <p className="empty-title">Không có cuộc họp nào khớp bộ lọc.</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={clearFilters}>
          Xoá bộ lọc
        </button>
      </EmptyRow>
    ) : (
      <EmptyRow>
        <p className="empty-title">Chưa có cuộc họp nào.</p>
        {canCreate ? (
          <Link className="btn btn-primary btn-sm" to={routes.meetingNew}>
            <Icon name="plus" size={16} />
            Tạo cuộc họp
          </Link>
        ) : (
          <p className="field-hint">Cuộc họp bạn được mời sẽ hiện ở đây sau khi được công bố.</p>
        )}
      </EmptyRow>
    )
  } else {
    const offset = (data.page - 1) * data.pageSize
    body = data.items.map((m, index) => (
      // The whole row opens the meeting; the title link and the eye button are the keyboard routes.
      <tr
        key={m.id}
        className={m.status === 'InProgress' ? 'row-link row-live' : 'row-link'}
        onClick={() => navigate(routes.meeting(m.id), { state: back })}
      >
        <td className="cell-index">{offset + index + 1}</td>
        <td className="cell-title">
          <Link to={routes.meeting(m.id)} state={back} onClick={(e) => e.stopPropagation()}>
            {m.title}
          </Link>
        </td>
        <td className="cell-schedule">
          {formatClock(m.plannedStartTime)}–{formatClock(m.plannedEndTime)}
          <span className="cell-sub">
            {formatDate(m.plannedStartTime) === formatDate(m.plannedEndTime)
              ? formatWeekdayDate(m.plannedStartTime)
              : `${formatDate(m.plannedStartTime)} – ${formatDate(m.plannedEndTime)}`}
          </span>
        </td>
        <td className="cell-muted cell-place">{placeLabel(m)}</td>
        <td>
          <StatusBadge status={m.status} />
        </td>
        <td>
          <SensitivityBadge sensitivity={m.sensitivity} />
        </td>
        <td className="col-action">
          <Link
            className="icon-button icon-button-sm icon-button-brand"
            to={routes.meeting(m.id)}
            state={back}
            onClick={(e) => e.stopPropagation()}
            title="Xem chi tiết"
            aria-label={`Xem chi tiết ${m.title}`}
          >
            <Icon name="eye" />
          </Link>
        </td>
      </tr>
    ))
  }

  return (
    <>
      <PageHeader
        title="Danh sách cuộc họp"
        description={
          data && !filtered
            ? `Các cuộc họp bạn có quyền xem · ${data.totalCount.toLocaleString('vi-VN')} cuộc họp`
            : 'Các cuộc họp bạn có quyền xem.'
        }
        action={
          canCreate && (
            <Link className="btn btn-primary btn-lg" to={routes.meetingNew}>
              <Icon name="plus" size={18} />
              Tạo cuộc họp
            </Link>
          )
        }
      />

      <StatusSummary
        active={state.status}
        showDrafts={canCreate}
        reloadKey={attempt}
        onSelect={(status) => update({ status })}
      />

      <div className="list-panel list-fill">
        <div className="toolbar">
          <label className="search search-flex">
            <Icon name="search" size={16} />
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Tìm theo tên cuộc họp…"
              aria-label="Tìm theo tên cuộc họp"
            />
          </label>

          <Select
            value={state.status}
            options={STATUS_OPTIONS}
            onChange={(status) => update({ status })}
            label="Trạng thái"
          />
          <Select
            value={state.sensitivity}
            options={SENSITIVITY_OPTIONS}
            onChange={(sensitivity) => update({ sensitivity })}
            label="Mức độ"
          />

          <fieldset className={rangeError ? 'date-range date-range-invalid' : 'date-range'}>
            <legend className="visually-hidden">Thời gian diễn ra</legend>
            <span className="date-range-label" aria-hidden="true">
              Thời điểm
            </span>
            <input
              type="date"
              value={state.from}
              max={state.to || undefined}
              onChange={(e) => update({ from: e.target.value })}
              aria-label="Diễn ra từ ngày"
              title="Diễn ra từ ngày"
            />
            <span aria-hidden="true">–</span>
            <input
              type="date"
              value={state.to}
              min={state.from || undefined}
              onChange={(e) => update({ to: e.target.value })}
              aria-label="Diễn ra đến ngày"
              title="Diễn ra đến ngày"
              aria-invalid={!!rangeError}
            />
          </fieldset>

          {/* Icon-only so the toolbar stays on one line; the empty state carries the labelled button. */}
          {filtered && (
            <button
              type="button"
              className="icon-button"
              onClick={clearFilters}
              title="Xoá bộ lọc"
              aria-label="Xoá bộ lọc"
            >
              <Icon name="x" size={16} />
            </button>
          )}
        </div>

        <div className="table-card table-fill" aria-busy={refreshing}>
          <table className={refreshing && data ? 'data-table data-table-stale' : 'data-table'}>
            <thead>
              <tr>
                <th className="cell-index">STT</th>
                <SortHeader column="title" state={state} onSort={sortBy}>
                  Tên cuộc họp
                </SortHeader>
                <SortHeader column="plannedStartTime" state={state} onSort={sortBy}>
                  Thời gian
                </SortHeader>
                <th className="cell-place">Hình thức · Địa điểm</th>
                <th>Trạng thái</th>
                <th>Mức độ</th>
                <th className="col-action">Thao tác</th>
              </tr>
            </thead>
            <tbody>{body}</tbody>
          </table>
        </div>

        {data && data.totalCount > 0 && !rangeError && load.kind !== 'error' && (
          <Pagination
            page={data.page}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            totalPages={data.totalPages}
            pageSizes={PAGE_SIZES}
            itemLabel="cuộc họp"
            onPageChange={(page) => update({ page })}
            onPageSizeChange={(pageSize) => update({ pageSize })}
            disabled={refreshing}
          />
        )}
      </div>
    </>
  )
}

function EmptyRow({ children }: { children: ReactNode }) {
  return (
    <tr>
      <td colSpan={COLUMN_COUNT} className="cell-empty">
        {children}
      </td>
    </tr>
  )
}

/**
 * A sortable column header. The button carries the action; `aria-sort` on the header tells a screen
 * reader which way the column is currently sorted.
 */
function SortHeader({
  column,
  state,
  onSort,
  children,
}: {
  column: MeetingSortBy
  state: ListState
  onSort: (column: MeetingSortBy) => void
  children: ReactNode
}) {
  const active = state.sortBy === column
  const direction = active ? state.sortDir : null
  return (
    <th aria-sort={direction === 'asc' ? 'ascending' : direction === 'desc' ? 'descending' : 'none'}>
      <button
        type="button"
        className={active ? 'sort-button sort-button-active' : 'sort-button'}
        onClick={() => onSort(column)}
      >
        {children}
        <Icon name={direction === 'asc' ? 'sortAsc' : direction === 'desc' ? 'sortDesc' : 'sortBoth'} size={14} />
      </button>
    </th>
  )
}

/**
 * The search box keeps what is typed locally and hands it on after a pause. When the URL changes
 * from elsewhere — Back, "Xoá bộ lọc" — the box follows it.
 */
function useSearchBox(committed: string, commit: (value: string) => void) {
  const [value, setValue] = useState(committed)
  const commitRef = useRef(commit)
  const lastCommitted = useRef(committed)

  useEffect(() => {
    commitRef.current = commit
  })

  // Follow the URL when it changes without the box being the cause.
  useEffect(() => {
    if (committed !== lastCommitted.current) {
      lastCommitted.current = committed
      setValue(committed)
    }
  }, [committed])

  useEffect(() => {
    if (value.trim() === lastCommitted.current.trim()) return
    const timer = setTimeout(() => {
      lastCommitted.current = value.trim()
      commitRef.current(value)
    }, SEARCH_DELAY_MS)
    return () => clearTimeout(timer)
  }, [value])

  return [value, setValue] as const
}
