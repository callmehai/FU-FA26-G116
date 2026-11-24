import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { exportReportTable, getMeetingReport, getReportTable } from '../../api/reports'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { Select, type SelectOption } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { routes } from '../../lib/routes'
import { useRefetchOnFocus } from '../../lib/useRefetchOnFocus'
import type { MeetingStatus, Sensitivity } from '../../types/meeting'
import type { MeetingReport, ReportTable } from '../../types/report'
import { saveBlob } from '../documents/documentFile'
import { SensitivityBadge, StatusBadge } from '../meetings/StatusBadge'
import {
  MSG463_INVALID_PERIOD,
  MSG466_NOTHING_READABLE,
  defaultPeriod,
  followPeriod,
  formatRate,
  monthOptions,
  parseMonth,
  periodValid,
  vietnamMonth,
} from './reportFormat'
import {
  ENTITIES,
  FILTERS,
  GROUP_LABEL,
  GROUP_SORT,
  TOP_VALUES,
  entityConfig,
  exportFileName,
  filtersOf,
  formatFigure,
  parseQuery,
  queryToApi,
  queryToUrl,
  switchEntity,
  switchGroup,
  type Entity,
  type ReportQuery,
} from './reportQuery'

type Load = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'ready' }

const QUERY_KEYS = ['entity', 'groupBy', 'sort', 'top', ...Object.keys(FILTERS)]

/**
 * SCRUM-38, reworked after the supervisor's review of 22/11/2026: the figures a manager reads first in one band at the
 * top, then a query over the main tables — pick the table, how to group it, what to filter, how many to show — so any
 * follow-up question ("who chaired the most, and second?") has an answer on the screen. Open to every internal user
 * (BR-149, BR-150): the server counts only what the viewer may see. Period and query live in the URL, so a reload or a shared
 * link opens the same answer.
 */
export function ReportsPage() {
  const toast = useToast()
  const [params, setParams] = useSearchParams()
  const now = vietnamMonth()
  const fallback = defaultPeriod(now)
  const from = parseMonth(params.get('from')) ?? fallback.from
  const to = parseMonth(params.get('to')) ?? fallback.to
  const valid = periodValid(from, to)
  const options = useMemo<SelectOption<string>[]>(() => monthOptions(now, [from, to]), [now, from, to])
  const query = parseQuery((name) => params.get(name))
  const apiParams = queryToApi(query, from, to)
  const apiKey = new URLSearchParams(apiParams).toString()

  const [report, setReport] = useState<MeetingReport | null>(null)
  const [table, setTable] = useState<ReportTable | null>(null)
  const [tableLoad, setTableLoad] = useState<Load>({ kind: 'loading' })
  const [reportError, setReportError] = useState<string | null>(null)
  const [refreshing, setRefreshing] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [exporting, setExporting] = useState(false)
  // The server's MSG463, for a period the page let through; the page's own check usually catches it first.
  const [serverPeriodError, setServerPeriodError] = useState<string | null>(null)
  const latestReport = useRef(0)
  const latestTable = useRef(0)

  // The manager figures follow the period only.
  useEffect(() => {
    if (!valid) return
    const ticket = ++latestReport.current
    void Promise.resolve().then(async () => {
      try {
        const next = await getMeetingReport(from, to)
        if (ticket !== latestReport.current) return
        setReport(next)
        setReportError(null)
      } catch (error) {
        if (ticket !== latestReport.current) return
        if (isApiError(error, 400, 'MSG463')) setServerPeriodError(errorMessage(error))
        else setReportError(errorMessage(error))
      }
    })
  }, [from, to, valid, attempt])

  // The table follows the period and the query.
  useEffect(() => {
    if (!valid) return
    const ticket = ++latestTable.current
    void Promise.resolve().then(async () => {
      setRefreshing(true)
      try {
        const next = await getReportTable(Object.fromEntries(new URLSearchParams(apiKey)))
        if (ticket !== latestTable.current) return
        setTable(next)
        setServerPeriodError(null)
        setTableLoad({ kind: 'ready' })
      } catch (error) {
        if (ticket !== latestTable.current) return
        if (isApiError(error, 400, 'MSG463')) setServerPeriodError(errorMessage(error))
        else setTableLoad({ kind: 'error', message: errorMessage(error) })
      } finally {
        if (ticket === latestTable.current) setRefreshing(false)
      }
    })
  }, [apiKey, valid, attempt])

  useRefetchOnFocus(() => setAttempt((n) => n + 1), { enabled: valid })

  const retry = useCallback(() => {
    setTableLoad({ kind: 'loading' })
    setReportError(null)
    setAttempt((n) => n + 1)
  }, [])

  /** Replaces the period and the query in the URL, leaving any other parameter alone. */
  const navigate = (period: { from: string; to: string }, next: ReportQuery) => {
    setServerPeriodError(null)
    const kept = [...params.entries()].filter(([key]) => key !== 'from' && key !== 'to' && !QUERY_KEYS.includes(key))
    setParams(new URLSearchParams([...kept, ...Object.entries({ ...period, ...queryToUrl(next) })]), { replace: true })
  }
  const choosePeriod = (next: { from?: string; to?: string }) => navigate(followPeriod(from, to, next), query)
  const choose = (next: ReportQuery) => navigate({ from, to }, next)

  const download = async () => {
    setExporting(true)
    try {
      saveBlob(await exportReportTable(apiParams), exportFileName(query.entity, from, to))
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      setExporting(false)
    }
  }

  const periodError = !valid ? MSG463_INVALID_PERIOD : serverPeriodError
  const shownReport = valid && !serverPeriodError ? report : null
  const shownTable = valid && !serverPeriodError ? table : null

  return (
    <>
      <PageHeader
        title="Báo cáo"
        action={
          <button
            type="button"
            className="btn btn-primary btn-lg"
            onClick={() => void download()}
            disabled={!valid || !!serverPeriodError || exporting || tableLoad.kind !== 'ready'}
          >
            <Icon name="download" size={16} />
            {exporting ? 'Đang xuất…' : 'Xuất Excel'}
          </button>
        }
      />

      <div className="toolbar report-toolbar">
        <span className="report-toolbar-word" aria-hidden="true">
          Từ
        </span>
        <Select value={from} options={options} onChange={(next) => choosePeriod({ from: next })} label="Từ tháng" />
        <span className="report-toolbar-word" aria-hidden="true">
          đến
        </span>
        <Select value={to} options={options} onChange={(next) => choosePeriod({ to: next })} label="Đến tháng" />
        {shownTable && !periodError && (
          <span className="report-generated">Số liệu lúc {formatDateTime(shownTable.generatedAt)}</span>
        )}
      </div>
      {periodError && (
        <p className="field-error report-period-error" role="alert">
          {periodError}
        </p>
      )}

      {!periodError && (
        <>
          {reportError ? (
            <div className="alert alert-error page-alert" role="alert">
              <p>{reportError}</p>
              <button type="button" className="btn btn-outline btn-sm" onClick={retry}>
                <Icon name="refresh" size={16} />
                Thử lại
              </button>
            </div>
          ) : (
            <ReportBand report={shownReport} />
          )}
          <QueryPanel
            query={query}
            table={shownTable}
            load={tableLoad}
            stale={refreshing}
            nothingReadable={shownReport?.total === 0}
            onChange={choose}
            onRetry={retry}
          />
        </>
      )}
    </>
  )
}

/**
 * The figures a manager asks about first, as plain numbers in one row: meetings held, coming and called off, how many
 * came, how votes ended, minutes issued, hours recorded. Each counts only what the viewer may see.
 */
function ReportBand({ report }: { report: MeetingReport | null }) {
  const count = (status: MeetingStatus) => report?.byStatus.find((s) => s.status === status)?.count ?? 0
  const figures: { label: string; value: ReactNode; extra?: string }[] = report
    ? [
        { label: 'Đã họp', value: formatFigure(count('Ended'), 'count') },
        { label: 'Sắp họp', value: formatFigure(count('Approved') + count('Published'), 'count') },
        { label: 'Đã huỷ', value: formatFigure(count('Cancelled'), 'count') },
        {
          label: 'Tỷ lệ có mặt',
          value: report.attendance.averageRate === null ? <Empty /> : formatRate(report.attendance.averageRate),
        },
        {
          label: 'Thông qua',
          value: formatFigure(report.votes.passed, 'count'),
          extra: `/ ${formatFigure(report.votes.closed, 'count')} biểu quyết`,
        },
        {
          label: 'Biên bản ban hành',
          value: formatFigure(report.minutes.issued, 'count'),
          extra: report.minutes.waitingApproval > 0 ? `${report.minutes.waitingApproval} chờ duyệt` : undefined,
        },
        {
          label: 'Giờ ghi âm',
          value: formatFigure(report.recordings.recordedHours, 'hours'),
          extra: `${formatFigure(report.recordings.transcribedHours, 'hours')} gỡ băng`,
        },
      ]
    : []
  return (
    <section className="card report-band" aria-label="Số liệu chính" aria-busy={!report}>
      <dl className="report-figures">
        {report
          ? figures.map((f) => (
              <div key={f.label} className="report-figure">
                <dt>{f.label}</dt>
                <dd>
                  <strong>{f.value}</strong>
                  {f.extra && <span className="report-figure-extra">{f.extra}</span>}
                </dd>
              </div>
            ))
          : Array.from({ length: 7 }, (_, i) => (
              <div key={i} className="report-figure">
                <span className="skeleton" />
              </div>
            ))}
      </dl>
    </section>
  )
}

const Empty = () => <em className="value-empty">Chưa có</em>

function QueryPanel({
  query,
  table,
  load,
  stale,
  nothingReadable,
  onChange,
  onRetry,
}: {
  query: ReportQuery
  table: ReportTable | null
  load: Load
  stale: boolean
  nothingReadable: boolean
  onChange: (next: ReportQuery) => void
  onRetry: () => void
}) {
  const config = entityConfig(query.entity)
  const filtered = Object.keys(query.filters).length > 0
  // The table shown belongs to the query on screen only once it has loaded; until then the old one stays, dimmed.
  const current = table && table.entity === query.entity && table.groupBy === query.groupBy ? table : null
  const setFilter = (key: string, value: string) => {
    const filters = { ...query.filters }
    if (value) filters[key] = value
    else delete filters[key]
    onChange({ ...query, filters })
  }

  let body: ReactNode
  if (load.kind === 'error')
    body = (
      <div className="alert alert-error report-query-alert" role="alert">
        <p>{load.message}</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={onRetry}>
          <Icon name="refresh" size={16} />
          Thử lại
        </button>
      </div>
    )
  else if (!current) body = <TableSkeleton />
  else if (!current.total.count)
    body = (
      <div className="report-query-empty" aria-busy={stale}>
        <p>
          {filtered
            ? 'Không có dữ liệu khớp bộ lọc.'
            : nothingReadable
              ? MSG466_NOTHING_READABLE
              : 'Chưa có dữ liệu trong khoảng này.'}
        </p>
        {filtered && (
          <button type="button" className="btn btn-outline btn-sm" onClick={() => onChange({ ...query, filters: {} })}>
            Xoá bộ lọc
          </button>
        )}
      </div>
    )
  else body = <ResultTable table={current} query={query} stale={stale} onChange={onChange} />

  return (
    <section className="card report-query" aria-label="Truy vấn báo cáo">
      <div className="report-fields">
        <Field label="Dữ liệu">
          <Select<Entity>
            value={query.entity}
            options={ENTITIES.map((e) => ({ value: e.key, label: e.label }))}
            onChange={(entity) => onChange(switchEntity(query, entity))}
            label="Dữ liệu"
          />
        </Field>
        <Field label="Nhóm theo">
          <Select
            value={query.groupBy}
            options={config.groups.map((g) => ({ value: g, label: GROUP_LABEL[g] }))}
            onChange={(groupBy) => onChange(switchGroup(query, groupBy))}
            label="Nhóm theo"
          />
        </Field>
        <Field label="Hiển thị">
          <Select
            value={query.top || 'all'}
            options={[
              { value: 'all', label: 'Tất cả' },
              ...TOP_VALUES.map((n) => ({ value: n, label: `${n} nhóm đầu` })),
            ]}
            onChange={(top) => onChange({ ...query, top: top === 'all' ? '' : top })}
            label="Hiển thị"
          />
        </Field>
        {filtersOf(query.entity).map((key) => (
          <Field key={key} label={FILTERS[key].label}>
            <Select
              value={query.filters[key] ?? ''}
              options={[
                { value: '', label: FILTERS[key].all },
                ...FILTERS[key].values.map(([value, label]) => ({ value, label })),
              ]}
              onChange={(value) => setFilter(key, value)}
              label={FILTERS[key].label}
            />
          </Field>
        ))}
        {filtered && (
          <button
            type="button"
            className="btn btn-ghost btn-sm report-clear"
            onClick={() => onChange({ ...query, filters: {} })}
          >
            Xoá lọc
          </button>
        )}
      </div>
      {body}
    </section>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="report-field">
      <span aria-hidden="true">{label}</span>
      {children}
    </div>
  )
}

function TableSkeleton() {
  return (
    <div className="report-skeleton" aria-busy="true">
      {[0, 1, 2, 3, 4].map((i) => (
        <span key={i} className="skeleton" />
      ))}
    </div>
  )
}

/**
 * One row per group: its rank, the group, every figure, and a bar for the figure it is sorted by. A header sorts by its
 * column, biggest first; the group's own header puts the groups back in their order.
 */
function ResultTable({
  table,
  query,
  stale,
  onChange,
}: {
  table: ReportTable
  query: ReportQuery
  stale: boolean
  onChange: (next: ReportQuery) => void
}) {
  const columns = shownColumns(table)
  const barColumn = columns.find((c) => c.key === table.sort) ?? columns[0]
  const peak = Math.max(0, ...table.rows.map((r) => r.values[barColumn.key] ?? 0))
  const sortBy = (sort: string) => onChange({ ...query, sort })
  return (
    <>
      <div className="table-card report-table-card">
        <table className={stale ? 'data-table report-result data-table-stale' : 'data-table report-result'}>
          <thead>
            <tr>
              <th className="report-rank">#</th>
              <SortHeader active={table.sort === GROUP_SORT} onSort={() => sortBy(GROUP_SORT)}>
                {table.groupLabel}
              </SortHeader>
              {columns.map((c) => (
                <SortHeader key={c.key} numeric active={table.sort === c.key} onSort={() => sortBy(c.key)}>
                  {c.label}
                </SortHeader>
              ))}
              <th className="report-bar-cell">
                <span className="visually-hidden">Biểu đồ {barColumn.label}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {table.rows.map((row) => {
              const bar = row.values[barColumn.key] ?? 0
              return (
                <tr key={row.key}>
                  <td className="report-rank">{row.rank}</td>
                  <td className="report-group">
                    <GroupLabel groupBy={table.groupBy} rowKey={row.key} label={row.label} />
                  </td>
                  {columns.map((c) => (
                    <Figure key={c.key} label={c.label} value={formatFigure(row.values[c.key], c.format)} />
                  ))}
                  <td className="report-bar-cell" aria-hidden="true">
                    {bar > 0 && peak > 0 && <span className="report-bar" style={{ width: `${(bar / peak) * 100}%` }} />}
                  </td>
                </tr>
              )
            })}
          </tbody>
          <tfoot>
            <tr>
              <td className="report-rank" />
              <th scope="row" className="report-group">
                Tổng
              </th>
              {columns.map((c) => (
                <Figure key={c.key} label={c.label} value={formatFigure(table.total[c.key], c.format)} />
              ))}
              <td className="report-bar-cell" />
            </tr>
          </tfoot>
        </table>
      </div>
      {table.top !== null && table.groups > table.rows.length && (
        <p className="report-cut">
          {table.rows.length} / {table.groups} nhóm
        </p>
      )}
    </>
  )
}

/** One meeting per row: its count, held and called-off columns would say 1 or 0 on every row. */
function shownColumns(table: ReportTable) {
  if (table.entity !== 'meetings' || table.groupBy !== 'meeting') return table.columns
  return table.columns.filter((c) => !PER_MEETING_NOISE.includes(c.key))
}
const PER_MEETING_NOISE = ['count', 'ended', 'cancelled']

function Figure({ label, value }: { label: string; value: string | null }) {
  return (
    <td className={value === '0' ? 'cell-num cell-zero' : 'cell-num'} data-label={label}>
      {value ?? <Empty />}
    </td>
  )
}

function SortHeader({
  active,
  numeric = false,
  onSort,
  children,
}: {
  active: boolean
  numeric?: boolean
  onSort: () => void
  children: ReactNode
}) {
  return (
    <th
      className={numeric ? 'cell-num' : undefined}
      aria-sort={active ? (numeric ? 'descending' : 'ascending') : 'none'}
    >
      <button type="button" className={active ? 'sort-button sort-button-active' : 'sort-button'} onClick={onSort}>
        {children}
        <Icon name={active ? (numeric ? 'sortDesc' : 'sortAsc') : 'sortBoth'} size={14} />
      </button>
    </th>
  )
}

/** A meeting opens; a state reads as its badge; "nobody" and "none" read as absent. */
function GroupLabel({ groupBy, rowKey, label }: { groupBy: string; rowKey: string; label: string }) {
  if (groupBy === 'meeting')
    return (
      <Link to={routes.meeting(rowKey)} className="report-meeting">
        {label}
      </Link>
    )
  if (groupBy === 'status') return <StatusBadge status={rowKey as MeetingStatus} flat />
  if (groupBy === 'sensitivity') return <SensitivityBadge sensitivity={rowKey as Sensitivity} chip />
  if (rowKey === 'None') return <em className="value-empty">{label}</em>
  return <>{label}</>
}
