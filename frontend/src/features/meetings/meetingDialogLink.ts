import { useLocation, type To } from 'react-router-dom'
import { toDateInput } from '../calendar/dates'

/**
 * The create and edit forms are dialogs over the current screen, as in AI-eCabinet, and live in the
 * URL so Back closes them and a reload keeps them open. `?create=` carries the day the calendar was
 * showing (YYYY-MM-DD) or "1" from elsewhere; `?edit=1` sits on a meeting's own page.
 */
export const CREATE_PARAM = 'create'
export const EDIT_PARAM = 'edit'
const NO_DAY = '1'

/** Marks a history entry the app pushed, so closing steps Back instead of stacking entries. */
export interface MeetingDialogState {
  meetingDialogPushed?: boolean
}

/** A link that opens the create dialog over the current page, keeping its own query. */
export function useCreateMeetingLink(day?: Date): { to: To; state: MeetingDialogState } {
  const { pathname, search, state } = useLocation()
  const params = new URLSearchParams(search)
  params.set(CREATE_PARAM, day ? toDateInput(day) : NO_DAY)
  // Keep what the page was opened with (the detail page's back target) alongside the marker.
  return { to: { pathname, search: `?${params}` }, state: { ...(state as object), meetingDialogPushed: true } }
}

/** A link that opens the edit dialog on the meeting's page. */
export function useEditMeetingLink(): { to: To; state: MeetingDialogState } {
  const { pathname, search, state } = useLocation()
  const params = new URLSearchParams(search)
  params.set(EDIT_PARAM, NO_DAY)
  return { to: { pathname, search: `?${params}` }, state: { ...(state as object), meetingDialogPushed: true } }
}

const pad = (n: number) => String(n).padStart(2, '0')

/**
 * The window a new meeting starts with, as AI-eCabinet fills it: a day ahead at 08:00–09:00; today
 * (also "no day") from the next full hour, for one hour; a past day, or a next hour that falls on
 * tomorrow, left blank.
 */
export function createMeetingPrefill(
  picked: string | null,
  now = new Date(),
): { date: string; start: string; end: string } {
  const blank = { date: '', start: '', end: '' }
  const today = toDateInput(now)
  const day = !picked || picked === NO_DAY ? today : picked
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day) || day < today) return blank
  if (day > today) return { date: day, start: '08:00', end: '09:00' }
  const start = Math.max(8, now.getHours() + 1)
  if (start > 22) return blank
  return { date: day, start: `${pad(start)}:00`, end: `${pad(start + 1)}:00` }
}
