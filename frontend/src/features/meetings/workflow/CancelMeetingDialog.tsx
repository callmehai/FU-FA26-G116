import { useState } from 'react'
import { transitionMeeting } from '../../../api/meetings'
import { Dialog } from '../../../components/Dialog'
import { Icon } from '../../../components/Icon'
import { errorMessage, isApiError } from '../../../lib/errors'
import type { Meeting } from '../../../types/meeting'
import { workflowFailure, type WorkflowOutcome } from './workflowOutcome'

/** The backend's limit, counted after trimming. */
const REASON_MAX = 1000

/**
 * UC-75 Cancel Meeting: the organizer says why, before the meeting starts. Who hears about it depends on
 * whether it was announced, so the dialog says which. What was typed stays when the request fails.
 */
export function CancelMeetingDialog({
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

  async function cancelMeeting() {
    const trimmed = reason.trim()
    if (!trimmed) {
      setReasonError('Cần nhập lý do huỷ cuộc họp.')
      return
    }
    setPending(true)
    setError(null)
    try {
      const response = await transitionMeeting(meeting.id, { action: 'Cancel', reason: trimmed })
      onDone({ kind: 'done', response })
    } catch (err) {
      setPending(false)
      if (isApiError(err, 400, 'MSG262') || isApiError(err, 400, 'MSG345')) {
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
      title="Huỷ cuộc họp?"
      description={meeting.title}
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-outline" onClick={close} disabled={pending}>
            Giữ cuộc họp
          </button>
          <button type="button" className="btn btn-danger" onClick={cancelMeeting} disabled={pending}>
            <Icon name="calendarX" size={16} />
            {pending ? 'Đang huỷ…' : 'Huỷ cuộc họp'}
          </button>
        </>
      }
    >
      <label className="field">
        <span className="field-required">Lý do huỷ</span>
        <textarea
          rows={4}
          value={reason}
          onChange={(e) => {
            setReason(e.target.value)
            setReasonError(null)
          }}
          maxLength={REASON_MAX}
          placeholder="Ví dụ: Lãnh đạo đi công tác, sẽ xếp lịch lại sau."
          aria-invalid={!!reasonError}
          autoFocus
        />
        <span className="field-foot">
          {reasonError && <small className="field-error">{reasonError}</small>}
          <small className="field-counter">
            {reason.length.toLocaleString('vi-VN')}/{REASON_MAX.toLocaleString('vi-VN')}
          </small>
        </span>
      </label>

      <p className="dialog-text dialog-note">
        {meeting.status === 'Published'
          ? 'Mọi người được mời sẽ nhận thông báo và email kèm lý do. Không hoàn tác được.'
          : 'Cuộc họp chưa công bố nên không ai nhận thông báo. Không hoàn tác được.'}
      </p>

      {error && (
        <p className="alert alert-error dialog-error" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  )
}
