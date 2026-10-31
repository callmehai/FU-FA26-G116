import { useState } from 'react'
import { confirmDocumentRead } from '../../api/votes'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'

/** Where the reader is: still reading, at the last page, or looking at a file the viewer could not draw. */
export type ReadProgress = 'reading' | 'reached' | 'unreadable'

/**
 * UC-74 in the document viewer, for a vote document opened from a vote the caller has still to cast
 * (`?read=1`). It stays locked until the reader reaches the last page (BR-290) — the label says so,
 * since a touch screen shows no tooltip — then records the confirmation once. A file the viewer cannot
 * draw can never be read to its end, and the vote's documents no longer change once it is open (BR-288),
 * so the label says whom to tell instead of leaving the reader at a locked button. The vote's panel sees
 * a confirmation when its window regains focus.
 */
export function ReadConfirmButton({
  meetingId,
  meetingDocumentId,
  progress,
}: {
  meetingId: string
  meetingDocumentId: string
  progress: ReadProgress
}) {
  const toast = useToast()
  const [phase, setPhase] = useState<'idle' | 'sending' | 'done'>('idle')

  if (phase === 'done') {
    return (
      <span className="viewer-read-done" role="status">
        <Icon name="checkCircle" size={16} />
        <span className="viewer-download-label">Đã xác nhận đọc</span>
      </span>
    )
  }
  if (progress === 'unreadable') {
    return (
      <button type="button" className="btn btn-outline" disabled>
        <Icon name="alertCircle" size={16} />
        Không mở được, báo Thư ký
      </button>
    )
  }
  return (
    <button
      type="button"
      className="btn btn-primary"
      disabled={progress !== 'reached' || phase === 'sending'}
      onClick={async () => {
        setPhase('sending')
        try {
          toast.show((await confirmDocumentRead(meetingId, meetingDocumentId)).message)
          setPhase('done')
        } catch (err) {
          // MSG224 / MSG225: no longer waiting on this reader's ballot; MSG156: the document is gone.
          toast.show(errorMessage(err), 'error')
          setPhase('idle')
        }
      }}
    >
      <Icon name="check" size={16} />
      {phase === 'sending' ? 'Đang xác nhận…' : progress === 'reached' ? 'Xác nhận đã đọc' : 'Đọc tới trang cuối'}
    </button>
  )
}
