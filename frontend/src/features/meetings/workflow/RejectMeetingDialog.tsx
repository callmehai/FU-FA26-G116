import { useState } from 'react'
import { transitionMeeting } from '../../../api/meetings'
import { Dialog } from '../../../components/Dialog'
import { errorMessage, isApiError } from '../../../lib/errors'
import type { Meeting } from '../../../types/meeting'
import { workflowFailure, type WorkflowOutcome } from './workflowOutcome'

/** The backend's limit, counted after trimming. */
const REASON_MAX = 1000

/**
 * UC-19 Reject: the approver says why, so the organizer knows what to change. What was typed stays
 * in the dialog when the request fails — a lost reason would have to be written again.
 */
export function RejectMeetingDialog({
  meeting,
  onDone,
  onClose,
}: {
  meeting: Meeting
  onDone: (outcome: WorkflowOutcome) => void
  onClose: () => void
}) {
  const [reason, setReason] = useState('')
  const [reasonError, setReasonError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function reject() {
    const trimmed = reason.trim()
    if (!trimmed) {
      setReasonError('Cần nhập lý do từ chối.')
      return
    }
    setPending(true)
    setError(null)
    try {
      const response = await transitionMeeting(meeting.id, { action: 'Reject', reason: trimmed })
      onDone({ kind: 'done', response })
    } catch (err) {
      setPending(false)
      if (isApiError(err, 400, 'MSG83') || isApiError(err, 400, 'MSG84')) {
        setReasonError(errorMessage(err))
        return
      }
      const failure = workflowFailure(err)
      if (failure) onDone(failure)
      else setError(errorMessage(err))
    }
  }

  const close = () => {
    if (!pending) onClose()
  }

  return (
    <Dialog
      title="Từ chối cuộc họp"
      description={meeting.title}
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-ghost" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button type="button" className="btn btn-danger" onClick={reject} disabled={pending}>
            {pending ? 'Đang từ chối…' : 'Từ chối'}
          </button>
        </>
      }
    >
      <label className="field">
        <span className="field-required">Lý do từ chối</span>
        <textarea
          rows={5}
          value={reason}
          onChange={(e) => {
            setReason(e.target.value)
            setReasonError(null)
          }}
          maxLength={REASON_MAX}
          placeholder="Ví dụ: Cần bổ sung báo cáo tài chính vào nội dung cuộc họp."
          aria-invalid={!!reasonError}
        />
        <span className="field-foot">
          {reasonError ? (
            <small className="field-error">{reasonError}</small>
          ) : (
            <small className="field-hint">Người tổ chức sẽ thấy lý do này.</small>
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
