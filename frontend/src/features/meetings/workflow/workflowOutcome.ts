import { ApiError } from '../../../api/client'
import { errorMessage, isApiError } from '../../../lib/errors'
import type { MeetingTransitionResponse } from '../../../types/meeting'

/**
 * How a workflow action ended, for the detail page to act on. A dialog keeps the failures the user
 * can fix where they are (a missing reason, a save that may succeed on retry); everything that
 * means "this action no longer applies" closes it and lets the page show what is true now.
 */
export type WorkflowOutcome =
  | { kind: 'done'; response: MeetingTransitionResponse }
  /** 409 — the meeting moved on meanwhile. Nothing was written. */
  | { kind: 'conflict'; message: string }
  /**
   * 403 MSG85, or MSG46 on publish — the caller is no longer the one who may take this action
   * (an organizer who lost `MeetingCreator` gets MSG46 from publish).
   */
  | { kind: 'forbidden'; message: string }
  /** 404 — the meeting is gone, or the caller may no longer read it. */
  | { kind: 'gone' }
  /** 403 without a code — the account itself was deactivated or lost its capability. */
  | { kind: 'accessLost'; error: unknown }

/** The failures that end the action; null for the ones its dialog should show and keep open. */
export function workflowFailure(error: unknown): WorkflowOutcome | null {
  if (error instanceof ApiError && error.status === 409) return { kind: 'conflict', message: errorMessage(error) }
  if (isApiError(error, 403, 'MSG85') || isApiError(error, 403, 'MSG46')) {
    return { kind: 'forbidden', message: errorMessage(error) }
  }
  if (isApiError(error, 403)) return { kind: 'accessLost', error }
  if (isApiError(error, 404)) return { kind: 'gone' }
  return null
}
