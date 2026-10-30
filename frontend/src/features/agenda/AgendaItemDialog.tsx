import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { createAgendaItem, getAgendaEditorOptions, updateAgendaItem } from '../../api/agenda'
import { ApiError } from '../../api/client'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { Select, type SelectOption } from '../../components/Select'
import { errorMessage } from '../../lib/errors'
import type { AgendaEditorOptions, AgendaItem, AgendaItemInput, AgendaMutationResponse } from '../../types/agenda'

const TITLE_MAX = 300
const DESCRIPTION_MAX = 4000

/** One Select value for both kinds of presenter: `u:<user id>`, `g:<invitation id>`, or none. */
type PresenterValue = '' | `u:${string}` | `g:${string}`

function presenterOf(item: AgendaItem | null): PresenterValue {
  if (item?.presenterId) return `u:${item.presenterId}`
  if (item?.presenterGuestInvitationId) return `g:${item.presenterGuestInvitationId}`
  return ''
}

interface Values {
  title: string
  description: string
  presenter: PresenterValue
}

/**
 * UC-23a / UC-23b: add an agenda item or edit one, in any status (SCRUM-98). The presenter is one of
 * the meeting's current people or guests. Documents are not picked here: the row's paperclip links
 * them (UC-33b), and leaving `documentIds` out keeps the item's links. The server re-checks all of it
 * and answers field errors under MSG66.
 */
export function AgendaItemDialog({
  meetingId,
  item,
  onSaved,
  onStale,
  onClose,
}: {
  meetingId: string
  /** null adds a new item at the end. */
  item: AgendaItem | null
  /** Saved: the panel says so and re-reads. */
  onSaved: (response: AgendaMutationResponse) => void
  /** Refused for a reason other than a field (meeting ended, item deleted): the panel re-reads behind. */
  onStale: () => void
  onClose: () => void
}) {
  const initial: Values = {
    title: item?.title ?? '',
    description: item?.description ?? '',
    presenter: presenterOf(item),
  }
  const [values, setValues] = useState(initial)
  const [options, setOptions] = useState<AgendaEditorOptions | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)

  const loadOptions = useCallback(() => {
    setLoadError(null)
    getAgendaEditorOptions(meetingId).then(setOptions, (err) => setLoadError(errorMessage(err)))
  }, [meetingId])
  useEffect(() => {
    void Promise.resolve().then(loadOptions)
  }, [loadOptions])

  const dirty =
    values.title !== initial.title ||
    values.description !== initial.description ||
    values.presenter !== initial.presenter
  const close = () => {
    if (pending) return
    if (dirty) setConfirmDiscard(true)
    else onClose()
  }

  const set = <K extends keyof Values>(key: K, value: Values[K]) => {
    setValues((v) => ({ ...v, [key]: value }))
    setFieldErrors((errors) => {
      const rest = { ...errors }
      delete rest[key === 'presenter' ? 'presenterId' : key]
      return rest
    })
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    const title = values.title.trim()
    if (!title) {
      setFieldErrors({ title: 'Vui lòng nhập nội dung cần bàn.' })
      return
    }
    const input: AgendaItemInput = {
      title,
      description: values.description,
      presenterId: values.presenter.startsWith('u:') ? values.presenter.slice(2) : null,
      presenterGuestInvitationId: values.presenter.startsWith('g:') ? values.presenter.slice(2) : null,
    }
    setPending(true)
    setFormError(null)
    try {
      onSaved(await (item ? updateAgendaItem(meetingId, item.id, input) : createAgendaItem(meetingId, input)))
      onClose()
    } catch (err) {
      setPending(false)
      // A field the server rejected is shown under that field; anything else (the meeting ended, the
      // item was deleted, the role was taken away) above the buttons, while the panel re-reads.
      const errors = err instanceof ApiError && err.status === 400 ? err.body?.errors : undefined
      if (errors && Object.keys(errors).length > 0) {
        setFieldErrors(
          Object.fromEntries(Object.entries(errors).map(([k, v]) => [k, Array.isArray(v) ? v.join(' ') : String(v)])),
        )
      } else {
        setFormError(errorMessage(err))
        onStale()
      }
    }
  }

  const presenterOptions: SelectOption<PresenterValue>[] = [
    { value: '', label: 'Chưa phân công' },
    ...(options?.presenters ?? []).map((p) => ({
      value: (p.kind === 'Guest' ? `g:${p.id}` : `u:${p.id}`) as PresenterValue,
      label: p.kind === 'Guest' ? `${p.fullName} (khách)` : p.fullName,
    })),
  ]

  return (
    <Dialog
      title={item ? 'Sửa nội dung' : 'Thêm nội dung'}
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-outline" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button type="submit" form="agenda-item" className="btn btn-primary" disabled={pending || !options}>
            {pending ? 'Đang lưu…' : 'Lưu'}
          </button>
        </>
      }
    >
      <form id="agenda-item" className="dialog-form" onSubmit={submit} noValidate>
        <label className="field">
          <span className="field-required">Nội dung cần bàn</span>
          <input
            value={values.title}
            onChange={(e) => set('title', e.target.value)}
            maxLength={TITLE_MAX}
            placeholder="VD: Dự toán ngân sách năm 2027"
            aria-invalid={!!fieldErrors.title}
          />
          {fieldErrors.title && <small className="field-error">{fieldErrors.title}</small>}
        </label>

        <div className="field">
          <span id="agenda-presenter">Người trình bày</span>
          <Select
            label="Người trình bày"
            value={values.presenter}
            options={presenterOptions}
            onChange={(v) => set('presenter', v)}
            disabled={!options}
          />
          {loadError ? (
            <small className="field-error">
              {loadError}{' '}
              <button type="button" className="btn btn-outline btn-sm" onClick={loadOptions}>
                <Icon name="refresh" size={16} />
                Thử lại
              </button>
            </small>
          ) : fieldErrors.presenterId || fieldErrors.presenterGuestInvitationId ? (
            <small className="field-error">{fieldErrors.presenterId ?? fieldErrors.presenterGuestInvitationId}</small>
          ) : item?.legacyPresenterName && !values.presenter ? (
            <small className="field-hint">
              Trước đây: {item.legacyPresenterName}. Chọn lại người trình bày nếu cần.
            </small>
          ) : (
            <small className="field-hint">Không bắt buộc, có thể chọn sau.</small>
          )}
        </div>

        <label className="field">
          <span>Ghi chú</span>
          <textarea
            rows={3}
            value={values.description}
            onChange={(e) => set('description', e.target.value)}
            maxLength={DESCRIPTION_MAX}
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
          <p className="alert alert-error dialog-error" role="alert">
            {formError}
          </p>
        )}
      </form>

      {confirmDiscard && (
        <ConfirmDialog
          title="Bỏ thay đổi chưa lưu?"
          tone="danger"
          confirmLabel="Bỏ thay đổi"
          cancelLabel="Tiếp tục sửa"
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
