/**
 * Conversions between the browser's date inputs and the API's timestamps.
 *
 * `<input type="datetime-local">` and `<input type="date">` hold wall-clock text in the viewer's
 * own timezone, with no offset. The API wants ISO 8601 with an offset and answers in UTC. Going
 * through `Date` does the timezone arithmetic, including daylight-saving edges.
 */

const pad = (n: number) => String(n).padStart(2, '0')

/** UTC ISO → `YYYY-MM-DDTHH:mm` in local time, for a datetime-local input. */
export function toDateTimeInput(iso: string): string {
  const d = new Date(iso)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** A datetime-local value → UTC ISO, or null when the input is empty or unparseable. */
export function fromDateTimeInput(value: string): string | null {
  if (!value) return null
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? null : d.toISOString()
}

/** A date input's `YYYY-MM-DD` → the first instant of that local day, as UTC ISO. */
export function startOfDateInput(value: string): string | null {
  return fromDateTimeInput(value && `${value}T00:00:00`)
}

/** A date input's `YYYY-MM-DD` → the last millisecond of that local day, as UTC ISO. */
export function endOfDateInput(value: string): string | null {
  return fromDateTimeInput(value && `${value}T23:59:59.999`)
}
