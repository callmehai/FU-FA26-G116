import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { createVote, getVote, updateVote } from '../../api/votes'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { SegmentedControl } from '../../components/SegmentedControl'
import { errorMessage } from '../../lib/errors'
import { VOTE_ROLE_LABEL, VOTE_ROLES, type VoteInput, type VoteMutationResponse, type VoteRole } from '../../types/vote'

const TITLE_MAX = 300
const DESCRIPTION_MAX = 4000
const OPTION_MAX = 200
const DEFAULT_OPTIONS = ['Đồng ý', 'Không đồng ý', 'Không ý kiến']

interface Values {
  title: string
  description: string
  options: string[]
  secret: 'public' | 'secret'
  roles: VoteRole[]
}

const EMPTY: Values = { title: '', description: '', options: DEFAULT_OPTIONS, secret: 'public', roles: [...VOTE_ROLES] }

const same = (a: Values, b: Values) =>
  a.title === b.title &&
  a.description === b.description &&
  a.secret === b.secret &&
  a.options.join('\n') === b.options.join('\n') &&
  [...a.roles].sort().join() === [...b.roles].sort().join()

/** The rules the server checks under MSG209, with its own words, so most mistakes never leave the form. */
function validate(values: Values): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!values.title.trim()) errors.title = 'Nhập nội dung biểu quyết.'
  const options = values.options.map((o) => o.trim())
  const distinct = new Set(options.map((o) => o.toLocaleLowerCase('vi')))
  if (options.length < 2 || options.some((o) => !o) || distinct.size !== options.length)
    errors.options = 'Cần ít nhất 2 phương án khác nhau, không để trống.'
  if (values.roles.length === 0) errors.eligibleRoles = 'Chọn ít nhất một vai trò.'
  return errors
}

/**
 * UC-53 / UC-53 AF-1 / UC-54 AF-1: create a vote or edit a draft, then save it as a draft or — the
 * Chair, while the meeting runs — open it at once. Opening from here is the confirmation: no second
 * dialog. A failed open saves nothing (no new draft, the old draft unchanged) and keeps what was typed.
 */
export function VoteDialog({
  meetingId,
  voteId,
  canOpen,
  onSaved,
  onStale,
  onClose,
}: {
  meetingId: string
  /** null creates a new vote. */
  voteId: string | null
  /** The Chair while the meeting is in progress (`permissions.canOpen`). */
  canOpen: boolean
  onSaved: (response: VoteMutationResponse) => void
  /** Refused for a reason other than a field: the panel re-reads behind the dialog. */
  onStale: () => void
  onClose: () => void
}) {
  const [initial, setInitial] = useState<Values | null>(voteId ? null : EMPTY)
  const [values, setValues] = useState<Values>(EMPTY)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState<'draft' | 'open' | null>(null)
  const [confirmDiscard, setConfirmDiscard] = useState(false)

  const load = useCallback(() => {
    if (!voteId) return
    setLoadError(null)
    getVote(meetingId, voteId).then(
      (vote) => {
        const loaded: Values = {
          title: vote.title,
          description: vote.description,
          options: vote.options.map((o) => o.label),
          secret: vote.isAnonymous ? 'secret' : 'public',
          roles: vote.eligibleRoles,
        }
        setInitial(loaded)
        setValues(loaded)
      },
      (err) => setLoadError(errorMessage(err)),
    )
  }, [meetingId, voteId])
  useEffect(() => {
    void Promise.resolve().then(load)
  }, [load])

  const dirty = initial !== null && !same(values, initial)
  const close = () => {
    if (pending) return
    if (dirty) setConfirmDiscard(true)
    else onClose()
  }

  const set = <K extends keyof Values>(key: K, value: Values[K]) => {
    setValues((v) => ({ ...v, [key]: value }))
    const field = key === 'roles' ? 'eligibleRoles' : key === 'secret' ? 'isAnonymous' : key
    setFieldErrors((errors) => {
      const rest = { ...errors }
      delete rest[field]
      return rest
    })
  }
  const setOption = (index: number, label: string) =>
    set(
      'options',
      values.options.map((o, i) => (i === index ? label : o)),
    )
  const toggleRole = (role: VoteRole) =>
    set('roles', values.roles.includes(role) ? values.roles.filter((r) => r !== role) : [...values.roles, role])

  async function save(open: boolean) {
    const errors = validate(values)
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      document.querySelector<HTMLElement>('#vote-form [aria-invalid="true"]')?.focus()
      return
    }
    const input: VoteInput = {
      title: values.title.trim(),
      description: values.description,
      options: values.options.map((o) => o.trim()),
      isAnonymous: values.secret === 'secret',
      eligibleRoles: VOTE_ROLES.filter((r) => values.roles.includes(r)),
      openImmediately: open,
    }
    setPending(open ? 'open' : 'draft')
    setFormError(null)
    try {
      onSaved(await (voteId ? updateVote(meetingId, voteId, input) : createVote(meetingId, input)))
      onClose()
    } catch (err) {
      setPending(null)
      const errors = err instanceof ApiError && err.status === 400 ? err.body?.errors : undefined
      if (errors && Object.keys(errors).length > 0) {
        setFieldErrors(
          Object.fromEntries(Object.entries(errors).map(([k, v]) => [k, Array.isArray(v) ? v.join(' ') : String(v)])),
        )
      } else {
        // MSG218–221 (cannot open now, another vote is open, nobody eligible) and MSG213 keep the form.
        setFormError(errorMessage(err))
        onStale()
      }
    }
  }

  // Enter in a field only ever saves a draft: opening fixes who may vote and cannot be undone, so it takes
  // a click on "Mở biểu quyết" itself.
  const submit = (event: FormEvent) => {
    event.preventDefault()
    void save(false)
  }

  return (
    <Dialog
      title={voteId ? 'Sửa biểu quyết' : 'Tạo biểu quyết'}
      size="lg"
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-ghost" onClick={close} disabled={pending !== null}>
            Huỷ
          </button>
          <button
            type="submit"
            form="vote-form"
            className={canOpen ? 'btn btn-outline' : 'btn btn-primary'}
            disabled={pending !== null || !initial}
          >
            {pending === 'draft' ? 'Đang lưu…' : 'Lưu nháp'}
          </button>
          {canOpen && (
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => void save(true)}
              disabled={pending !== null || !initial}
            >
              {pending === 'open' ? 'Đang mở…' : 'Mở biểu quyết'}
            </button>
          )}
        </>
      }
    >
      {loadError ? (
        <div className="alert alert-error" role="alert">
          <p>{loadError}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={load}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      ) : (
        <form id="vote-form" className="dialog-form" onSubmit={submit} noValidate aria-busy={!initial}>
          <label className="field">
            <span className="field-required">Nội dung biểu quyết</span>
            <input
              value={values.title}
              onChange={(e) => set('title', e.target.value)}
              maxLength={TITLE_MAX}
              placeholder="VD: Thông qua kế hoạch quý IV"
              aria-invalid={!!fieldErrors.title}
              disabled={!initial}
            />
            {fieldErrors.title && <small className="field-error">{fieldErrors.title}</small>}
          </label>

          <fieldset className="field vote-options-field">
            <legend className="field-required">Phương án</legend>
            <ol className="vote-option-inputs">
              {values.options.map((option, index) => (
                <li key={index}>
                  <input
                    value={option}
                    onChange={(e) => setOption(index, e.target.value)}
                    maxLength={OPTION_MAX}
                    aria-label={`Phương án ${index + 1}`}
                    aria-invalid={!!fieldErrors.options && !option.trim()}
                    disabled={!initial}
                  />
                  {values.options.length > 2 && (
                    <button
                      type="button"
                      className="icon-button icon-button-sm"
                      onClick={() =>
                        set(
                          'options',
                          values.options.filter((_, i) => i !== index),
                        )
                      }
                      aria-label={`Bỏ phương án ${index + 1}`}
                      data-tooltip="Bỏ phương án"
                    >
                      <Icon name="x" size={16} />
                    </button>
                  )}
                </li>
              ))}
            </ol>
            <button
              type="button"
              className="btn btn-ghost btn-sm vote-option-add"
              onClick={() => set('options', [...values.options, ''])}
              disabled={!initial}
            >
              <Icon name="plus" size={16} />
              Thêm phương án
            </button>
            {fieldErrors.options && <small className="field-error">{fieldErrors.options}</small>}
          </fieldset>

          <div className="field">
            <span>Hình thức</span>
            <SegmentedControl
              label="Hình thức"
              value={values.secret}
              options={[
                { value: 'public', label: 'Công khai' },
                { value: 'secret', label: 'Bỏ phiếu kín' },
              ]}
              onChange={(v) => set('secret', v)}
              disabled={!initial}
            />
            {fieldErrors.isAnonymous ? (
              <small className="field-error">{fieldErrors.isAnonymous}</small>
            ) : (
              <small className="field-hint">
                {values.secret === 'secret'
                  ? 'Chỉ hiện tổng số phiếu, không ai biết ai chọn gì.'
                  : 'Sau khi đóng, mọi người thấy ai chọn gì.'}
              </small>
            )}
          </div>

          <fieldset className="field">
            <legend className="field-required">Người được bỏ phiếu</legend>
            <div className="vote-role-picks">
              {VOTE_ROLES.map((role) => (
                <label key={role} className="vote-role-pick">
                  <input
                    type="checkbox"
                    checked={values.roles.includes(role)}
                    onChange={() => toggleRole(role)}
                    disabled={!initial}
                  />
                  {VOTE_ROLE_LABEL[role]}
                </label>
              ))}
            </div>
            {fieldErrors.eligibleRoles ? (
              <small className="field-error">{fieldErrors.eligibleRoles}</small>
            ) : (
              <small className="field-hint">Danh sách người bỏ phiếu chốt lúc mở.</small>
            )}
          </fieldset>

          <label className="field">
            <span>Mô tả</span>
            <textarea
              rows={3}
              value={values.description}
              onChange={(e) => set('description', e.target.value)}
              maxLength={DESCRIPTION_MAX}
              aria-invalid={!!fieldErrors.description}
              disabled={!initial}
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
      )}

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
