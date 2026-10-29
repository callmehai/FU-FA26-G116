import { useCallback, useEffect, useState, type ReactNode } from 'react'
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
import { useSearchBox } from '../../lib/useSearchBox'
import {
  MEETING_MODE_LABEL,
  MEETING_STATUSES,
  MEETING_STATUS_LABEL,
  SENSITIVITIES,
  SENSITIVITY_LABEL,
  type MeetingListResponse,
  type MeetingStatus,
  type MeetingSortBy,
} from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'
import { useAuth } from '../auth/useAuth'
import {
  PAGE_SIZES,
  WHEN_LABEL,
  WHEN_OPTIONS,
  clearedState,
  defaultDirection,
  hasFilters,
  readListState,
  toQuery,
  writeListState,
  type ListState,
} from './listParams'
import { SensitivityBadge, StatusBadge } from './StatusBadge'
import { StatusSummary } from './StatusSummary'
import { useCreateMeetingLink } from './meetingDialogLink'

// An empty value means "no filter", which is why these lists lead with an all-pass entry.
const STATUS_OPTIONS = [
  { value: '' as const, label: 'Mọi trạng thái' },
  ...MEETING_STATUSES.map((s) => ({ value: s, label: MEETING_STATUS_LABEL[s] })),
]

const SENSITIVITY_OPTIONS = [
  { value: '' as const, label: 'Mọi mức độ' },
  ...SENSITIVITIES.map((s) => ({ value: s, label: SENSITIVITY_LABEL[s] })),
]

const WHEN_SELECT_OPTIONS = [
  { value: '' as const, label: 'Mọi thời điểm' },
  ...WHEN_OPTIONS.map((w) => ({ value: w, label: WHEN_LABEL[w] })),
]

const COLUMN_COUNT = 7
/** A row's tone by state: live stands out, called off and over fade back (global.css: .row-*). */
const ROW_TONE: Partial<Record<MeetingStatus, string>> = {
  InProgress: 'row-live',
  Cancelled: 'row-cancelled',
  Ended: 'row-ended',
}
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
  }, [queryKey, attempt, handleAccessLost, setSearchParams])

  function sortBy(column: MeetingSortBy) {
    const sortDir = state.sortBy === column ? (state.sortDir === 'asc' ? 'desc' : 'asc') : defaultDirection(column)
    update({ sortBy: column, sortDir })
  }

  function clearFilters() {
    setSearch('')
    setSearchParams(writeListState(clearedState(state)), { replace: true })
  }

  const filtered = hasFilters(state)
  const activeFilters = [state.status, state.sensitivity, state.when].filter(Boolean).length
  const [filtersOpen, setFiltersOpen] = useState(false)
  // The list as it opens: only meetings under way, which most of the time is none.
  const onlyLive = state.status === 'InProgress' && !state.q.trim() && !state.sensitivity && !state.when
  const data = shown
  const canCreate = can('MeetingCreator')
  const createLink = useCreateMeetingLink()

  let body: ReactNode
  if (load.kind === 'error') {
    body = (
      <EmptyRow>
        <p className="cell-error">{load.invalidQuery ? 'Bộ lọc trên đường dẫn không hợp lệ.' : load.message}</p>
        {load.invalidQuery ? (
          <button type="button" className="btn btn-outline btn-sm" onClick={clearFilters}>
            <Icon name="x" size={16} />
            Đặt lại bộ lọc
          </button>
        ) : (
          <button type="button" className="btn btn-outline btn-sm" onClick={() => setAttempt((n) => n + 1)}>
            <Icon name="refresh" size={16} />
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
    body = onlyLive ? (
      <EmptyRow>
        <p className="empty-title">Hiện không có cuộc họp nào đang diễn ra.</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={clearFilters}>
          <Icon name="list" size={16} />
          Xem mọi cuộc họp
        </button>
      </EmptyRow>
    ) : filtered ? (
      <EmptyRow>
        <p className="empty-title">Không có cuộc họp nào khớp bộ lọc.</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={clearFilters}>
          <Icon name="x" size={16} />
          Xoá bộ lọc
        </button>
      </EmptyRow>
    ) : (
      <EmptyRow>
        <p className="empty-title">Chưa có cuộc họp nào.</p>
        {canCreate ? (
          <Link className="btn btn-primary btn-sm btn-cta" to={createLink.to} state={createLink.state}>
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
      // The row itself says where the meeting stands at a glance: live stands out, called off and
      // over fade back.
      <tr
        key={m.id}
        className={`row-link${ROW_TONE[m.status] ? ` ${ROW_TONE[m.status]}` : ''}`}
        onClick={() => navigate(routes.meeting(m.id), { state: back })}
      >
        <td className="cell-index">{offset + index + 1}</td>
        <td className="cell-title">
          <Link to={routes.meeting(m.id)} state={back} onClick={(e) => e.stopPropagation()}>
            {m.title}
          </Link>
          {m.mode !== 'Online' && m.location && (
            <span className="cell-sub cell-meta">
              <Icon name="mapPin" size={13} />
              {m.location}
            </span>
          )}
        </td>
        <td className="cell-schedule">
          <b>
            {formatClock(m.plannedStartTime)} – {formatClock(m.plannedEndTime)}
          </b>
          <span className="cell-sub">
            {formatDate(m.plannedStartTime) === formatDate(m.plannedEndTime)
              ? formatWeekdayDate(m.plannedStartTime)
              : `${formatDate(m.plannedStartTime)} – ${formatDate(m.plannedEndTime)}`}
          </span>
        </td>
        <td className="cell-muted cell-place">{MEETING_MODE_LABEL[m.mode]}</td>
        <td className="cell-status">
          <StatusBadge status={m.status} />
        </td>
        <td className="cell-level">
          <SensitivityBadge sensitivity={m.sensitivity} />
        </td>
        <td className="col-action">
          {m.status === 'InProgress' && m.mode !== 'InPerson' ? (
            // The call to act now: green like the "Đang diễn ra" state, and it pings. The room
            // answers 404 to someone who is not invited.
            <Link className="btn btn-live btn-sm btn-ping" to={routes.meetingRoom(m.id)} onClick={(e) => e.stopPropagation()}>
              <Icon name="video" size={16} />
              Vào phòng họp
            </Link>
          ) : (
            <Link
              className="icon-button icon-button-sm icon-button-brand"
              to={routes.meeting(m.id)}
              state={back}
              onClick={(e) => e.stopPropagation()}
              data-tooltip="Xem chi tiết"
              aria-label={`Xem chi tiết ${m.title}`}
            >
              <Icon name="eye" />
            </Link>
          )}
        </td>
      </tr>
    ))
  }

  return (
    <>
      <PageHeader
        title="Danh sách cuộc họp"
        action={
          canCreate && (
            <Link className="btn btn-primary btn-lg btn-cta" to={createLink.to} state={createLink.state}>
              <Icon name="plus" size={16} />
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

      <div className="list-panel list-page list-fill">
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

          {/* On a phone the filters fold behind one button with their count, as on AI-eCabinet. */}
          <button
            type="button"
            className="btn btn-outline filters-toggle"
            aria-expanded={filtersOpen}
            aria-controls="meeting-filters"
            onClick={() => setFiltersOpen((o) => !o)}
          >
            <Icon name="filter" size={16} />
            Bộ lọc
            {activeFilters > 0 && <span className="filters-count">{activeFilters}</span>}
          </button>
          <div id="meeting-filters" className={filtersOpen ? 'toolbar-filters toolbar-filters-open' : 'toolbar-filters'}>
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

            <Select
              value={state.when}
              options={WHEN_SELECT_OPTIONS}
              onChange={(when) => update({ when })}
              label="Thời điểm"
            />

            {/* Icon-only so the toolbar stays on one line; the empty state carries the labelled button. */}
            {filtered && (
              <button
                type="button"
                className="icon-button"
                onClick={clearFilters}
                data-tooltip="Xoá bộ lọc"
                aria-label="Xoá bộ lọc"
              >
                <Icon name="x" size={16} />
              </button>
            )}
          </div>

        </div>

        <div className="table-card table-fill" aria-busy={refreshing}>
          <table className={refreshing && data ? 'data-table meetings-table data-table-stale' : 'data-table meetings-table'}>
            <thead>
              <tr>
                <th className="cell-index">STT</th>
                <SortHeader column="title" state={state} onSort={sortBy}>
                  Tên cuộc họp
                </SortHeader>
                <SortHeader column="plannedStartTime" state={state} onSort={sortBy}>
                  Thời gian diễn ra
                </SortHeader>
                <th className="cell-place">Hình thức</th>
                <th>Trạng thái</th>
                <th>Mức độ</th>
                <th className="col-action">Thao tác</th>
              </tr>
            </thead>
            <tbody>{body}</tbody>
          </table>
        </div>

        {data && data.totalCount > 0 && load.kind !== 'error' && (
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
