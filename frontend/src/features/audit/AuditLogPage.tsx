import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { listAuditEventTypes, listAuditLog } from '../../api/auditLog'
import { listUsers } from '../../api/users'
import { Badge } from '../../components/Badge'
import { DateField } from '../../components/DateField'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { Pagination } from '../../components/Pagination'
import { Select, type SelectOption } from '../../components/Select'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSearchBox } from '../../lib/useSearchBox'
import type { AuditEventType, AuditLogItem, AuditLogPage } from '../../types/auditLog'
import { useAccessLost } from '../auth/useAccessLost'
import { ActorCell, AuditEntrySheet } from './AuditEntrySheet'
import { objectTypeLabel } from './objectTypes'

const PAGE_SIZES = [20, 50, 100] as const
const DEFAULT_PAGE_SIZE = 20
const COLUMN_COUNT = 6
const SKELETON_ROWS = 6
const DAY = /^\d{4}-\d{2}-\d{2}$/
const ACTOR = /^(guest|system|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i

/** Events that mark a security concern: a red chip and a tinted row (screen 3.3). */
const ALERT_ACTIONS = new Set(['auth.sign_in_failed'])

interface Criteria {
  /** Whole local days, `YYYY-MM-DD`. */
  from: string
  to: string
  actor: string
  action: string
  q: string
  page: number
  pageSize: number
}

/** The URL read defensively: a malformed value is dropped rather than sent, so an old link opens unfiltered. */
function readCriteria(params: URLSearchParams): Criteria {
  const day = (key: string) => (DAY.test(params.get(key) ?? '') ? params.get(key)! : '')
  const page = Number(params.get('page'))
  const pageSize = Number(params.get('pageSize'))
  return {
    from: day('from'),
    to: day('to'),
    actor: ACTOR.test(params.get('actor') ?? '') ? params.get('actor')!.toLowerCase() : '',
    action: params.get('action') ?? '',
    q: (params.get('q') ?? '').slice(0, 100),
    page: Number.isInteger(page) && page >= 1 ? page : 1,
    pageSize: (PAGE_SIZES as readonly number[]).includes(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
  }
}

function writeCriteria(c: Criteria) {
  const params = new URLSearchParams()
  for (const key of ['from', 'to', 'actor', 'action'] as const) if (c[key]) params.set(key, c[key])
  if (c.q.trim()) params.set('q', c.q.trim())
  if (c.page > 1) params.set('page', String(c.page))
  if (c.pageSize !== DEFAULT_PAGE_SIZE) params.set('pageSize', String(c.pageSize))
  return params
}

/** `YYYY-MM-DD` as the start of that local day, or its last millisecond, in ISO. */
function dayBound(day: string, end: boolean) {
  const [y, m, d] = day.split('-').map(Number)
  return (end ? new Date(y, m - 1, d, 23, 59, 59, 999) : new Date(y, m - 1, d)).toISOString()
}

const rangeInvalid = (from: string, to: string) => Boolean(from && to && from > to)

type Load = { kind: 'loading' } | { kind: 'loaded' } | { kind: 'error'; message: string; invalidQuery: boolean }

/**
 * UC-13 View Audit Log (SCRUM-16), screen 3.3. Read-only: the server filters, pages and names each actor;
 * nothing here filters rows itself. The criteria live in the URL. No CSV export (Q3), and the object
 * column names only accounts and roles (BR-443).
 */
export function AuditLogPage() {
  const handleAccessLost = useAccessLost()
  const [searchParams, setSearchParams] = useSearchParams()
  const criteria = readCriteria(searchParams)
  const queryKey = writeCriteria(criteria).toString()

  const [load, setLoad] = useState<Load>({ kind: 'loading' })
  /** The last good page, kept on screen while the next one loads. */
  const [shown, setShown] = useState<AuditLogPage | null>(null)
  const [refreshing, setRefreshing] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [eventTypes, setEventTypes] = useState<AuditEventType[]>([])
  const [actors, setActors] = useState<SelectOption<string>[]>([])
  const [opened, setOpened] = useState<AuditLogItem | null>(null)
  const [filtersOpen, setFiltersOpen] = useState(false)

  const update = useCallback(
    (patch: Partial<Criteria>) =>
      setSearchParams(writeCriteria({ ...readCriteria(searchParams), page: 1, ...patch }), { replace: true }),
    [searchParams, setSearchParams],
  )

  const [search, setSearch] = useSearchBox(criteria.q, (q) => update({ q }))

  // The period is held here until it is valid: a start after the end is reported and not applied (screen 3.3).
  const [range, setRange] = useState({ from: criteria.from, to: criteria.to })
  const [urlRange, setUrlRange] = useState(range)
  if (urlRange.from !== criteria.from || urlRange.to !== criteria.to) {
    // The URL moved on its own (Back, "Xoá bộ lọc"): follow it.
    setUrlRange({ from: criteria.from, to: criteria.to })
    setRange({ from: criteria.from, to: criteria.to })
  }
  function changeRange(next: { from: string; to: string }) {
    setRange(next)
    if (!rangeInvalid(next.from, next.to)) update(next)
  }

  // The filter choices. Neither blocks the list: without them the dropdowns offer less.
  useEffect(() => {
    void Promise.resolve().then(async () => {
      try {
        setEventTypes(await listAuditEventTypes())
      } catch (err) {
        handleAccessLost(err)
      }
    })
    void Promise.resolve().then(async () => {
      try {
        const users = await listUsers()
        // Two people with one name are told apart by their email.
        const shared = new Set(
          users.filter((u, i) => users.findIndex((o) => o.fullName === u.fullName) !== i).map((u) => u.fullName),
        )
        setActors(
          users.map((u) => ({
            value: u.id,
            label: u.fullName,
            hint:
              [shared.has(u.fullName) && u.email, !u.isActive && 'Đã vô hiệu hoá'].filter(Boolean).join(' · ') ||
              undefined,
          })),
        )
      } catch (err) {
        handleAccessLost(err)
      }
    })
  }, [handleAccessLost])

  useEffect(() => {
    let cancelled = false
    void Promise.resolve().then(async () => {
      if (cancelled) return
      const c = readCriteria(new URLSearchParams(queryKey))
      setRefreshing(true)
      try {
        const data = await listAuditLog({
          from: c.from ? dayBound(c.from, false) : undefined,
          to: c.to ? dayBound(c.to, true) : undefined,
          actor: c.actor || undefined,
          action: c.action || undefined,
          q: c.q.trim() || undefined,
          page: c.page,
          pageSize: c.pageSize,
        })
        if (cancelled) return
        // Past the last page (a stale link): go to the real last page.
        if (data.items.length === 0 && data.totalPages > 0 && data.page > data.totalPages) {
          setSearchParams(writeCriteria({ ...c, page: data.totalPages }), { replace: true })
          return
        }
        setShown(data)
        setLoad({ kind: 'loaded' })
      } catch (err) {
        if (cancelled || handleAccessLost(err)) return
        setLoad({ kind: 'error', message: errorMessage(err), invalidQuery: isApiError(err, 400, 'MSG469') })
      } finally {
        if (!cancelled) setRefreshing(false)
      }
    })
    return () => {
      cancelled = true
    }
  }, [queryKey, attempt, handleAccessLost, setSearchParams])

  const filtered = Boolean(criteria.from || criteria.to || criteria.actor || criteria.action || criteria.q.trim())
  const activeFilters = [criteria.from || criteria.to, criteria.actor, criteria.action].filter(Boolean).length

  function clearFilters() {
    setSearch('')
    setRange({ from: '', to: '' })
    setSearchParams(writeCriteria({ ...readCriteria(new URLSearchParams()), pageSize: criteria.pageSize }), {
      replace: true,
    })
  }

  const actorOptions: SelectOption<string>[] = [
    { value: '', label: 'Mọi người thực hiện' },
    { value: 'guest', label: 'Khách' },
    { value: 'system', label: 'Hệ thống' },
    ...actors,
  ]
  const eventOptions: SelectOption<string>[] = [
    { value: '', label: 'Mọi loại sự kiện' },
    ...eventTypes.map((t) => ({ value: t.code, label: t.label })),
  ]

  const data = shown
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
    // AF-3 / BR-12: no code from the server, the sentence is the screen's.
    body = filtered ? (
      <EmptyRow>
        <p className="empty-title">Không có sự kiện nào khớp bộ lọc.</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={clearFilters}>
          <Icon name="x" size={16} />
          Xoá bộ lọc
        </button>
      </EmptyRow>
    ) : (
      <EmptyRow>
        <p className="empty-title">Chưa có sự kiện nào được ghi.</p>
      </EmptyRow>
    )
  } else {
    body = data.items.map((item) => {
      const alert = ALERT_ACTIONS.has(item.action)
      return (
        <tr key={item.id} className={alert ? 'row-link row-alert' : 'row-link'} onClick={() => setOpened(item)}>
          <td className="audit-time">{formatDateTime(item.occurredAt)}</td>
          <td className="cell-title audit-actor">
            <ActorCell actor={item.actor} />
          </td>
          <td className="audit-event">
            <Badge tone={alert ? 'red' : 'slate'}>{item.actionLabel}</Badge>
          </td>
          <td className="audit-object">
            {objectTypeLabel(item.objectType)}
            <span className="cell-sub">{item.objectName ?? `#${item.objectId.slice(0, 8)}`}</span>
          </td>
          <td className="cell-muted audit-ip">{item.ipAddress || <span className="audit-none">Không có</span>}</td>
          <td className="col-action">
            <button
              type="button"
              className="icon-button icon-button-sm icon-button-brand"
              onClick={(e) => {
                e.stopPropagation()
                setOpened(item)
              }}
              data-tooltip="Xem chi tiết"
              aria-label={`Xem chi tiết: ${item.actionLabel}, ${formatDateTime(item.occurredAt)}`}
            >
              <Icon name="eye" />
            </button>
          </td>
        </tr>
      )
    })
  }

  const invalidRange = rangeInvalid(range.from, range.to)

  return (
    <>
      <PageHeader title="Nhật ký hệ thống" description="Sự kiện nghiệp vụ và bảo mật. Không sửa, không xoá được." />

      <div className="list-panel list-page list-fill">
        <div className="toolbar audit-toolbar">
          <label className="search search-flex">
            <Icon name="search" size={16} />
            <input
              type="search"
              value={search}
              maxLength={100}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Tìm theo đối tượng…"
              aria-label="Tìm theo mã đối tượng hoặc nội dung chi tiết"
            />
          </label>

          <button
            type="button"
            className="btn btn-outline filters-toggle"
            aria-expanded={filtersOpen}
            aria-controls="audit-filters"
            onClick={() => setFiltersOpen((o) => !o)}
          >
            <Icon name="filter" size={16} />
            Bộ lọc
            {activeFilters > 0 && <span className="filters-count">{activeFilters}</span>}
          </button>
          <div id="audit-filters" className={filtersOpen ? 'toolbar-filters toolbar-filters-open' : 'toolbar-filters'}>
            <div className="audit-range" role="group" aria-label="Khoảng thời gian">
              <label className="audit-day">
                <span className="visually-hidden">Từ ngày</span>
                <DateField
                  value={range.from}
                  onChange={(from) => changeRange({ ...range, from })}
                  invalid={invalidRange}
                />
              </label>
              <span aria-hidden="true">–</span>
              <label className="audit-day">
                <span className="visually-hidden">Đến ngày</span>
                <DateField value={range.to} onChange={(to) => changeRange({ ...range, to })} invalid={invalidRange} />
              </label>
            </div>
            <Select
              value={criteria.actor}
              options={actorOptions}
              onChange={(actor) => update({ actor })}
              label="Người thực hiện"
            />
            <Select
              value={criteria.action}
              options={eventOptions}
              onChange={(action) => update({ action })}
              label="Loại sự kiện"
            />
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
          {invalidRange && (
            <p className="field-error audit-range-error" role="alert">
              Ngày bắt đầu không được sau ngày kết thúc.
            </p>
          )}
        </div>

        <div className="table-card table-fill" aria-busy={refreshing}>
          <table className={refreshing && data ? 'data-table audit-table data-table-stale' : 'data-table audit-table'}>
            <thead>
              <tr>
                <th>Thời điểm</th>
                <th>Người thực hiện</th>
                <th>Sự kiện</th>
                <th>Đối tượng</th>
                <th>Địa chỉ IP</th>
                <th className="col-action">
                  <span className="visually-hidden">Thao tác</span>
                </th>
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
            itemLabel="sự kiện"
            onPageChange={(page) => update({ page })}
            onPageSizeChange={(pageSize) => update({ pageSize })}
            disabled={refreshing}
          />
        )}
      </div>

      {opened && <AuditEntrySheet item={opened} onClose={() => setOpened(null)} />}
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
