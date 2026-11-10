import type { Share, ShareLevel, ShareStatus } from '../../types/calendarSharing'

/** BR-431: a share lasts at most 365 days, both ends counted. */
export const MAX_DAYS = 365

/** UC-84f / UC-85c: the FE's own MSG439, shown for a Stopped share; the backend never sends it. */
export const MSG439_STOPPED =
  'Chia sẻ lịch đã dừng vì người xem không còn quyền tạo cuộc họp hoặc tài khoản bị vô hiệu hoá. Hãy xin chia sẻ mới.'

export const LEVEL_LABEL: Record<ShareLevel, string> = {
  Level1: 'Họp Thường',
  Level2: 'Thường và Nội bộ',
}

/** What each level lets the viewer see, for the radio rows and the (i) beside a level chip. */
export const LEVEL_HINT: Record<ShareLevel, string> = {
  Level1: 'Giờ bận của các cuộc họp Thường.',
  Level2: 'Giờ bận của các cuộc họp Thường và Nội bộ. Họp Mật không bao giờ hiện.',
}

export const STATUS_LABEL: Record<ShareStatus, string> = {
  Pending: 'Chờ đồng ý',
  Effective: 'Đang chia sẻ',
  Declined: 'Đã từ chối',
  Withdrawn: 'Đã rút',
  Ended: 'Đã ngừng',
  Stopped: 'Đã dừng',
}

/** Open shares first; the closed ones stay listed, muted, so the reason access ended is not lost. */
export const isClosed = (share: Share) => share.status !== 'Pending' && share.status !== 'Effective'

/** Today's date in Vietnam, which is what the server counts in, whatever the viewer's own zone. */
export function vietnamToday(now = new Date()): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(now)
  const part = (type: string) => parts.find((p) => p.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}-${part('day')}`
}

/** Day arithmetic on `YYYY-MM-DD` strings, in UTC so no zone or daylight saving shifts a day. */
const toUtc = (day: string) => Date.UTC(Number(day.slice(0, 4)), Number(day.slice(5, 7)) - 1, Number(day.slice(8, 10)))
const fromUtc = (ms: number) => new Date(ms).toISOString().slice(0, 10)

export const addDaysTo = (day: string, n: number) => fromUtc(toUtc(day) + n * 86_400_000)

/** Days from `from` to `to`, both counted: the same day is 1. */
export const spanDays = (from: string, to: string) => Math.round((toUtc(to) - toUtc(from)) / 86_400_000) + 1

/** "10/11/2026" */
export const dayText = (day: string) => `${day.slice(8, 10)}/${day.slice(5, 7)}/${day.slice(0, 4)}`

/** "10/11 – 09/12/2026", or with both years when they differ. */
export function periodText(from: string, to: string) {
  if (from === to) return dayText(from)
  const sameYear = from.slice(0, 4) === to.slice(0, 4)
  const head = sameYear ? `${from.slice(8, 10)}/${from.slice(5, 7)}` : dayText(from)
  return `${head} – ${dayText(to)}`
}

export interface RangeRules {
  /** The earliest start allowed (today for a new share). */
  minStart: string
  /** When set, the start may not change (an Effective share keeps its start, UC-84d). */
  fixedStart?: string
  /** When set, the range must sit inside it (the owner narrowing a request, UC-84c). */
  within?: { from: string; to: string }
  today: string
}

/** BR-431 as the server checks it, so the dialog can say what is wrong before sending. Null when valid. */
export function rangeError(from: string, to: string, rules: RangeRules): string | null {
  if (!from || !to) return 'Chọn đủ ngày bắt đầu và ngày kết thúc.'
  if (rules.fixedStart && from !== rules.fixedStart) return `Ngày bắt đầu giữ nguyên ${dayText(rules.fixedStart)}.`
  if (!rules.fixedStart && from < rules.minStart) return 'Ngày bắt đầu không được trước hôm nay.'
  if (to < from) return 'Ngày kết thúc phải từ ngày bắt đầu trở đi.'
  if (to < rules.today) return 'Ngày kết thúc đã qua.'
  if (rules.within && (from < rules.within.from || to > rules.within.to))
    return `Chỉ chọn trong khoảng đã xin: ${periodText(rules.within.from, rules.within.to)}.`
  if (spanDays(from, to) > MAX_DAYS) return `Chia sẻ dài tối đa ${MAX_DAYS} ngày.`
  return null
}

/** Quick ranges from a start, as AI-eCabinet offers them: a week, a month, three months. */
export const PRESETS = [
  { label: '1 tuần', days: 7 },
  { label: '1 tháng', days: 30 },
  { label: '3 tháng', days: 90 },
] as const

/** The other side of a share, from the caller's point of view. */
export const otherSide = (share: Share, me: string) => (share.owner.id === me ? share.viewer : share.owner)
