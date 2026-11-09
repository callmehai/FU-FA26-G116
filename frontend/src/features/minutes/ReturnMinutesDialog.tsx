import { useState } from 'react'
import { returnMinutes } from '../../api/summary'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { errorMessage, isApiError } from '../../lib/errors'
import type { MinutesState } from '../../types/summary'

/** The backend's limit (MinutesLimits.ReturnReason), counted after trimming. */
const REASON_MAX = 2000

/**
 * UC-65: the Chair sends the minutes waiting for approval back to the Secretary, saying what to fix. What was typed stays
 * in the dialog when the request fails — a lost reason would have to be written again. A conflict (someone acted on the
 * minutes meanwhile) closes it and lets the card reload.
 */
export function ReturnMinutesDialog({
  minutesId,
  updatedAt,
  onDone,
  onConflict,
  onClose,
}: {
  minutesId: string
  updatedAt: string
  onDone: (result: MinutesState & { message: string }) => void
  onConflict: (message: string) => void
  onClose: () => void
}) {
  const [reason, setReason] = useState('')
  const [reasonError, setReasonError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function submit() {
    const trimmed = reason.trim()
    if (!trimmed) {
      setReasonError('Nhập lý do trả lại, tối đa 2.000 ký tự.')
      return
    }
    setPending(true)
    setError(null)
    try {
      onDone(await returnMinutes(minutesId, trimmed, updatedAt))
    } catch (err) {
      setPending(false)
      if (isApiError(err, 400, 'MSG431')) setReasonError(errorMessage(err))
      else if (isApiError(err, 409) || isApiError(err, 404)) onConflict(errorMessage(err))
      else setError(errorMessage(err))
    }
  }

  const close = () => {
    if (!pending) onClose()
  }

  return (
    <Dialog
      title="Trả lại biên bản"
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-outline" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button type="button" className="btn btn-primary" onClick={() => void submit()} disabled={pending}>
            <Icon name="reply" size={16} />
            {pending ? 'Đang trả lại…' : 'Trả lại'}
          </button>
        </>
      }
    >
      <label className="field">
        <span className="field-required">Lý do trả lại</span>
        <textarea
          rows={5}
          value={reason}
          onChange={(e) => {
            setReason(e.target.value)
            setReasonError(null)
          }}
          maxLength={REASON_MAX}
          placeholder="Ví dụ: Bổ sung kết luận của mục 2."
          aria-invalid={!!reasonError}
          autoFocus
        />
        <span className="field-foot">
          {reasonError ? (
            <small className="field-error">{reasonError}</small>
          ) : (
            <small className="field-hint">Thư ký sẽ thấy lý do này.</small>
          )}
          <small className="field-counter">
            {reason.length.toLocaleString('vi-VN')}/{REASON_MAX.toLocaleString('vi-VN')}
          </small>
        </span>
      </label>

      {error && (
        <p className="alert alert-error dialog-error" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  )
}
