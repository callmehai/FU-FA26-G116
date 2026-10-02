import { useCallback, useRef, useState, type FormEvent } from 'react'
import { useBeforeUnload } from 'react-router-dom'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { SegmentedControl } from '../../components/SegmentedControl'
import { fromDateTimeInput, toDateTimeInput } from '../../lib/dateInput'
import { errorMessage, isApiError } from '../../lib/errors'
import {
  MEETING_MODES,
  MEETING_MODE_LABEL,
  SENSITIVITIES,
  SENSITIVITY_LABEL,
  type Meeting,
  type MeetingMode,
  type MeetingMutationResponse,
  type MeetingRequest,
  type Sensitivity,
} from '../../types/meeting'

/** The backend's limits (MeetingRules). The inputs enforce them; the backend checks again. */
const TITLE_MAX = 300
const LOCATION_MAX = 300
const DESCRIPTION_MAX = 4000

type Field = 'title' | 'startsAt' | 'endsAt' | 'mode' | 'location' | 'sensitivity' | 'description'
type FieldErrors = Partial<Record<Field, string>>

/** Field order on screen, so the first error to focus is the one highest on the page. */
const FIELD_ORDER: Field[] = ['title', 'startsAt', 'endsAt', 'mode', 'location', 'sensitivity', 'description']

interface Values {
  title: string
  startsAt: string
  endsAt: string
  mode: MeetingMode
  location: string
  sensitivity: Sensitivity
  description: string
}

const MODE_OPTIONS = MEETING_MODES.map((value) => ({ value, label: MEETING_MODE_LABEL[value] }))
const SENSITIVITY_OPTIONS = SENSITIVITIES.map((value) => ({ value, label: SENSITIVITY_LABEL[value] }))

/** Create starts from the defaults the backend would apply anyway: in person, routine sensitivity. */
function initialValues(meeting: Meeting | null): Values {
  return {
    title: meeting?.title ?? '',
    startsAt: meeting ? toDateTimeInput(meeting.startsAt) : '',
    endsAt: meeting?.endsAt ? toDateTimeInput(meeting.endsAt) : '',
    mode: meeting?.mode ?? 'InPerson',
    location: meeting?.location ?? '',
    sensitivity: meeting?.sensitivity ?? 'Normal',
    description: meeting?.description ?? '',
  }
}

/** BR-25, checked as soon as both times are known rather than only on submit. */
function endsAtError(values: Values): string | undefined {
  const start = fromDateTimeInput(values.startsAt)
  const end = fromDateTimeInput(values.endsAt)
  if (start && end && end <= start) return 'Thời gian kết thúc phải sau thời gian bắt đầu.'
  return undefined
}

/** BR-24 and BR-25 — the same rules the backend applies, so most mistakes never cost a round trip. */
function validate(values: Values): FieldErrors {
  const errors: FieldErrors = {}
  if (!values.title.trim()) errors.title = 'Tên cuộc họp không được để trống.'
  if (!fromDateTimeInput(values.startsAt)) errors.startsAt = 'Vui lòng chọn thời gian bắt đầu.'
  const endError = endsAtError(values)
  if (endError) errors.endsAt = endError
  return errors
}

function toRequest(values: Values): MeetingRequest {
  return {
    title: values.title.trim(),
    startsAt: fromDateTimeInput(values.startsAt)!,
    endsAt: fromDateTimeInput(values.endsAt),
    mode: values.mode,
    // An online meeting happens in the in-app call room, so it has no place. What was typed stays in
    // the form (switching back restores it) but is not saved.
    location: values.mode === 'Online' ? '' : values.location.trim(),
    sensitivity: values.sensitivity,
    description: values.description,
  }
}

function sameValues(a: Values, b: Values) {
  return (Object.keys(a) as (keyof Values)[]).every((key) => a[key] === b[key])
}

/**
 * UC-11 and UC-12 share this form, so creating and editing cannot drift apart.
 *
 * The page around it owns what differs: where the meeting comes from, what a save calls, and the
 * errors only one of them can hit (404 and 409 exist only for an edit). Everything the user types is
 * kept when a save fails.
 */
export function MeetingForm({
  meeting,
  submitLabel,
  pendingLabel,
  onSubmit,
  onSaved,
  onError,
  onCancel,
}: {
  /** The meeting being edited, or null to create one. */
  meeting: Meeting | null
  submitLabel: string
  pendingLabel: string
  onSubmit: (body: MeetingRequest) => Promise<MeetingMutationResponse>
  onSaved: (response: MeetingMutationResponse) => void
  /** Returns true when the page has handled the failure itself; otherwise the form shows it. */
  onError: (error: unknown) => boolean
  onCancel: () => void
}) {
  const [initial] = useState(() => initialValues(meeting))
  const [values, setValues] = useState(initial)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)
  const formRef = useRef<HTMLFormElement>(null)
  const submitted = useRef(false)

  const dirty = !sameValues(values, initial)

  // Closing the tab or reloading with unsaved input asks first. In-app links are not covered: the
  // app uses <BrowserRouter>, and route blocking needs a data router.
  useBeforeUnload(
    useCallback(
      (event: BeforeUnloadEvent) => {
        if (dirty && !submitted.current) event.preventDefault()
      },
      [dirty],
    ),
  )

  function set<K extends keyof Values>(key: K, value: Values[K]) {
    const next = { ...values, [key]: value }
    setValues(next)
    setFieldErrors((errors) => {
      const rest: FieldErrors = { ...errors }
      delete rest[key]
      // The end time is judged against the start, so changing either re-checks it.
      if (key === 'startsAt' || key === 'endsAt') {
        delete rest.endsAt
        const endError = endsAtError(next)
        if (endError) rest.endsAt = endError
      }
      return rest
    })
  }

  function focusFirstError(errors: FieldErrors) {
    const first = FIELD_ORDER.find((field) => errors[field])
    if (!first) return
    formRef.current?.querySelector<HTMLElement>(`[data-field="${first}"] :is(input, textarea, button)`)?.focus()
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const errors = validate(values)
    setFieldErrors(errors)
    setFormError(null)
    if (Object.keys(errors).length > 0) {
      focusFirstError(errors)
      return
    }

    setPending(true)
    try {
      const response = await onSubmit(toRequest(values))
      submitted.current = true
      onSaved(response)
    } catch (err) {
      setPending(false)
      if (onError(err)) return
      if (isApiError(err, 400, 'MSG31') && err.body?.errors) {
        // Every invalid field at once, keyed by the request's own field names.
        const serverErrors = err.body.errors as FieldErrors
        setFieldErrors(serverErrors)
        focusFirstError(serverErrors)
      } else {
        setFormError(errorMessage(err))
      }
    }
  }

  function cancel() {
    if (dirty) setConfirmDiscard(true)
    else onCancel()
  }

  return (
    <>
      <form ref={formRef} className="card form-card form-grid" onSubmit={handleSubmit} noValidate>
        <label className="field span-2" data-field="title">
          <span className="field-required">Tên cuộc họp</span>
          <input
            name="title"
            value={values.title}
            onChange={(e) => set('title', e.target.value)}
            maxLength={TITLE_MAX}
            placeholder="Ví dụ: Giao ban tuần 40"
            aria-invalid={!!fieldErrors.title}
            autoFocus={!meeting}
          />
          {fieldErrors.title && <small className="field-error">{fieldErrors.title}</small>}
        </label>

        <label className="field" data-field="startsAt">
          <span className="field-required">Thời gian bắt đầu</span>
          <input
            type="datetime-local"
            name="startsAt"
            value={values.startsAt}
            onChange={(e) => set('startsAt', e.target.value)}
            aria-invalid={!!fieldErrors.startsAt}
          />
          {fieldErrors.startsAt && <small className="field-error">{fieldErrors.startsAt}</small>}
        </label>

        <label className="field" data-field="endsAt">
          <span>Thời gian kết thúc dự kiến</span>
          <input
            type="datetime-local"
            name="endsAt"
            value={values.endsAt}
            min={values.startsAt || undefined}
            onChange={(e) => set('endsAt', e.target.value)}
            aria-invalid={!!fieldErrors.endsAt}
          />
          {fieldErrors.endsAt ? (
            <small className="field-error">{fieldErrors.endsAt}</small>
          ) : (
            <small className="field-hint">Có thể để trống nếu chưa xác định.</small>
          )}
        </label>

        <div className="field" data-field="mode">
          <span>Hình thức</span>
          <SegmentedControl
            value={values.mode}
            options={MODE_OPTIONS}
            onChange={(v) => set('mode', v)}
            label="Hình thức"
          />
          {fieldErrors.mode && <small className="field-error">{fieldErrors.mode}</small>}
        </div>

        {/* The place follows the mode. The cell keeps its size in every mode, so switching does not
            shift the rest of the form. */}
        {values.mode === 'Online' ? (
          <div className="field" data-field="location">
            <span>Địa điểm</span>
            <p className="field-static">
              <Icon name="video" size={16} />
              Phòng gọi trực tuyến trong TrustCabinet
            </p>
            <small className="field-hint">Không cần địa điểm hay đường dẫn họp bên ngoài.</small>
          </div>
        ) : (
          <label className="field" data-field="location">
            <span>{values.mode === 'Hybrid' ? 'Địa điểm họp trực tiếp' : 'Địa điểm'}</span>
            <input
              name="location"
              value={values.location}
              onChange={(e) => set('location', e.target.value)}
              maxLength={LOCATION_MAX}
              placeholder="Ví dụ: Phòng họp A — Tầng 4"
              aria-invalid={!!fieldErrors.location}
            />
            {fieldErrors.location ? (
              <small className="field-error">{fieldErrors.location}</small>
            ) : (
              // One line in every mode, so switching mode never moves the rows below.
              <small className="field-hint">
                {values.mode === 'Hybrid' ? 'Người ở xa vào phòng gọi trực tuyến.' : 'Phòng họp hoặc địa chỉ.'}
              </small>
            )}
          </label>
        )}

        <div className="field" data-field="sensitivity">
          <span>Mức độ mật</span>
          <SegmentedControl
            value={values.sensitivity}
            options={SENSITIVITY_OPTIONS}
            onChange={(v) => set('sensitivity', v)}
            label="Mức độ mật"
          />
          {fieldErrors.sensitivity && <small className="field-error">{fieldErrors.sensitivity}</small>}
        </div>

        <label className="field span-2" data-field="description">
          <span>Mô tả</span>
          <textarea
            name="description"
            rows={4}
            value={values.description}
            onChange={(e) => set('description', e.target.value)}
            maxLength={DESCRIPTION_MAX}
            placeholder="Mục đích, nội dung chính dự kiến bàn…"
            aria-invalid={!!fieldErrors.description}
          />
          <span className="field-foot">
            {fieldErrors.description ? <small className="field-error">{fieldErrors.description}</small> : <span />}
            <small className="field-counter">
              {values.description.length.toLocaleString('vi-VN')}/{DESCRIPTION_MAX.toLocaleString('vi-VN')}
            </small>
          </span>
        </label>

        {formError && (
          <p className="alert alert-error span-2" role="alert">
            {formError}
          </p>
        )}

        <div className="form-actions span-2">
          <button type="button" className="btn btn-ghost" onClick={cancel} disabled={pending}>
            Huỷ
          </button>
          <button type="submit" className="btn btn-primary" disabled={pending}>
            {pending ? pendingLabel : submitLabel}
          </button>
        </div>
      </form>

      {confirmDiscard && (
        <ConfirmDialog
          title="Bỏ thay đổi chưa lưu?"
          tone="danger"
          confirmLabel="Bỏ thay đổi"
          cancelLabel="Tiếp tục sửa"
          pendingLabel="Đang thoát…"
          onConfirm={async () => onCancel()}
          onClose={() => setConfirmDiscard(false)}
        >
          Những gì bạn vừa nhập sẽ không được lưu.
        </ConfirmDialog>
      )}
    </>
  )
}
