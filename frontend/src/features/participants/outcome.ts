import { ApiError } from '../../api/client'
import { isApiError } from '../../lib/errors'
import type { ParticipantMutationResponse } from '../../types/participant'

/** How a dialog hands its outcome to the panel: a saved change, or a refusal that needs a re-read. */
export type DialogOutcome =
  { kind: 'saved'; response: ParticipantMutationResponse } | { kind: 'refused'; error: ApiError }

/**
 * A refusal the dialog cannot fix by itself: someone else changed the roster (MSG275), the meeting
 * locked it (MSG98), the caller lost the role (MSG85), or the meeting is gone. The panel closes the
 * dialog, says why and shows what is true now.
 */
export function isRefusal(err: unknown): err is ApiError {
  return isApiError(err, 409, 'MSG275') || isApiError(err, 409, 'MSG98') || isApiError(err, 403) || isApiError(err, 404)
}
