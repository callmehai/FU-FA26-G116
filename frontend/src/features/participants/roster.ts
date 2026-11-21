import type { MeetingRole } from '../../types/participant'

/** One person picked for a new meeting. */
export interface RosterEntry {
  userId: string
  fullName: string
  email: string
  role: MeetingRole
  /** The system role may record proxy Voice ID samples; a Secretary without it only in this meeting (PO 2026-11-21). */
  voiceCollector?: boolean
}

/** Red note under a Secretary pick whose system role lacks "Thu giọng mẫu hộ" (PO 2026-11-21). */
export function tempSecretaryNote(names: string[]): string | null {
  if (names.length === 0) return null
  return `${names.join(', ')} không có vai trò Thư ký hệ thống: chỉ thu giọng mẫu hộ được trong cuộc họp này khi đang mở, họp xong là hết quyền với các mẫu đã thu.`
}

/** The backend's limit on the roster sent with a new meeting. */
export const ROSTER_MAX = 100

/** The same rules the backend applies on POST (MSG277), so the commonest mistakes cost no round trip. */
export function rosterError(roster: RosterEntry[]): string | undefined {
  if (roster.length === 0) return 'Chọn ít nhất một người dự họp.'
  if (roster.length > ROSTER_MAX) return `Mỗi lần tạo chọn tối đa ${ROSTER_MAX} người.`
  if (roster.filter((p) => p.role === 'Chair').length !== 1) return 'Chọn một người làm Chủ toạ.'
  return undefined
}
