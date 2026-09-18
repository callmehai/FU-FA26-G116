import { useState } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../components/PageHeader'
import { Select } from '../../components/Select'
import { routes } from '../../lib/routes'
import {
  MEETING_MODE_LABEL,
  SENSITIVITIES,
  SENSITIVITY_LABEL,
  type MeetingMode,
  type Sensitivity,
} from '../../types/meeting'

const MODE_OPTIONS = (['InPerson', 'Online', 'Hybrid'] as MeetingMode[]).map((value) => ({
  value,
  label: MEETING_MODE_LABEL[value],
}))

const SENSITIVITY_OPTIONS = SENSITIVITIES.map((value) => ({
  value,
  label: SENSITIVITY_LABEL[value],
}))

/**
 * Form shell for creating a meeting. The fields mirror what the detail screen reads back, so the
 * shape of `POST /api/meetings` is already visible; submitting is deliberately inert until that
 * endpoint exists, rather than pretending to save.
 */
export function CreateMeetingPage() {
  const [mode, setMode] = useState<MeetingMode>('InPerson')
  const [sensitivity, setSensitivity] = useState<Sensitivity>('Normal')

  return (
    <>
      <nav className="breadcrumb" aria-label="Đường dẫn">
        <Link to={routes.meetings}>Cuộc họp</Link>
        <span aria-hidden="true">›</span>
        <span>Tạo cuộc họp</span>
      </nav>

      <PageHeader title="Tạo cuộc họp" description="Điền thông tin cơ bản, sau đó mời thành phần tham dự." />

      <form className="card form-grid" onSubmit={(e) => e.preventDefault()}>
        <label className="field span-2">
          <span>Tên cuộc họp</span>
          <input type="text" placeholder="Giao ban tuần…" required />
        </label>

        <label className="field">
          <span>Thời gian bắt đầu</span>
          <input type="datetime-local" required />
        </label>

        <label className="field">
          <span>Thời gian kết thúc dự kiến</span>
          <input type="datetime-local" required />
        </label>

        <div className="field">
          <span>Hình thức</span>
          <Select value={mode} options={MODE_OPTIONS} onChange={setMode} label="Hình thức" />
        </div>

        <div className="field">
          <span>Mức độ</span>
          <Select value={sensitivity} options={SENSITIVITY_OPTIONS} onChange={setSensitivity} label="Mức độ" />
        </div>

        <label className="field span-2">
          <span>Địa điểm</span>
          <input type="text" placeholder="Phòng họp A — Tầng 4" />
        </label>

        <label className="field span-2">
          <span>Mô tả</span>
          <textarea rows={4} placeholder="Nội dung dự kiến của cuộc họp…" />
        </label>

        <div className="form-actions span-2">
          <Link className="btn btn-ghost" to={routes.meetings}>
            Huỷ
          </Link>
          <button className="btn btn-primary" type="submit" disabled>
            Lưu nháp
          </button>
        </div>

        <p className="mock-note span-2">
          Nút lưu đang tắt vì backend chưa có <code>POST /api/meetings</code>. Bật lại khi endpoint
          sẵn sàng.
        </p>
      </form>
    </>
  )
}
