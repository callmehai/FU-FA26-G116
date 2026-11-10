import type { SelectOption } from '../../components/Select'

/** UC-70a EX-1: the server's MSG463, shown before asking when the page can already tell (BR-433). */
export const MSG463_INVALID_PERIOD =
  'Khoảng thời gian không hợp lệ. Chọn tối đa 24 tháng, tháng bắt đầu không sau tháng kết thúc.'
/** UC-70a AF-2: the FE's own MSG466, when the viewer may read no meeting of the period. */
export const MSG466_NOTHING_READABLE = 'Không có cuộc họp nào bạn xem được trong khoảng này.'

/** BR-433: at most 24 months; 12 by default, ending with the current month. */
export const MAX_MONTHS = 24
const DEFAULT_MONTHS = 12
/** How far back the month pickers reach, besides any month already in the URL. */
const PICKER_YEARS = 4

const MONTH = /^(\d{4})-(0[1-9]|1[0-2])$/

const index = (month: string) => Number(month.slice(0, 4)) * 12 + Number(month.slice(5, 7)) - 1
const fromIndex = (i: number) => `${Math.floor(i / 12)}-${String((i % 12) + 1).padStart(2, '0')}`

/** "yyyy-MM" as the server takes it, or null for anything else. */
export function parseMonth(value: string | null): string | null {
  return value && MONTH.test(value) && Number(value.slice(0, 4)) >= 2000 ? value : null
}

/** The current month in Vietnam time, which is what the server counts in, whatever the viewer's own zone. */
export function vietnamMonth(now = new Date()): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
  }).formatToParts(now)
  const part = (type: string) => parts.find((p) => p.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}`
}

export function defaultPeriod(current: string) {
  return { from: fromIndex(index(current) - (DEFAULT_MONTHS - 1)), to: current }
}

/** BR-433, as the server checks it. */
export function periodValid(from: string, to: string) {
  const months = index(to) - index(from) + 1
  return months >= 1 && months <= MAX_MONTHS
}

/** "2026-01" as people read it: 01/2026. */
export const monthLabel = (month: string) => `${month.slice(5, 7)}/${month.slice(0, 4)}`

/** Newest first, from the current month back to January four years ago, plus any month the URL already holds. */
export function monthOptions(current: string, extra: string[]): SelectOption<string>[] {
  const start = (Number(current.slice(0, 4)) - PICKER_YEARS) * 12
  const months = new Set<string>(extra)
  for (let i = index(current); i >= start; i--) months.add(fromIndex(i))
  return [...months]
    .sort((a, b) => index(b) - index(a))
    .map((month) => ({ value: month, label: `Tháng ${monthLabel(month)}` }))
}

const percent = new Intl.NumberFormat('vi-VN', { style: 'percent', maximumFractionDigits: 1 })
const decimal = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 })

/** 0.8725 → "87,3 %" (the server sends a fraction). */
export const formatRate = (rate: number) => percent.format(rate)

/** 95 → "1 giờ 35 phút", 45 → "45 phút". */
export function formatMinutes(minutes: number) {
  const total = Math.round(minutes)
  const hours = Math.floor(total / 60)
  const rest = total % 60
  if (hours === 0) return `${rest} phút`
  return rest === 0 ? `${hours} giờ` : `${hours} giờ ${rest} phút`
}

/** 30.5 → "30,5 giờ"; past two days, in days: 60 → "2,5 ngày". */
export function formatHours(hours: number) {
  return hours < 48 ? `${decimal.format(hours)} giờ` : `${decimal.format(hours / 24)} ngày`
}
