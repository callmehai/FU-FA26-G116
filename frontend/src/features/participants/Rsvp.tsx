import { useState } from 'react'
import { respondToInvitation } from '../../api/participants'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { InvitationResponse } from '../../types/participant'
import { useAccessLost } from '../auth/useAccessLost'

/**
 * "Bạn có tham dự cuộc họp này?", as AI-eCabinet asks it: two buttons, shown only while the server
 * says the caller may answer (`canRespond`). The answer is final and changes no access; declining
 * only takes the meeting off the caller's calendar. So there is no confirmation step.
 *
 * `onAnswered` runs after every outcome that changed what the server holds — an answer recorded,
 * or a refusal saying the question no longer applies — so the caller re-reads the meeting.
 */
export function RsvpButtons({
  meetingId,
  onAnswered,
  labelledBy,
  ping = false,
}: {
  meetingId: string
  onAnswered: (response: Exclude<InvitationResponse, 'Pending'>) => void
  labelledBy?: string
  /** The one question on screen (detail page, quick look) ripples; a list of cards does not. */
  ping?: boolean
}) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [pending, setPending] = useState<'Accepted' | 'Declined' | null>(null)

  async function send(response: 'Accepted' | 'Declined') {
    setPending(response)
    try {
      const result = await respondToInvitation(meetingId, response)
      toast.show(
        response === 'Declined' ? `${result.message} Cuộc họp không còn hiện trên lịch của bạn.` : result.message,
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
        className={`btn btn-accept btn-sm btn-cta${ping ? ' btn-ping' : ''}`}
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
        onClick={() => send('Declined')}
      >
        <Icon name="x" size={16} />
        {pending === 'Declined' ? 'Đang gửi…' : 'Không tham dự'}
      </button>
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
