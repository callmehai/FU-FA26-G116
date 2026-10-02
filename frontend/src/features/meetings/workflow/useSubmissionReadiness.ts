import { useCallback, useEffect, useState } from 'react'
import { getMeetingSubmissionReadiness } from '../../../api/meetings'
import { errorMessage } from '../../../lib/errors'
import { readinessProblems } from './readiness'

export type ReadinessState =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'ready' }
  | { kind: 'notReady'; problems: string[] }
  | { kind: 'error'; message: string; failure: unknown }

/**
 * GET …/submission-readiness for a draft the caller organizes, so the page can say what is missing
 * before the organizer tries to submit. `version` changes whenever the meeting does (its
 * `updatedAt`), which re-checks after an edit. Submitting checks again on the server regardless.
 */
export function useSubmissionReadiness(meetingId: string, enabled: boolean, version: string) {
  const [state, setState] = useState<ReadinessState>({ kind: 'idle' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (!enabled) return
    let cancelled = false
    void Promise.resolve().then(async () => {
      if (cancelled) return
      setState({ kind: 'checking' })
      try {
        const result = await getMeetingSubmissionReadiness(meetingId)
        if (cancelled) return
        setState(result.ready ? { kind: 'ready' } : { kind: 'notReady', problems: readinessProblems(result.errors) })
      } catch (err) {
        if (!cancelled) setState({ kind: 'error', message: errorMessage(err), failure: err })
      }
    })
    return () => {
      cancelled = true
    }
  }, [meetingId, enabled, version, attempt])

  const retry = useCallback(() => setAttempt((n) => n + 1), [])

  return { readiness: enabled ? state : ({ kind: 'idle' } as ReadinessState), retry }
}
