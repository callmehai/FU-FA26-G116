import { isApiError } from '../../lib/errors'
import type { ShareResult } from '../../types/calendarSharing'

/**
 * What a dialog hands back: the saved share with the server's message, or a refusal that means the list on screen is
 * out of date (the share moved on, the caller lost the right, the share is gone) — the page reloads and says why.
 */
export type ShareOutcome = { kind: 'saved'; result: ShareResult } | { kind: 'stale'; error: unknown }

/** Errors that no edit in the dialog can fix: the list behind it no longer matches the server. */
export const isStale = (error: unknown) =>
  isApiError(error, 404) ||
  isApiError(error, 403) ||
  isApiError(error, 409, 'MSG450') ||
  isApiError(error, 409, 'MSG449') ||
  isApiError(error, 409, 'MSG468')
