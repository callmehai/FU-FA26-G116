import { PageHeader } from '../../components/PageHeader'

export function CalendarPage() {
  return (
    <>
      <PageHeader title="Lịch làm việc" description="Các cuộc họp bạn tham dự, xếp theo tháng." />

      <div className="card card-pending">
        <p>
          Lưới lịch chưa dựng. Sẽ hiện thực khi backend có{' '}
          <code>GET /api/meetings?from=…&amp;to=…</code>.
        </p>
      </div>
    </>
  )
}
