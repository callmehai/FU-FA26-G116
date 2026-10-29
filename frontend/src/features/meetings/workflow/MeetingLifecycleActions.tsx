import { useState } from 'react'
import { transitionMeeting } from '../../../api/meetings'
import { ConfirmDialog } from '../../../components/ConfirmDialog'
import { Icon } from '../../../components/Icon'
import { useToast } from '../../../components/toastContext'
import { errorMessage } from '../../../lib/errors'
import { formatDateTime } from '../../../lib/format'
import type { Meeting } from '../../../types/meeting'
import { lifecycleAction } from './lifecycle'
import { workflowFailure, type WorkflowOutcome } from './workflowOutcome'

/** Starting this far ahead of the schedule is asked about; a few minutes early is the room filling. */
const EARLY_START_MS = 15 * 60_000

/**
 * The Chair's "Bắt đầu họp" / "Kết thúc họp" buttons, for the page header next to "Vào phòng họp".
 * Starting on time is one click; starting well before the schedule, or without a Secretary (which
 * the server refuses), asks first. Ending always asks: it closes the room and cannot be undone.
 */
export function MeetingLifecycleActions({
  meeting,
  onOutcome,
}: {
  meeting: Meeting
  onOutcome: (outcome: WorkflowOutcome) => void
}) {
  const toast = useToast()
  const [dialog, setDialog] = useState<'start' | 'end' | null>(null)
  const [starting, setStarting] = useState(false)
  const action = lifecycleAction(meeting)
  if (!action) return null

  /** Runs the transition; a failure that ends the action goes to the page, the rest is thrown. */
  async function run(which: 'Start' | 'End') {
    try {
      const response = await transitionMeeting(meeting.id, { action: which })
      onOutcome({ kind: 'done', response })
    } catch (err) {
      const failure = workflowFailure(err)
      if (!failure) throw err
      onOutcome(failure)
    }
  }

  const noSecretary = meeting.secretaries.length === 0

  async function start() {
    const early = Date.parse(meeting.plannedStartTime) - Date.now() > EARLY_START_MS
    if (noSecretary || early) {
      setDialog('start')
      return
    }
    setStarting(true)
    try {
      await run('Start')
    } catch (err) {
      // MSG115: nothing was saved, so the button is there to try again.
      toast.show(errorMessage(err), 'error')
    } finally {
      setStarting(false)
    }
  }

  return (
    <>
      {action === 'Start' ? (
        <button type="button" className="btn btn-live btn-lg btn-cta btn-ping" onClick={start} disabled={starting}>
          <Icon name="play" size={18} />
          {starting ? 'Đang bắt đầu…' : 'Bắt đầu họp'}
        </button>
      ) : (
        <button type="button" className="btn btn-outline btn-lg" onClick={() => setDialog('end')}>
          <Icon name="stop" size={16} />
          Kết thúc họp
        </button>
      )}

      {dialog === 'start' &&
        (noSecretary ? (
          <ConfirmDialog
            title="Chưa thể bắt đầu"
            confirmLabel="Bắt đầu họp"
            pendingLabel="Đang bắt đầu…"
            cancelLabel="Đóng"
            blocker="Thêm Thư ký ở tab Thành phần tham dự rồi bắt đầu lại."
            onConfirm={() => run('Start')}
            onClose={() => setDialog(null)}
          >
            Cuộc họp cần ít nhất một Thư ký.
          </ConfirmDialog>
        ) : (
          <ConfirmDialog
            title="Bắt đầu sớm hơn lịch?"
            confirmLabel="Vẫn bắt đầu"
            pendingLabel="Đang bắt đầu…"
            onConfirm={() => run('Start')}
            onClose={() => setDialog(null)}
          >
            “{meeting.title}” được xếp lịch lúc {formatDateTime(meeting.plannedStartTime)}.
          </ConfirmDialog>
        ))}
      {dialog === 'end' && (
        <ConfirmDialog
          title="Kết thúc cuộc họp?"
          confirmLabel="Kết thúc họp"
          pendingLabel="Đang kết thúc…"
          tone="danger"
          onConfirm={() => run('End')}
          onClose={() => setDialog(null)}
        >
          {meeting.mode === 'InPerson'
            ? `“${meeting.title}” sẽ kết thúc và không mở lại được.`
            : `“${meeting.title}” sẽ kết thúc và không mở lại được. Phòng họp trực tuyến đóng, mọi người bị ngắt kết nối.`}
        </ConfirmDialog>
      )}
    </>
  )
}
