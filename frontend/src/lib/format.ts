/**
 * Vietnamese date formatting, matching how the rest of the product reads: 24-hour clock, day
 * before month. Timestamps arrive from the backend as UTC ISO strings and are displayed in the
 * viewer's own timezone.
 */
const dateTime = new Intl.DateTimeFormat('vi-VN', {
  hour: '2-digit',
  minute: '2-digit',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

const dateOnly = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

export function formatDateTime(iso: string) {
  return dateTime.format(new Date(iso)).replace(', ', ' ')
}

export function formatDate(iso: string) {
  return dateOnly.format(new Date(iso))
}

const timeOnly = new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' })

const MINUTE = 60_000
const HOUR = 60 * MINUTE

/**
 * How long ago something happened, the way a feed reads it: "Vừa xong", "5 phút trước",
 * "3 giờ trước", "Hôm qua lúc 14:30", then the full date. The exact time belongs in a tooltip.
 */
export function formatRelative(iso: string, now = Date.now()) {
  const then = new Date(iso)
  const elapsed = now - then.getTime()
  // A clock slightly behind the server's must not read "in the future".
  if (elapsed < MINUTE) return 'Vừa xong'
  if (elapsed < HOUR) return `${Math.floor(elapsed / MINUTE)} phút trước`

  const startOfToday = new Date(now)
  startOfToday.setHours(0, 0, 0, 0)
  if (then.getTime() >= startOfToday.getTime()) return `${Math.floor(elapsed / HOUR)} giờ trước`

  const startOfYesterday = new Date(startOfToday)
  startOfYesterday.setDate(startOfYesterday.getDate() - 1)
  if (then.getTime() >= startOfYesterday.getTime()) return `Hôm qua lúc ${timeOnly.format(then)}`

  return formatDateTime(iso)
}

const WEEKDAYS = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy']

/** "09:00" in the viewer's timezone. */
export function formatClock(iso: string) {
  return timeOnly.format(new Date(iso))
}

/** "Thứ Năm, 15/10/2026" */
export function formatWeekdayDate(iso: string) {
  return `${WEEKDAYS[new Date(iso).getDay()]}, ${formatDate(iso)}`
}

/**
 * A meeting's planned or actual sitting: "Thứ Năm, 15/10/2026 · 08:30–09:30". One that runs past
 * midnight names both days.
 */
export function formatSchedule(startIso: string, endIso: string) {
  if (formatDate(startIso) !== formatDate(endIso)) return `${formatDateTime(startIso)} – ${formatDateTime(endIso)}`
  return `${formatWeekdayDate(startIso)} · ${formatClock(startIso)}–${formatClock(endIso)}`
}
