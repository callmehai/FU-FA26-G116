import { useState, type FormEvent } from 'react'
import { createDraftFromSlot } from '../../api/meetingSlots'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { SegmentedControl } from '../../components/SegmentedControl'
import { errorMessage, isApiError } from '../../lib/errors'
import {
  MEETING_MODES,
  MEETING_MODE_LABEL,
  SENSITIVITIES,
  SENSITIVITY_LABEL,
  type MeetingMode,
  type MeetingMutationResponse,
  type Sensitivity,
} from '../../types/meeting'
import type { Slot } from '../../types/meetingSlots'
import { Person } from '../participants/Person'
import type { Invitee } from './InviteePicker'
import { clockOf, dayLabel } from './slotRules'

/** The backend's limits (MeetingRules), as in MeetingForm. */
const TITLE_MAX = 300
const LOCATION_MAX = 300
const ONLINE_LINK_MAX = 1000
const DESCRIPTION_MAX = 4000

const MODE_OPTIONS = MEETING_MODES.map((value) => ({ value, label: MEETING_MODE_LABEL[value] }))
const SENSITIVITY_OPTIONS = SENSITIVITIES.map((value) => ({ value, label: SENSITIVITY_LABEL[value] }))
const FORM_ID = 'slot-draft-form'

type Field = 'title' | 'location' | 'onlineLink' | 'description' | 'plannedStartTime' | 'userIds'

/**
 * UC-85b — a Draft meeting from a found slot. The time is the slot's and does not change here; the roster follows
 * BR-429 — the first person chairs, the rest are Members, the caller is Secretary — and the arrows pick who chairs.
 * The server rechecks the slot against the same shared data as the search (BR-428): someone who became busy since
 * answers 409 MSG446 and the page searches again.
 */
export function SlotDraftDialog({
  slot,
  day,
  invitees,
  me,
  includeMe,
  bufferMinutes,
  onClose,
  onCreated,
  onTaken,
}: {
  slot: Slot
  day: string
  /** In the order searched; the first one chairs unless reordered. */
  invitees: Invitee[]
  me: Invitee
  includeMe: boolean
  bufferMinutes: number
  onClose: () => void
  onCreated: (response: MeetingMutationResponse) => void
  /** 409 MSG446, with the server's sentence. */
  onTaken: (message: string) => void
}) {
  const [order, setOrder] = useState(invitees)
  const [title, setTitle] = useState('')
  const [mode, setMode] = useState<MeetingMode>('InPerson')
  const [sensitivity, setSensitivity] = useState<Sensitivity>('Normal')
  const [location, setLocation] = useState('')
  const [onlineLink, setOnlineLink] = useState('')
  const [description, setDescription] = useState('')
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const [rowErrors, setRowErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)

  const dirty =
    !!title.trim() || !!location.trim() || !!onlineLink.trim() || !!description.trim() || order[0]?.id !== invitees[0]?.id

  function move(index: number, by: -1 | 1) {
    const next = [...order]
    ;[next[index], next[index + by]] = [next[index + by], next[index]]
    setOrder(next)
    setRowErrors({})
  }

  function cancel() {
    if (pending) return
    if (dirty) setConfirmDiscard(true)
    else onClose()
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    const local: Partial<Record<Field, string>> = {}
    if (!title.trim()) local.title = 'Tên cuộc họp không được để trống.'
    setErrors(local)
    setFormError(null)
    if (Object.keys(local).length > 0) return

    // The acceptance the search showed: whoever was busy or unknown then may still be (BR-428, plan P2).
    const accepted = [...slot.busyUserIds, ...slot.unknownUserIds]
    setPending(true)
    try {
      const response = await createDraftFromSlot({
        title: title.trim(),
        plannedStartTime: slot.start,
        plannedEndTime: slot.end,
        mode,
        location: mode === 'Online' ? '' : location.trim(),
        onlineLink: mode === 'InPerson' ? null : onlineLink.trim() || null,
        sensitivity,
        description,
        userIds: order.map((p) => p.id),
        includeMe,
        bufferMinutes,
        acceptedBusyUserIds: accepted,
      })
      onCreated(response)
    } catch (err) {
      setPending(false)
      if (isApiError(err, 409, 'MSG446')) {
        onTaken(errorMessage(err))
        return
      }
      if (isApiError(err, 400, 'MSG31') || isApiError(err, 400, 'MSG277')) {
        const next: Partial<Record<Field, string>> = {}
        const rows: Record<string, string> = {}
        let other: string | null = null
        for (const [key, message] of Object.entries(err.body?.errors ?? {})) {
          const row = /^userIds\[(\d+)\]$/.exec(key)
          if (row && order[Number(row[1])]) rows[order[Number(row[1])].id] = message
          else if (key === 'title' || key === 'location' || key === 'onlineLink' || key === 'description')
            next[key] = message
          else if (key === 'userIds') next.userIds = message
          // The slot's own time (passed since the search) has no field here: say it in the form's message.
          else other = message
        }
        setErrors(next)
        setRowErrors(rows)
        setFormError(other ?? (Object.keys(next).length || Object.keys(rows).length ? null : errorMessage(err)))
        return
      }
      setFormError(errorMessage(err))
    }
  }

  const error = (field: Field) => errors[field] && <small className="field-error">{errors[field]}</small>

  return (
    <Dialog
      title="Tạo cuộc họp nháp"
      size="xl"
      onClose={cancel}
      footer={
        <>
          <p className="dialog-foot-note">Cuộc họp ở trạng thái Nháp và đi luồng duyệt bình thường.</p>
          <button type="button" className="btn btn-outline" onClick={cancel} disabled={pending}>
            Huỷ
          </button>
          <button type="submit" form={FORM_ID} className="btn btn-primary" disabled={pending}>
            <Icon name="plus" size={16} />
            {pending ? 'Đang tạo…' : 'Tạo nháp'}
          </button>
        </>
      }
    >
      <form id={FORM_ID} className="meeting-form" onSubmit={submit} noValidate>
        <p className="find-locked">
          <Icon name="lock" size={16} />
          <b>
            {dayLabel(day)}/{day.slice(0, 4)}, {clockOf(slot.start)} – {clockOf(slot.end)}
          </b>
          <span className="find-locked-note">Giờ theo khung đã chọn</span>
        </p>

        <div className="meeting-form-cols">
          <div className="meeting-form-main">
            <label className="field" data-field="title">
              <span className="field-label-row">
                <span className="field-required">Tên cuộc họp</span>
                <small className="field-counter">
                  {title.length}/{TITLE_MAX}
                </small>
              </span>
              <input
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                maxLength={TITLE_MAX}
                placeholder="Ví dụ: Giao ban tuần 40"
                aria-invalid={!!errors.title}
                autoFocus
              />
              {error('title')}
            </label>

            <label className="field" data-field="description">
              <span>Mô tả</span>
              <textarea
                rows={3}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                maxLength={DESCRIPTION_MAX}
                placeholder="Mục đích, nội dung chính dự kiến bàn…"
                aria-invalid={!!errors.description}
              />
              {error('description')}
            </label>

            <div className="field" data-field="userIds">
              <span>Thành phần tham dự</span>
              <ol className="roster-list find-roster" aria-label="Thành phần tham dự">
                {order.map((person, index) => (
                  <li key={person.id} className={rowErrors[person.id] ? 'roster-row roster-row-invalid' : 'roster-row'}>
                    <div className="roster-row-main">
                      <Person name={person.fullName} email={person.email} />
                      {rowErrors[person.id] && <small className="field-error">{rowErrors[person.id]}</small>}
                    </div>
                    <span className={index === 0 ? 'find-role find-role-chair' : 'find-role'}>
                      {index === 0 ? 'Chủ toạ' : 'Thành viên'}
                    </span>
                    <span className="find-move">
                      <button
                        type="button"
                        className="icon-button icon-button-sm"
                        aria-label={`Đưa ${person.fullName} lên`}
                        disabled={index === 0}
                        onClick={() => move(index, -1)}
                      >
                        <Icon name="chevronUp" size={16} />
                      </button>
                      <button
                        type="button"
                        className="icon-button icon-button-sm"
                        aria-label={`Đưa ${person.fullName} xuống`}
                        disabled={index === order.length - 1}
                        onClick={() => move(index, 1)}
                      >
                        <Icon name="chevronDown" size={16} />
                      </button>
                    </span>
                  </li>
                ))}
                <li className="roster-row">
                  <div className="roster-row-main">
                    <Person name={`${me.fullName} (Tôi)`} email={me.email} />
                  </div>
                  <span className="find-role">Thư ký</span>
                  <span className="find-move" />
                </li>
              </ol>
              {errors.userIds ? (
                <small className="field-error">{errors.userIds}</small>
              ) : (
                <small className="field-hint">Người đầu danh sách làm Chủ toạ. Bạn là Thư ký.</small>
              )}
            </div>
          </div>

          <div className="meeting-form-side">
            <div className="field" data-field="mode">
              <span>Hình thức họp</span>
              <SegmentedControl value={mode} options={MODE_OPTIONS} onChange={setMode} label="Hình thức họp" />
            </div>
            {mode !== 'Online' && (
              <label className="field" data-field="location">
                <span>{mode === 'Hybrid' ? 'Địa điểm họp trực tiếp' : 'Địa điểm'}</span>
                <input
                  value={location}
                  onChange={(e) => setLocation(e.target.value)}
                  maxLength={LOCATION_MAX}
                  placeholder="Ví dụ: Phòng họp A — Tầng 4"
                  aria-invalid={!!errors.location}
                />
                {error('location')}
              </label>
            )}
            {mode !== 'InPerson' && (
              <label className="field" data-field="onlineLink">
                <span>Link họp trực tuyến</span>
                <input
                  value={onlineLink}
                  onChange={(e) => setOnlineLink(e.target.value)}
                  maxLength={ONLINE_LINK_MAX}
                  placeholder="Để trống: phòng họp TrustCabinet"
                  aria-invalid={!!errors.onlineLink}
                />
                {error('onlineLink')}
              </label>
            )}
            <div className="field" data-field="sensitivity">
              <span className="field-required">Mức độ mật</span>
              <SegmentedControl
                value={sensitivity}
                options={SENSITIVITY_OPTIONS}
                onChange={setSensitivity}
                label="Mức độ mật"
              />
            </div>
          </div>
        </div>

        {formError && (
          <p className="alert alert-error" role="alert">
            {formError}
          </p>
        )}
      </form>

      {confirmDiscard && (
        <ConfirmDialog
          title="Bỏ cuộc họp đang soạn?"
          tone="danger"
          confirmLabel="Bỏ cuộc họp"
          cancelLabel="Tiếp tục soạn"
          pendingLabel="Đang đóng…"
          onConfirm={async () => onClose()}
          onClose={() => setConfirmDiscard(false)}
        >
          Những gì bạn vừa nhập sẽ không được lưu.
        </ConfirmDialog>
      )}
    </Dialog>
  )
}
