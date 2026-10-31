import { useCallback, useEffect, useRef, useState } from 'react'
import { getVotes } from '../../api/votes'
import { Icon } from '../../components/Icon'
import { useMeetingEvents } from '../meetings/useMeetingEvents'

interface Notice {
  voteId: string
  title: string
  /** On the list fixed at opening and not voted yet: the button says "Bỏ phiếu", else "Xem". */
  canCast: boolean
}

/**
 * The call room's floating "Chủ toạ mở biểu quyết: … — Bỏ phiếu" (SCRUM-28, ADR-0011). It reads the
 * list on `voteOpened` (and on joining, so someone who comes in late sees it too) and shows the open
 * vote to everyone in the room who has not voted in it; whoever is not on its list may only look. The
 * title comes from that read, never from the event. It goes once the votes panel is open, the ballot is
 * in, the vote closes, or it is dismissed.
 */
export function VoteOpenedNotice({
  meetingId,
  panelOpen,
  onShow,
}: {
  meetingId: string
  /** The votes panel is open: everything the notice says is already in view. */
  panelOpen: boolean
  onShow: () => void
}) {
  const [notice, setNotice] = useState<Notice | null>(null)
  const dismissed = useRef(new Set<string>())

  const check = useCallback(async () => {
    try {
      const list = await getVotes(meetingId)
      const open = list.votes.find((v) => v.status === 'Open' && !v.hasVoted)
      setNotice(
        open && !dismissed.current.has(open.id)
          ? { voteId: open.id, title: open.title, canCast: open.actions.canCast }
          : null,
      )
    } catch {
      // A read that fails says nothing here; the votes panel shows the error when opened.
    }
  }, [meetingId])

  useEffect(() => {
    void Promise.resolve().then(check)
  }, [check])
  // While a notice shows, any change may end it (the ballot is in, the vote closed); otherwise only an opening.
  useMeetingEvents(meetingId, () => void check(), notice ? ['voteOpened', 'voteChanged'] : ['voteOpened'])

  useEffect(() => {
    if (!panelOpen || !notice) return
    dismissed.current.add(notice.voteId)
    void Promise.resolve().then(() => setNotice(null))
  }, [panelOpen, notice])

  if (!notice || panelOpen) return null
  return (
    <div className="room-vote-notice" role="status">
      <Icon name="vote" size={20} />
      <p>
        <span className="room-vote-notice-lead">Chủ toạ mở biểu quyết</span>
        <strong>{notice.title}</strong>
      </p>
      <button
        type="button"
        className={notice.canCast ? 'btn btn-primary btn-sm' : 'btn btn-outline btn-sm'}
        onClick={onShow}
      >
        {notice.canCast ? 'Bỏ phiếu' : 'Xem'}
      </button>
      <button
        type="button"
        className="icon-button icon-button-sm"
        aria-label="Để sau"
        data-tooltip="Để sau"
        onClick={() => {
          dismissed.current.add(notice.voteId)
          setNotice(null)
        }}
      >
        <Icon name="x" size={16} />
      </button>
    </div>
  )
}
