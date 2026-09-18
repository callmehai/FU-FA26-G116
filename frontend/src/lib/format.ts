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
