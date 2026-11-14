import type { ShareLevel } from './calendarSharing'
import type { MeetingMode, Sensitivity } from './meeting'

/**
 * SCRUM-137 — docs/03-api/24-tim-gio-hop.md. Dates are `YYYY-MM-DD` and clock times `HH:mm`, both in Vietnam;
 * weekdays are 1 (Monday) … 5 (Friday). Every field may be left out and the server applies the same defaults.
 */
export interface SlotSearchRequest {
  userIds: string[]
  includeMe: boolean
  durationMinutes: number
  fromDate: string
  toDate: string
  weekdays: number[]
  workStart: string
  workEnd: string
  lunchBreak: boolean
  lunchStart: string
  lunchEnd: string
  bufferMinutes: number
  stepMinutes: number
}

/** How a person of the search is counted: the caller's own calendar, a shared one, or none (BR-416). */
export type SlotPersonStatus = 'Own' | 'Shared' | 'NotShared'

export interface SlotPerson {
  id: string
  fullName: string
  email: string
  status: SlotPersonStatus
  level: ShareLevel | null
  startDate: string | null
  endDate: string | null
}

/** One candidate slot. Start and end carry `+07:00`; `freeCount = countedCount − busy − unknown`. */
export interface Slot {
  start: string
  end: string
  freeCount: number
  countedCount: number
  busyUserIds: string[]
  unknownUserIds: string[]
}

export interface SlotDay {
  date: string
  slots: Slot[]
}

/** `code` is `MSG445` only when nothing fits everyone and the slots with the most people free come back (AF-1). */
export interface SlotSearchResult {
  code: string | null
  message: string | null
  allFree: boolean
  truncated: boolean
  people: SlotPerson[]
  days: SlotDay[]
}

/**
 * POST /api/calendar/slots/draft. The roster is built by the server (BR-429): `userIds[0]` chairs, the rest are
 * Members, the caller is Secretary. `acceptedBusyUserIds` are those already busy or unknown when the slot was found.
 */
export interface SlotDraftRequest {
  title: string
  plannedStartTime: string
  plannedEndTime: string
  mode: MeetingMode
  location: string
  onlineLink: string | null
  sensitivity: Sensitivity
  description: string
  userIds: string[]
  includeMe: boolean
  bufferMinutes: number
  acceptedBusyUserIds: string[]
}
