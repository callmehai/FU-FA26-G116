import { useState } from 'react'
import { respondToInvitation } from '../../api/participants'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { InvitationResponse } from '../../types/participant'
import { useAccessLost } from '../auth/useAccessLost'
import { useGuest } from '../guest/guestContext'

/**
 * "Bạn có tham dự cuộc họp này?", as AI-eCabinet asks it: two buttons, shown only while the server
 * says the caller may answer (`canRespond`). The answer is final (MSG285) and changes no access. Declining
 * also takes the meeting off the caller's calendar (a guest has none), so it asks first (SCRUM-147);
 * accepting, the common answer, goes out at once.
 *
 * `onAnswered` runs after every outcome that changed what the server holds — an answer recorded,
 * or a refusal saying the question no longer applies — so the caller re-reads the meeting.
 */
export function RsvpButtons({
  meetingId,
  onAnswered,
  labelledBy,
}: {
  meetingId: string
  onAnswered: (response: Exclude<InvitationResponse, 'Pending'>) => void
  labelledBy?: string
}) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const guest = useGuest() !== null
  const [pending, setPending] = useState<'Accepted' | 'Declined' | null>(null)
  const [confirmDecline, setConfirmDecline] = useState(false)

  async function send(response: 'Accepted' | 'Declined') {
    setPending(response)
    try {
      const result = await respondToInvitation(meetingId, response)
      toast.show(
        response === 'Declined' && !guest
          ? `${result.message} Cuộc họp không còn hiện trên lịch của bạn.`
          : result.message,
      )
      onAnswered(response)
    } catch (err) {
      setPending(null)
      if (handleAccessLost(err)) return
      toast.show(errorMessage(err), 'error')
      // Answered elsewhere (MSG285), now the Chair (MSG286), or the meeting moved on (MSG107):
      // the question no longer applies, so show what is true now.
      if (isApiError(err, 409) || isApiError(err, 404)) onAnswered(response)
    }
  }

  return (
    <div className="rsvp-buttons" role="group" aria-labelledby={labelledBy}>
      <button
        type="button"
        className="btn btn-accept btn-sm btn-cta"
        disabled={pending !== null}
        onClick={() => send('Accepted')}
      >
        <Icon name="check" size={16} />
        {pending === 'Accepted' ? 'Đang gửi…' : 'Tham dự'}
      </button>
      <button
        type="button"
        className="btn btn-outline btn-sm"
        disabled={pending !== null}
        onClick={() => setConfirmDecline(true)}
      >
        <Icon name="x" size={16} />
        {pending === 'Declined' ? 'Đang gửi…' : 'Không tham dự'}
      </button>
      {confirmDecline && (
        <ConfirmDialog
          title="Không tham dự cuộc họp?"
          confirmLabel="Không tham dự"
          cancelLabel="Quay lại"
          pendingLabel="Đang gửi…"
          // send() reports its own outcome as a toast, so the dialog just closes after it.
          onConfirm={() => send('Declined')}
          onClose={() => setConfirmDecline(false)}
        >
          {guest
            ? 'Bạn chỉ trả lời được một lần, sau đó không đổi được.'
            : 'Bạn chỉ trả lời được một lần, sau đó không đổi được. Cuộc họp sẽ không còn hiện trên lịch của bạn.'}
        </ConfirmDialog>
      )}
    </div>
  )
}

/** The answer given, beside a name in the roster or a meeting's title. Nothing while unanswered. */
export function RsvpChip({ response }: { response: InvitationResponse | null }) {
  if (response !== 'Accepted' && response !== 'Declined') return null
  const declined = response === 'Declined'
  return (
    <span className={`badge rsvp-chip ${declined ? 'badge-slate' : 'badge-emerald'}`}>
      <Icon name={declined ? 'x' : 'check'} size={14} />
      {declined ? 'Không tham dự' : 'Đã xác nhận tham dự'}
    </span>
  )
}
