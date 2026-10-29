/** Reading the day and time people type into a form's date and time fields. */

const pad = (n: number) => String(n).padStart(2, '0')

/**
 * What people type for a day: "29/10/2026", "29.10.26", "29-10", "29102026". Day first, as in
 * Vietnam; a missing year is this year. Null when it names no real day (31/02).
 */
export function parseDay(text: string, now = new Date()): Date | null {
  const t = text.trim()
  const parts = /^\d{8}$/.test(t) ? [t.slice(0, 2), t.slice(2, 4), t.slice(4)] : t.split(/[/.\-\s]+/)
  if (parts.length < 2 || parts.length > 3 || parts.some((p) => !/^\d+$/.test(p))) return null
  const day = Number(parts[0])
  const month = Number(parts[1])
  let year = parts[2] === undefined ? now.getFullYear() : Number(parts[2])
  if (parts[2]?.length === 2) year += 2000
  if (year < 1000) return null
  const date = new Date(year, month - 1, day)
  // Date rolls 31/02 over into March; a real day comes back unchanged.
  return date.getDate() === day && date.getMonth() === month - 1 ? date : null
}

/**
 * What people type for a time, read on the 24-hour clock: "14:30", "14h30", "14g", "1430", "930",
 * "9". Null when it is not a time of day.
 */
export function parseTime(text: string): string | null {
  const t = text.trim().toLowerCase().replace(/\s+/g, '')
  let hours: number
  let minutes: number
  const split = /^(\d{1,2})[:.hg](\d{0,2})$/.exec(t)
  if (split) {
    hours = Number(split[1])
    minutes = Number(split[2] || 0)
  } else if (/^\d{1,4}$/.test(t)) {
    // Up to two digits are the hour; three or four are hour and minutes, minutes last.
    hours = Number(t.length <= 2 ? t : t.slice(0, -2))
    minutes = t.length <= 2 ? 0 : Number(t.slice(-2))
  } else {
    return null
  }
  return hours < 24 && minutes < 60 ? `${pad(hours)}:${pad(minutes)}` : null
}
