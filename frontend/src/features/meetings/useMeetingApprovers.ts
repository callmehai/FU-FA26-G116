import { useEffect, useState } from 'react'
import { listMeetingApprovers } from '../../api/meetings'
import { isApiError } from '../../lib/errors'
import type { MeetingApprover } from '../../types/meeting'

/**
 * Accounts that can be picked as a meeting's approver. The endpoint needs `MeetingCreator`, so pass
 * `enabled: false` for anyone else rather than letting the request fail.
 *
 * Draft forms never assign an approver. This hook resolves the current approver on details and
 * provides eligible accounts when the submission flow is integrated.
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
        // The endpoint rechecks capabilities if access changes while this screen is open.
        if (!cancelled)
          setError(
            isApiError(err, 403) ? 'Bạn không còn quyền chọn người duyệt.' : 'Không tải được danh sách người duyệt.',
          )
      },
    )
    return () => {
      cancelled = true
    }
  }, [enabled, attempt])

  return { list, error, reload: () => setAttempt((n) => n + 1) }
}
