import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { createMeeting, listMeetingApprovers } from '../../api/meetings'
import { ApiError } from '../../api/client'
import { PageHeader } from '../../components/PageHeader'
import { Select } from '../../components/Select'
import { errorMessage } from '../../lib/errors'
import { routes } from '../../lib/routes'
import {
  MEETING_MODE_LABEL,
  SENSITIVITIES,
  SENSITIVITY_LABEL,
  type CreateMeetingRequest,
  type MeetingApproverOption,
  type MeetingMode,
  type MeetingMutationResponse,
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

type Field = keyof CreateMeetingRequest
type FieldErrors = Partial<Record<Field, string>>

/** UC-11 form for creating a draft meeting. */
export function CreateMeetingPage() {
  const [mode, setMode] = useState<MeetingMode>('InPerson')
  const [sensitivity, setSensitivity] = useState<Sensitivity>('Normal')
  const [title, setTitle] = useState('')
  const [startsAt, setStartsAt] = useState('')
  const [endsAt, setEndsAt] = useState('')
  const [location, setLocation] = useState('')
  const [description, setDescription] = useState('')
  const [assignedApproverId, setAssignedApproverId] = useState('')
  const [approvers, setApprovers] = useState<MeetingApproverOption[]>([])
  const [approversLoading, setApproversLoading] = useState(true)
  const [approversError, setApproversError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [created, setCreated] = useState<MeetingMutationResponse | null>(null)
  const [pending, setPending] = useState(false)
  const [approverReload, setApproverReload] = useState(0)

  function retryApprovers() {
    setApproversLoading(true)
    setApproversError(null)
    setApproverReload((value) => value + 1)
  }

  useEffect(() => {
    let current = true
    listMeetingApprovers().then(
      (options) => {
        if (current) setApprovers(options)
      },
      (error: unknown) => {
        if (current) setApproversError(errorMessage(error))
      },
    ).finally(() => {
      if (current) setApproversLoading(false)
    })
    return () => {
      current = false
    }
  }, [approverReload])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const errors: FieldErrors = {}
    if (!assignedApproverId) errors.assignedApproverId = 'Vui lòng chọn người duyệt.'
    setFieldErrors(errors)
    setFormError(null)
    if (Object.keys(errors).length > 0) return

    const body: CreateMeetingRequest = {
      title: title.trim(),
      startsAt: new Date(startsAt).toISOString(),
      endsAt: endsAt ? new Date(endsAt).toISOString() : null,
      mode,
      location: location.trim(),
      sensitivity,
      description,
      assignedApproverId,
    }

    setPending(true)
    try {
      setCreated(await createMeeting(body))
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && error.body?.errors) {
        setFieldErrors(error.body.errors as FieldErrors)
        if (Object.keys(error.body.errors).length === 0) setFormError(errorMessage(error))
      } else {
        setFormError(errorMessage(error))
      }
    } finally {
      setPending(false)
    }
  }

  const approverOptions = [
    { value: '', label: 'Chọn người duyệt' },
    ...approvers.map((approver) => ({ value: approver.id, label: approver.fullName })),
  ]

  return (
    <>
      <nav className="breadcrumb" aria-label="Đường dẫn">
        <Link to={routes.meetings}>Cuộc họp</Link>
        <span aria-hidden="true">›</span>
        <span>Tạo cuộc họp</span>
      </nav>

      <PageHeader title="Tạo cuộc họp" description="Điền thông tin cơ bản, sau đó mời thành phần tham dự." />

      {created && (
        <div className="alert alert-info page-alert" role="status">
          <div>
            <strong>{created.message}</strong>
            <p className="alert-hint">Mã cuộc họp: {created.meeting.id}</p>
          </div>
          <button type="button" className="btn btn-ghost" onClick={() => setCreated(null)}>
            Tạo cuộc họp khác
          </button>
        </div>
      )}

      {formError && <p className="alert alert-error">{formError}</p>}
      {approversError && (
        <div className="alert alert-error page-alert" role="alert">
          <span>{approversError}</span>
          <button type="button" className="btn btn-ghost" onClick={retryApprovers}>
            Tải lại người duyệt
          </button>
        </div>
      )}

      <form className="card form-grid" onSubmit={handleSubmit}>
        <label className="field span-2">
          <span className="field-required">Tên cuộc họp</span>
          <input
            type="text"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            placeholder="Giao ban tuần…"
            required
            maxLength={300}
            aria-invalid={Boolean(fieldErrors.title)}
          />
          {fieldErrors.title && <small className="field-error">{fieldErrors.title}</small>}
        </label>

        <label className="field">
          <span className="field-required">Thời gian bắt đầu</span>
          <input
            type="datetime-local"
            value={startsAt}
            onChange={(event) => setStartsAt(event.target.value)}
            required
            aria-invalid={Boolean(fieldErrors.startsAt)}
          />
          {fieldErrors.startsAt && <small className="field-error">{fieldErrors.startsAt}</small>}
        </label>

        <label className="field">
          <span>Thời gian kết thúc dự kiến (không bắt buộc)</span>
          <input
            type="datetime-local"
            value={endsAt}
            onChange={(event) => setEndsAt(event.target.value)}
            aria-invalid={Boolean(fieldErrors.endsAt)}
          />
          {fieldErrors.endsAt && <small className="field-error">{fieldErrors.endsAt}</small>}
        </label>

        <div className="field">
          <span>Hình thức</span>
          <Select value={mode} options={MODE_OPTIONS} onChange={setMode} label="Hình thức" />
        </div>

        <div className="field">
          <span>Mức độ</span>
          <Select value={sensitivity} options={SENSITIVITY_OPTIONS} onChange={setSensitivity} label="Mức độ" />
        </div>

        <div className="field">
          <span className="field-required">Người duyệt</span>
          <Select
            value={assignedApproverId}
            options={approverOptions}
            onChange={setAssignedApproverId}
            label="Người duyệt"
          />
          {approversLoading && <small className="field-hint">Đang tải danh sách người duyệt…</small>}
          {!approversLoading && !approversError && approvers.length === 0 && (
            <small className="field-error">Chưa có tài khoản nào được cấp quyền MeetingApprover.</small>
          )}
          {fieldErrors.assignedApproverId && (
            <small className="field-error">{fieldErrors.assignedApproverId}</small>
          )}
        </div>

        <label className="field span-2">
          <span>Địa điểm</span>
          <input
            type="text"
            value={location}
            onChange={(event) => setLocation(event.target.value)}
            placeholder="Phòng họp A — Tầng 4"
            maxLength={300}
            aria-invalid={Boolean(fieldErrors.location)}
          />
          {fieldErrors.location && <small className="field-error">{fieldErrors.location}</small>}
        </label>

        <label className="field span-2">
          <span>Mô tả</span>
          <textarea
            rows={4}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Nội dung dự kiến của cuộc họp…"
            maxLength={4000}
            aria-invalid={Boolean(fieldErrors.description)}
          />
          {fieldErrors.description && <small className="field-error">{fieldErrors.description}</small>}
        </label>

        <div className="form-actions span-2">
          <Link className="btn btn-ghost" to={routes.meetings}>
            Huỷ
          </Link>
          <button
            className="btn btn-primary"
            type="submit"
            disabled={pending || approversLoading || approversError !== null || approvers.length === 0}
          >
            {pending ? 'Đang lưu…' : 'Lưu nháp'}
          </button>
        </div>
      </form>
    </>
  )
}
