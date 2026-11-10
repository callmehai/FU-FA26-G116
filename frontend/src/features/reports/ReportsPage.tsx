import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { exportMeetingReport, getMeetingReport } from '../../api/reports'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { PageHeader } from '../../components/PageHeader'
import { Select, type SelectOption } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import type { MeetingReport } from '../../types/report'
import { saveBlob } from '../documents/documentFile'
import { SensitivityBadge, StatusBadge } from '../meetings/StatusBadge'
import {
  MSG463_INVALID_PERIOD,
  MSG466_NOTHING_READABLE,
  defaultPeriod,
  formatHours,
  formatMinutes,
  formatRate,
  monthLabel,
  monthOptions,
  parseMonth,
  periodValid,
  vietnamMonth,
} from './reportFormat'

type Load = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'ready' }

/**
 * SCRUM-38 — the meeting report (UC-70a) and its Excel export (UC-70b). Open to every internal user (BR-432): the
 * server counts only the meetings the viewer may read, so this page shows nobody more than the meeting list does.
 * The period lives in the URL (`?from=yyyy-MM&to=yyyy-MM`), so a reload or a shared link opens the same figures.
 * The page shows counts and averages only, never a meeting (BR-437); the figures are numbers in tables, not tiles.
 */
export function ReportsPage() {
  const toast = useToast()
  const [params, setParams] = useSearchParams()
  const now = vietnamMonth()
  const fallback = defaultPeriod(now)
  // A month that does not parse is dropped, as an unknown filter value would be, and the default takes its place.
  const from = parseMonth(params.get('from')) ?? fallback.from
  const to = parseMonth(params.get('to')) ?? fallback.to
  const valid = periodValid(from, to)
  const options = useMemo<SelectOption<string>[]>(() => monthOptions(now, [from, to]), [now, from, to])

  const [report, setReport] = useState<MeetingReport | null>(null)
  const [load, setLoad] = useState<Load>({ kind: 'loading' })
  const [refreshing, setRefreshing] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [exporting, setExporting] = useState(false)
  // The server's MSG463, for a period the page let through; the page's own check usually catches it first.
  const [serverPeriodError, setServerPeriodError] = useState<string | null>(null)
  const latest = useRef(0)

  useEffect(() => {
    if (!valid) return
    const ticket = ++latest.current
    void Promise.resolve().then(async () => {
      setRefreshing(true)
      try {
        const next = await getMeetingReport(from, to)
        if (ticket !== latest.current) return
        setReport(next)
        setServerPeriodError(null)
        setLoad({ kind: 'ready' })
      } catch (error) {
        if (ticket !== latest.current) return
        if (isApiError(error, 400, 'MSG463')) setServerPeriodError(errorMessage(error))
        else setLoad({ kind: 'error', message: errorMessage(error) })
      } finally {
        if (ticket === latest.current) setRefreshing(false)
      }
    })
  }, [from, to, valid, attempt])

  const choose = (next: { from?: string; to?: string }) => {
    setServerPeriodError(null)
    setParams({ from: next.from ?? from, to: next.to ?? to }, { replace: true })
  }

  const retry = useCallback(() => {
    setLoad({ kind: 'loading' })
    setAttempt((n) => n + 1)
  }, [])

  const download = async () => {
    setExporting(true)
    try {
      saveBlob(await exportMeetingReport(from, to), `bao-cao-cuoc-hop_${from}_${to}.xlsx`)
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      setExporting(false)
    }
  }

  const periodError = !valid ? MSG463_INVALID_PERIOD : serverPeriodError
  const shown = valid && !serverPeriodError ? report : null

  let body: ReactNode
  if (periodError) body = null
  else if (load.kind === 'error')
    body = (
      <div className="alert alert-error page-alert" role="alert">
        <p>{load.message}</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={retry}>
          <Icon name="refresh" size={16} />
          Thử lại
        </button>
      </div>
    )
  else if (!shown) body = <ReportSkeleton />
  else if (shown.total === 0)
    body = (
      <div className="card report-empty" aria-busy={refreshing}>
        <p className="empty-title">{MSG466_NOTHING_READABLE}</p>
      </div>
    )
  else body = <ReportFigures report={shown} stale={refreshing} />

  return (
    <>
      <PageHeader
        title="Báo cáo"
        description={shown && !periodError ? `${shown.total} cuộc họp` : undefined}
        action={
          <button
            type="button"
            className="btn btn-primary btn-lg"
            onClick={() => void download()}
            disabled={!valid || !!serverPeriodError || exporting}
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
        <Select value={from} options={options} onChange={(next) => choose({ from: next })} label="Từ tháng" />
        <span className="report-toolbar-word" aria-hidden="true">
          đến
        </span>
        <Select value={to} options={options} onChange={(next) => choose({ to: next })} label="Đến tháng" />
        {shown && !periodError && (
          <span className="report-generated">Số liệu lúc {formatDateTime(shown.generatedAt)}</span>
        )}
      </div>
      {periodError && (
        <p className="field-error report-period-error" role="alert">
          {periodError}
        </p>
      )}

      {body}
    </>
  )
}

/** Sized like the figures, so nothing jumps when they arrive. */
function ReportSkeleton() {
  return (
    <div className="report-grid" aria-busy="true">
      <section className="card report-card">
        <h2 className="report-card-title">Tổng quan</h2>
        <div className="report-skeleton">
          {[0, 1, 2].map((i) => (
            <span key={i} className="skeleton" />
          ))}
        </div>
      </section>
      <section className="card report-card">
        <h2 className="report-card-title">Theo trạng thái</h2>
        <div className="report-skeleton">
          {[0, 1, 2, 3].map((i) => (
            <span key={i} className="skeleton" />
          ))}
        </div>
      </section>
    </div>
  )
}

const NO_DATA = <em className="value-empty">Chưa có dữ liệu</em>

function ReportFigures({ report, stale }: { report: MeetingReport; stale: boolean }) {
  const ended = report.attendance.meetings + report.attendance.meetingsWithoutAttendance
  const peak = Math.max(1, ...report.byMonth.map((m) => m.count))
  const tableClass = stale ? 'data-table report-table data-table-stale' : 'data-table report-table'

  return (
    <div className="report-grid" aria-busy={stale}>
      <div className="report-column">
        <section className="card report-card" aria-labelledby="report-overview">
          <h2 className="report-card-title" id="report-overview">
            Tổng quan
          </h2>
          <div className="table-card">
            <table className={tableClass}>
              <thead>
                <tr>
                  <th>Chỉ số</th>
                  <th className="cell-num">Giá trị</th>
                </tr>
              </thead>
              <tbody>
                <Metric
                  label="Tỷ lệ tham dự trung bình"
                  explain="Số người có mặt chia cho số người được mời (thành viên và khách), trung bình trên các cuộc họp đã kết thúc có điểm danh."
                  value={report.attendance.averageRate === null ? NO_DATA : formatRate(report.attendance.averageRate)}
                  basis={report.attendance.meetings}
                  of={ended}
                />
                <Metric
                  label="Thời lượng họp trung bình"
                  explain="Từ lúc bắt đầu tới lúc kết thúc thực tế, trên các cuộc họp đã kết thúc."
                  value={
                    report.duration.averageMinutes === null ? NO_DATA : formatMinutes(report.duration.averageMinutes)
                  }
                  basis={report.duration.meetings}
                  of={ended}
                />
                <Metric
                  label="Từ kết thúc tới ban hành biên bản"
                  explain="Từ lúc kết thúc họp tới lần ban hành biên bản đầu tiên, trung bình trên các cuộc họp đã có biên bản ban hành. Bản đính chính không tính."
                  value={
                    report.minutesTurnaround.averageHours === null
                      ? NO_DATA
                      : formatHours(report.minutesTurnaround.averageHours)
                  }
                  basis={report.minutesTurnaround.meetings}
                  of={ended}
                />
              </tbody>
            </table>
          </div>
        </section>

        <section className="card report-card" aria-labelledby="report-months">
          <h2 className="report-card-title" id="report-months">
            Theo tháng
          </h2>
          <div className="table-card">
            <table className={tableClass}>
              <thead>
                <tr>
                  <th>Tháng</th>
                  <th className="cell-num">Số cuộc họp</th>
                  <th className="report-bar-cell">
                    <span className="visually-hidden">Biểu đồ</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {report.byMonth.map((m) => (
                  <tr key={m.month}>
                    <td>{monthLabel(m.month)}</td>
                    <td className={m.count === 0 ? 'cell-num cell-zero' : 'cell-num'}>{m.count}</td>
                    <td className="report-bar-cell" aria-hidden="true">
                      {m.count > 0 && <span className="report-bar" style={{ width: `${(m.count / peak) * 100}%` }} />}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <div className="report-column">
        <section className="card report-card" aria-labelledby="report-status">
          <h2 className="report-card-title" id="report-status">
            Theo trạng thái
          </h2>
          <div className="table-card">
            <table className={tableClass}>
              <tbody>
                {report.byStatus.map((s) => (
                  <tr key={s.status}>
                    <td>
                      <StatusBadge status={s.status} />
                    </td>
                    <td className={s.count === 0 ? 'cell-num cell-zero' : 'cell-num'}>{s.count}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section className="card report-card" aria-labelledby="report-sensitivity">
          <h2 className="report-card-title" id="report-sensitivity">
            Theo mức độ
          </h2>
          <div className="table-card">
            <table className={tableClass}>
              <tbody>
                {report.bySensitivity.map((s) => (
                  <tr key={s.sensitivity}>
                    <td>
                      <SensitivityBadge sensitivity={s.sensitivity} chip />
                    </td>
                    <td className={s.count === 0 ? 'cell-num cell-zero' : 'cell-num'}>{s.count}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  )
}

/** One average: its value, and how many of the period's Ended meetings it is based on ("18/20"). */
function Metric({
  label,
  explain,
  value,
  basis,
  of,
}: {
  label: string
  explain: string
  value: ReactNode
  basis: number
  of: number
}) {
  return (
    <tr>
      <td className="report-metric">
        {label}
        <InfoTip text={explain} />
      </td>
      <td className="cell-num report-value">
        <strong>{value}</strong>
        <span className="report-basis">
          trên {basis}/{of} cuộc họp
        </span>
      </td>
    </tr>
  )
}
