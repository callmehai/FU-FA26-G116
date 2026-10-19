/** Local-calendar date helpers for the calendar. Weeks start on Monday, as in Vietnam. */

export const WEEKDAYS = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy']

export function startOfWeek(date: Date) {
  const day = new Date(date.getFullYear(), date.getMonth(), date.getDate())
  // getDay(): Sunday is 0. The Vietnamese week starts on Monday.
  return addDays(day, -((day.getDay() + 6) % 7))
}

export function addDays(date: Date, n: number) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + n)
}

export function startOfMonth(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), 1)
}

export function addMonths(month: Date, n: number) {
  return new Date(month.getFullYear(), month.getMonth() + n, 1)
}

export function daysInMonth(month: Date) {
  return new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate()
}

/** Whole weeks, Monday to Sunday, covering the month: five or six rows, four for a February starting on Monday. */
export function monthGridDays(month: Date) {
  const first = startOfWeek(month)
  const weeks = Math.ceil((daysInMonth(month) + ((month.getDay() + 6) % 7)) / 7)
  return Array.from({ length: weeks * 7 }, (_, i) => addDays(first, i))
}

export function sameMonth(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth()
}

export function endOfDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59, 999)
}

export function sameDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()
}

export function toMonthInput(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
}

export function toDateInput(date: Date) {
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${m}-${d}`
}

/** "06/10/2026" */
export function formatDay(date: Date) {
  return `${String(date.getDate()).padStart(2, '0')}/${String(date.getMonth() + 1).padStart(2, '0')}/${date.getFullYear()}`
}

export function dayLabel(date: Date) {
  return `${WEEKDAYS[date.getDay()]}, ${formatDay(date)}`
}
