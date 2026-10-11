import { useCallback, useRef, useState, type FormEvent } from 'react'
import { useBeforeUnload } from 'react-router-dom'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { SegmentedControl } from '../../components/SegmentedControl'
import { fromDateTimeInput, toDateTimeInput } from '../../lib/dateInput'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDate } from '../../lib/format'
import {
  MEETING_MODES,
  MEETING_MODE_LABEL,
  RESCHEDULABLE_STATUSES,
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
const ONLINE_LINK_MAX = 1000
const DESCRIPTION_MAX = 4000

/** `date` is the form's own field; the two times use the request's names so server errors land on them. */
type Field =
  | 'title'
  | 'description'
  | 'date'
  | 'plannedStartTime'
  | 'plannedEndTime'
  | 'mode'
  | 'sensitivity'
  | 'location'
  | 'onlineLink'
type FieldErrors = Partial<Record<Field, string>>

/** Field order on screen, so the first error to focus is the one highest on the page. */
const FIELD_ORDER: Field[] = [
  'title',
  'description',
  'date',
  'plannedStartTime',
  'plannedEndTime',
  'mode',
  'sensitivity',
  'location',
  'onlineLink',
]

interface Values {
  title: string
  description: string
  /** `YYYY-MM-DD` in the viewer's timezone. One meeting is one sitting, so one day (ADR-0013). */
  date: string
  /** `HH:mm` */
  start: string
  end: string
  /**
   * Days from `date` to the end's day. 0 for every meeting the form creates; an older meeting (a
   * migrated multi-session one, or one written through the API) may end on a later day, and the form
   * keeps that rather than silently moving the end onto the start's day.
   */
  endDays: number
  mode: MeetingMode
  sensitivity: Sensitivity
  location: string
  onlineLink: string
}

const MODE_OPTIONS = MEETING_MODES.map((value) => ({ value, label: MEETING_MODE_LABEL[value] }))
const SENSITIVITY_OPTIONS = SENSITIVITIES.map((value) => ({ value, label: SENSITIVITY_LABEL[value] }))
const LOCKED_HINT = 'Đã công bố, không đổi được.'

/** Create starts from the defaults the backend would apply anyway: in person, routine sensitivity. */
function initialValues(meeting: Meeting | null): Values {
  const start = meeting ? toDateTimeInput(meeting.plannedStartTime) : ''
  const end = meeting ? toDateTimeInput(meeting.plannedEndTime) : ''
  return {
    title: meeting?.title ?? '',
    description: meeting?.description ?? '',
    date: start.slice(0, 10),
    start: start.slice(11),
    end: end.slice(11),
    endDays: meeting ? daysBetween(start.slice(0, 10), end.slice(0, 10)) : 0,
    mode: meeting?.mode ?? 'InPerson',
    sensitivity: meeting?.sensitivity ?? 'Normal',
    location: meeting?.location ?? '',
    onlineLink: meeting?.onlineLink ?? '',
  }
}

const DAY = 86_400_000
/** Calendar days between two `YYYY-MM-DD` values; read as UTC so a DST change cannot shave an hour off. */
const daysBetween = (from: string, to: string) => Math.round((Date.parse(to) - Date.parse(from)) / DAY)
const addDays = (date: string, days: number) => new Date(Date.parse(date) + days * DAY).toISOString().slice(0, 10)

const startOf = (values: Values) => (values.date && values.start ? fromDateTimeInput(`${values.date}T${values.start}`) : null)
const endOf = (values: Values) =>
  values.date && values.end ? fromDateTimeInput(`${addDays(values.date, values.endDays)}T${values.end}`) : null

/** BR-62: checked as soon as both times are known rather than only on submit. */
function endError(values: Values): string | undefined {
  const start = startOf(values)
  const end = endOf(values)
  if (start && end && end <= start) return 'Thời gian kết thúc phải sau thời gian bắt đầu.'
  return undefined
}

/**
 * BR-24, BR-62 and — on create (BR-162) and reschedule (BR-75) — a start in the future: the same
 * rules the backend applies, so the commonest mistakes never cost a round trip.
 */
function validate(values: Values, needsFutureStart: boolean): FieldErrors {
  const errors: FieldErrors = {}
  if (!values.title.trim()) errors.title = 'Tên cuộc họp không được để trống.'
  if (!values.date) errors.date = 'Vui lòng chọn ngày họp.'
  if (!values.start) errors.plannedStartTime = 'Vui lòng chọn giờ bắt đầu.'
  if (!values.end) errors.plannedEndTime = 'Vui lòng chọn giờ kết thúc.'
  const end = endError(values)
  if (end) errors.plannedEndTime = end
  const start = startOf(values)
  if (needsFutureStart && start && new Date(start).getTime() <= Date.now()) {
    errors.plannedStartTime = 'Thời gian bắt đầu phải sau thời điểm hiện tại.'
  }
  if (values.mode !== 'InPerson' && values.onlineLink.trim().length > ONLINE_LINK_MAX) {
    errors.onlineLink = 'Link họp trực tuyến không được vượt quá 1.000 ký tự.'
  }
  return errors
}

/** The end's day for the hint under "Kết thúc", e.g. "16/10/2026". */
const endDayLabel = (values: Values) => formatDate(`${addDays(values.date, values.endDays)}T12:00:00`)

function toRequest(values: Values): MeetingRequest {
  return {
    title: values.title.trim(),
    plannedStartTime: startOf(values)!,
    plannedEndTime: endOf(values)!,
    mode: values.mode,
    // What the mode hides stays in the form (switching back restores it) but is not saved: an online
    // meeting has no room, an in-person one no link.
    location: values.mode === 'Online' ? '' : values.location.trim(),
    onlineLink: values.mode === 'InPerson' ? null : values.onlineLink.trim() || null,
    sensitivity: values.sensitivity,
    description: values.description,
  }
}

function sameValues(a: Values, b: Values) {
  return (Object.keys(a) as (keyof Values)[]).every((key) => a[key] === b[key])
}

/**
 * UC-14 and UC-15 share this form, so creating and editing cannot drift apart. A Published meeting
 * opens it in reschedule mode (UC-15 AF-1): only the times, place and link stay editable (BR-158).
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
  const reschedule = !!meeting && RESCHEDULABLE_STATUSES.includes(meeting.status)
  const needsFutureStart = !meeting || reschedule
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

  /** `endDays` is not a field: it only follows from the meeting being edited. */
  function set<K extends Exclude<keyof Values, 'endDays'>>(key: K, value: Values[K]) {
    const next = { ...values, [key]: value }
    setValues(next)
    setFieldErrors((errors) => {
      const rest: FieldErrors = { ...errors }
      const field: Field = key === 'start' ? 'plannedStartTime' : key === 'end' ? 'plannedEndTime' : key
      delete rest[field]
      // The end is judged against the start, so changing the day or either time re-checks it.
      if (key === 'date' || key === 'start' || key === 'end') {
        delete rest.plannedEndTime
        const error = endError(next)
        if (error) rest.plannedEndTime = error
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
    const errors = validate(values, needsFutureStart)
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

  const errorOf = (field: Field, hint?: string) =>
    fieldErrors[field] ? (
      <small className="field-error">{fieldErrors[field]}</small>
    ) : (
      hint && <small className="field-hint">{hint}</small>
    )

  return (
    <>
      <form ref={formRef} className="card form-card form-grid" onSubmit={handleSubmit} noValidate>
        {reschedule && (
          <p className="alert alert-info span-2">
            Cuộc họp đã công bố: chỉ dời được thời gian, địa điểm và link họp. Lưu xong, mọi người được mời sẽ nhận thông
            báo lịch mới.
          </p>
        )}

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
            disabled={reschedule}
          />
          {errorOf('title', reschedule ? LOCKED_HINT : undefined)}
        </label>

        <label className="field span-2" data-field="description">
          <span>Mô tả</span>
          <textarea
            name="description"
            rows={3}
            value={values.description}
            onChange={(e) => set('description', e.target.value)}
            maxLength={DESCRIPTION_MAX}
            placeholder={reschedule ? undefined : 'Mục đích, nội dung chính dự kiến bàn…'}
            aria-invalid={!!fieldErrors.description}
            disabled={reschedule}
          />
          <span className="field-foot">
            {fieldErrors.description ? (
              <small className="field-error">{fieldErrors.description}</small>
            ) : reschedule ? (
              <small className="field-hint">{LOCKED_HINT}</small>
            ) : (
              <span />
            )}
            <small className="field-counter">
              {values.description.length.toLocaleString('vi-VN')}/{DESCRIPTION_MAX.toLocaleString('vi-VN')}
            </small>
          </span>
        </label>

        <div className="form-row-3 span-2">
          <label className="field" data-field="date">
            <span className="field-required">Ngày họp</span>
            <input
              type="date"
              name="date"
              value={values.date}
              onChange={(e) => set('date', e.target.value)}
              aria-invalid={!!fieldErrors.date}
            />
            {errorOf('date')}
          </label>
          <label className="field" data-field="plannedStartTime">
            <span className="field-required">Bắt đầu</span>
            <input
              type="time"
              name="start"
              value={values.start}
              onChange={(e) => set('start', e.target.value)}
              aria-invalid={!!fieldErrors.plannedStartTime}
            />
            {errorOf('plannedStartTime')}
          </label>
          <label className="field" data-field="plannedEndTime">
            <span className="field-required">Kết thúc</span>
            <input
              type="time"
              name="end"
              value={values.end}
              min={values.endDays === 0 ? values.start || undefined : undefined}
              onChange={(e) => set('end', e.target.value)}
              aria-invalid={!!fieldErrors.plannedEndTime}
            />
            {errorOf('plannedEndTime', values.endDays > 0 && values.date ? `Ngày ${endDayLabel(values)}.` : undefined)}
          </label>
        </div>

        <div className="field" data-field="mode">
          <span>Hình thức</span>
          <SegmentedControl
            value={values.mode}
            options={MODE_OPTIONS}
            onChange={(v) => set('mode', v)}
            label="Hình thức"
            disabled={reschedule}
          />
          {errorOf('mode', reschedule ? LOCKED_HINT : undefined)}
        </div>

        <div className="field" data-field="sensitivity">
          <span>Mức độ mật</span>
          <SegmentedControl
            value={values.sensitivity}
            options={SENSITIVITY_OPTIONS}
            onChange={(v) => set('sensitivity', v)}
            label="Mức độ mật"
            disabled={reschedule}
          />
          {errorOf('sensitivity', reschedule ? LOCKED_HINT : undefined)}
        </div>

        {/* The place and the link follow the mode: in person needs a room, online a link, hybrid both. */}
        {values.mode !== 'Online' && (
          <label className={values.mode === 'InPerson' ? 'field span-2' : 'field'} data-field="location">
            <span>{values.mode === 'Hybrid' ? 'Địa điểm họp trực tiếp' : 'Địa điểm'}</span>
            <input
              name="location"
              value={values.location}
              onChange={(e) => set('location', e.target.value)}
              maxLength={LOCATION_MAX}
              placeholder="Ví dụ: Phòng họp A — Tầng 4"
              aria-invalid={!!fieldErrors.location}
            />
            {errorOf('location', 'Phòng họp hoặc địa chỉ.')}
          </label>
        )}

        {values.mode !== 'InPerson' && (
          <label className={values.mode === 'Online' ? 'field span-2' : 'field'} data-field="onlineLink">
            <span>Link họp trực tuyến</span>
            <input
              name="onlineLink"
              value={values.onlineLink}
              onChange={(e) => set('onlineLink', e.target.value)}
              placeholder="Để trống: phòng họp TrustCabinet"
              aria-invalid={!!fieldErrors.onlineLink}
            />
            {errorOf('onlineLink', 'Không bắt buộc, tối đa 1.000 ký tự.')}
          </label>
        )}

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
