import { useCallback, useEffect, useState } from 'react'
import { listMeetingApprovers } from '../../api/meetings'
import { errorMessage, isApiError } from '../../lib/errors'
import type { MeetingApprover } from '../../types/meeting'

/**
 * Accounts that can be picked as a meeting's approver: active, holding `MeetingApprover`, never the
 * caller. The endpoint needs `MeetingCreator`, so pass `enabled: false` for anyone else rather than
 * letting the request fail.
 *
 * The organizer also reads it to tell whether the current approver is still eligible: someone
 * missing from the list was deactivated or lost the capability.
 */
export function useMeetingApprovers(enabled = true) {
  const [list, setList] = useState<MeetingApprover[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (!enabled) return
    let cancelled = false
    listMeetingApprovers().then(
      (approvers) => {
        if (cancelled) return
        setList(approvers)
        setError(null)
      },
      (err: unknown) => {
        // Losing MeetingCreator mid-way surfaces again, and is handled, on the next action.
        if (!cancelled) setError(isApiError(err, 403) ? 'Bạn không còn quyền chọn người duyệt.' : errorMessage(err))
      },
    )
    return () => {
      cancelled = true
    }
  }, [enabled, attempt])

  const reload = useCallback(() => {
    setError(null)
    setAttempt((n) => n + 1)
  }, [])

  return { list, error, reload }
}
