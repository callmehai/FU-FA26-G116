import { useCallback, useEffect, useRef, useState } from 'react'
import { getMeetingCall } from '../../api/calls'
import { errorMessage, isApiError } from '../../lib/errors'
import type { CallRoomState } from '../../types/call'
import { useAccessLost } from '../auth/useAccessLost'

/** How often a room that is not open yet is asked again: starting a meeting tells a guest nothing (SCRUM-59). */
const POLL_MS = 10_000

/**
 * The call room as a guest sees it (`GET …/call`). Starting a meeting only notifies members, so until the room is
 * open it is read again on a timer and whenever the tab comes back. Once open it is left alone: the room says when it
 * closes, and the guest page's session re-check takes the meeting off the screen once it ended. A 404 is the
 * session, the invitation or the meeting being over, which the guest page's re-check settles.
 */
export function useGuestCall(meetingId: string) {
  const [call, setCall] = useState<CallRoomState | null>(null)
  const [error, setError] = useState<string | null>(null)
  const handleAccessLost = useAccessLost()
  const generation = useRef(0)

  const read = useCallback(() => {
    const current = ++generation.current
    getMeetingCall(meetingId).then(
      (state) => {
        if (current !== generation.current) return
        setCall(state)
        setError(null)
      },
      (err: unknown) => {
        if (current !== generation.current) return
        handleAccessLost(err)
        if (!isApiError(err, 404) && !isApiError(err, 401)) setError(errorMessage(err))
      },
    )
  }, [meetingId, handleAccessLost])

  useEffect(() => {
    void Promise.resolve().then(read)
    const reads = generation
    return () => {
      reads.current++
    }
  }, [read])

  const open = call?.status === 'Open'
  useEffect(() => {
    if (open) return
    const check = () => document.visibilityState === 'visible' && read()
    const timer = window.setInterval(check, POLL_MS)
    window.addEventListener('focus', check)
    document.addEventListener('visibilitychange', check)
    return () => {
      window.clearInterval(timer)
      window.removeEventListener('focus', check)
      document.removeEventListener('visibilitychange', check)
    }
  }, [open, read])

  return { call, error, retry: read }
}
