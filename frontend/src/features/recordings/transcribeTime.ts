/**
 * How far a transcript being made has got, in the words the Bản ghi tab shows (PO 23/11/2026: "hiện được process bar
 * ko ? và hiện thời gian xử lí được ko"). Imports nothing, so `npm test` runs it under node as it is.
 */

/** The time left is a guess from the pace so far; before this much is done, or this much time has gone, it is noise. */
export const ETA_MIN_PERCENT = 5
export const ETA_MIN_ELAPSED_MS = 30_000

/** How long since `startedAt` (ISO), never below zero: the server's clock may run a little ahead of the browser's. */
export function elapsedSince(startedAt: string | null | undefined, now: number): number | null {
  if (!startedAt) return null
  const start = Date.parse(startedAt)
  return Number.isNaN(start) ? null : Math.max(0, now - start)
}

/** The time left at the pace so far, elapsed × (100 − p) / p; null while too early to tell. */
export function remainingMs(percent: number, elapsedMs: number | null): number | null {
  if (elapsedMs === null || percent < ETA_MIN_PERCENT || percent >= 100 || elapsedMs < ETA_MIN_ELAPSED_MS) return null
  return (elapsedMs * (100 - percent)) / percent
}

/** A running clock, `m:ss` or `h:mm:ss`. */
export function formatClock(ms: number): string {
  const total = Math.max(0, Math.floor(ms / 1000))
  const h = Math.floor(total / 3600)
  const m = Math.floor((total % 3600) / 60)
  const s = String(total % 60).padStart(2, '0')
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${s}` : `${m}:${s}`
}

/** A length of time in words: "45 giây", "24 phút", "1 giờ 5 phút". */
export function formatSpan(ms: number): string {
  if (ms < 60_000) return `${Math.max(1, Math.round(ms / 1000))} giây`
  const minutes = Math.round(ms / 60_000)
  if (minutes < 60) return `${minutes} phút`
  const rest = minutes % 60
  return rest ? `${Math.floor(minutes / 60)} giờ ${rest} phút` : `${Math.floor(minutes / 60)} giờ`
}

/** "còn ~15 phút", or "sắp xong" under a minute. */
export function formatRemaining(ms: number): string {
  return ms < 60_000 ? 'sắp xong' : `còn ~${formatSpan(ms)}`
}

/**
 * The line under the bar, as its three cells: "Đang chuyển chữ 42%", the clock "12:30", "còn ~15 phút". No per cent
 * before the worker has given one (an older worker never does), no clock before the run started, no time left while
 * too early to tell.
 */
export function progressParts(
  percent: number,
  elapsedMs: number | null,
  again = false,
): { label: string; clock: string | null; left: string | null } {
  const left = remainingMs(percent, elapsedMs)
  return {
    label: `Đang chuyển chữ${again ? ' lại' : ''}${percent > 0 ? ` ${percent}%` : ''}`,
    clock: elapsedMs === null ? null : formatClock(elapsedMs),
    left: left === null ? null : formatRemaining(left),
  }
}

/** How long making a finished transcript took; null for one made before the start was kept. */
export function processingTime(startedAt: string | null | undefined, completedAt: string | null | undefined): number | null {
  if (!startedAt || !completedAt) return null
  const span = Date.parse(completedAt) - Date.parse(startedAt)
  return Number.isNaN(span) || span < 0 ? null : span
}
