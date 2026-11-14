import type { Slot, SlotSearchRequest } from '../../types/meetingSlots'
import { addDaysTo, spanDays } from '../calendarSharing/shareFormat'

/**
 * SCRUM-137 — the search limits of the backend (MeetingSlotRules, BR-421, BR-422, ADR-0058 §7), mirrored so the commonest
 * mistakes never cost a round trip. The messages are the backend's own; the server checks again (MSG444).
 */
export const MAX_PEOPLE = 10
export const MAX_RANGE_DAYS = 31
export const MAX_DAYS_AHEAD = 365

/** The settings column. Dates `YYYY-MM-DD`, times `HH:mm`, weekdays 1 (Monday) … 5 (Friday). */
export interface SlotSettings {
  duration: number
  from: string
  to: string
  weekdays: number[]
  workStart: string
  workEnd: string
  lunch: boolean
  lunchStart: string
  lunchEnd: string
  buffer: number
  step: number
}

/** The request's field names, so a server error lands on the same control as the client's own. */
export type SettingsField =
  | 'durationMinutes'
  | 'fromDate'
  | 'toDate'
  | 'weekdays'
  | 'workStart'
  | 'workEnd'
  | 'lunchStart'
  | 'lunchEnd'
  | 'bufferMinutes'
  | 'stepMinutes'
export type SettingsErrors = Partial<Record<SettingsField, string>>

const minutesLabel = (m: number) =>
  m < 60 ? `${m} phút` : m % 60 === 0 ? `${m / 60} giờ` : `${Math.floor(m / 60)} giờ ${m % 60} phút`

export const DURATION_OPTIONS = [15, 30, 45, 60, 90, 120, 180, 240, 480].map((m) => ({ value: String(m), label: minutesLabel(m) }))
export const BUFFER_OPTIONS = [0, 15, 30, 45, 60, 120, 180, 240].map((m) => ({
  value: String(m),
  label: m === 0 ? 'Không cách' : minutesLabel(m),
}))
export const STEP_OPTIONS = [5, 10, 15, 30, 60, 120].map((m) => ({ value: String(m), label: minutesLabel(m) }))
export const WEEKDAY_LABEL = ['T2', 'T3', 'T4', 'T5', 'T6']

/** Day of the week of a `YYYY-MM-DD`, read as UTC so no zone shifts it: 0 = Sunday. */
export const weekdayOf = (day: string) => new Date(`${day}T00:00:00Z`).getUTCDay()

/** The backend's default end: the Friday of the week holding `from`; from a Saturday or Sunday, the next Friday. */
export function defaultTo(from: string) {
  const weekday = weekdayOf(from)
  const toFriday = (5 - weekday + 7) % 7
  return addDaysTo(from, weekday === 6 ? 6 : toFriday)
}

/** The backend's defaults (BR-421, BR-422), from today in Vietnam. */
export function defaultSettings(today: string): SlotSettings {
  return {
    duration: 120,
    from: today,
    to: defaultTo(today),
    weekdays: [1, 2, 3, 4, 5],
    workStart: '08:00',
    workEnd: '17:00',
    lunch: true,
    lunchStart: '12:00',
    lunchEnd: '13:00',
    buffer: 30,
    step: 30,
  }
}

const minutesOf = (time: string) => {
  const m = /^(\d{2}):(\d{2})$/.exec(time)
  return m ? Number(m[1]) * 60 + Number(m[2]) : null
}

/** A start on the step grid (counted from midnight) leaves room for the meeting in one block of working time. */
function fitsOneBlock(s: SlotSettings) {
  const fits = (from: number, to: number) => Math.ceil(from / s.step) * s.step + s.duration <= to
  const ws = minutesOf(s.workStart)!
  const we = minutesOf(s.workEnd)!
  return s.lunch ? fits(ws, minutesOf(s.lunchStart)!) || fits(minutesOf(s.lunchEnd)!, we) : fits(ws, we)
}

/** BR-421, BR-422 as MeetingSlotRules checks them; empty when the search may be sent. */
export function validateSettings(s: SlotSettings, today: string): SettingsErrors {
  const errors: SettingsErrors = {}
  if (!s.from || s.from < today || s.from > addDaysTo(today, MAX_DAYS_AHEAD))
    errors.fromDate = 'Ngày bắt đầu từ hôm nay đến tối đa 365 ngày sau.'
  if (!s.to || (s.from && (s.to < s.from || spanDays(s.from, s.to) > MAX_RANGE_DAYS)))
    errors.toDate = 'Khoảng ngày tối đa 31 ngày, ngày kết thúc không trước ngày bắt đầu.'
  if (s.weekdays.length === 0) errors.weekdays = 'Chọn ít nhất một ngày từ thứ Hai đến thứ Sáu.'
  const ws = minutesOf(s.workStart)
  const we = minutesOf(s.workEnd)
  if (ws === null) errors.workStart = 'Giờ làm việc không hợp lệ.'
  if (we === null || (ws !== null && we <= ws)) errors.workEnd = 'Giờ làm việc không hợp lệ.'
  if (s.lunch) {
    const ls = minutesOf(s.lunchStart)
    const le = minutesOf(s.lunchEnd)
    if (ls === null || (ws !== null && ls < ws)) errors.lunchStart = 'Giờ nghỉ trưa phải nằm trong giờ làm việc.'
    if (le === null || (ls !== null && le <= ls) || (we !== null && le > we))
      errors.lunchEnd = 'Giờ nghỉ trưa phải nằm trong giờ làm việc.'
  }
  const hoursOk = !errors.workStart && !errors.workEnd && !errors.lunchStart && !errors.lunchEnd
  if (hoursOk && !fitsOneBlock(s)) errors.durationMinutes = 'Thời lượng dài hơn một buổi làm việc. Rút ngắn hoặc bỏ nghỉ trưa.'
  return errors
}

export function toRequest(s: SlotSettings, userIds: string[], includeMe: boolean): SlotSearchRequest {
  return {
    userIds,
    includeMe,
    durationMinutes: s.duration,
    fromDate: s.from,
    toDate: s.to,
    weekdays: [...s.weekdays].sort(),
    workStart: s.workStart,
    workEnd: s.workEnd,
    lunchBreak: s.lunch,
    lunchStart: s.lunchStart,
    lunchEnd: s.lunchEnd,
    bufferMinutes: s.buffer,
    stepMinutes: s.step,
  }
}

const WEEKDAY_NAME = ['Chủ nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy']

/** "Thứ Hai, 16/11" */
export const dayLabel = (day: string) => `${WEEKDAY_NAME[weekdayOf(day)]}, ${day.slice(8, 10)}/${day.slice(5, 7)}`

/** "08:00" from an ISO time with `+07:00` — the server already sends Vietnam's clock. */
export const clockOf = (iso: string) => iso.slice(11, 16)

/** "16/11 – 20/11/2026, T2–T6, 08:00–17:00" — the dates and hours of the settings in one line. */
export function settingsDetail(s: SlotSettings) {
  const days = s.weekdays.length === 5 ? 'T2–T6' : [...s.weekdays].sort().map((d) => WEEKDAY_LABEL[d - 1]).join(', ')
  const range = `${s.from.slice(8, 10)}/${s.from.slice(5, 7)} – ${s.to.slice(8, 10)}/${s.to.slice(5, 7)}/${s.to.slice(0, 4)}`
  return `${range}, ${days}, ${s.workStart}–${s.workEnd}`
}

/** "2 giờ, 16/11 – 20/11/2026, T2–T6, 08:00–17:00" — the settings in one line, for the empty state. */
export const settingsSummary = (s: SlotSettings) => `${minutesLabel(s.duration)}, ${settingsDetail(s)}`

export { minutesLabel }

/** A slot's identity in the list: its start (two slots never start at the same instant). */
export const slotKey = (slot: Slot) => slot.start
