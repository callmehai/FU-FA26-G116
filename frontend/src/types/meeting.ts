/**
 * Meeting lifecycle. The order below is the state machine: a meeting is drafted, submitted for
 * approval, approved (or rejected back to draft), published to attendees, run, ended, then its
 * minutes are issued and the record is archived.
 *
 * Kept as a const array so the UI can both type-check a status and iterate the states, e.g. to
 * render a filter dropdown.
 */
export const MEETING_STATUSES = [
  'Draft',
  'WaitingApproval',
  'Rejected',
  'Approved',
  'Published',
  'InProgress',
  'Ended',
  'MinutesIssued',
  'Archived',
] as const

export type MeetingStatus = (typeof MEETING_STATUSES)[number]

export const MEETING_STATUS_LABEL: Record<MeetingStatus, string> = {
  Draft: 'Nháp',
  WaitingApproval: 'Chờ duyệt',
  Rejected: 'Bị từ chối',
  Approved: 'Đã duyệt',
  Published: 'Đã công bố',
  InProgress: 'Đang diễn ra',
  Ended: 'Đã kết thúc',
  MinutesIssued: 'Đã ban hành biên bản',
  Archived: 'Đã lưu trữ',
}

/** How sensitive a meeting's contents are. Drives the access banner and document handling. */
export const SENSITIVITIES = ['Normal', 'Internal', 'Confidential', 'Secret'] as const
export type Sensitivity = (typeof SENSITIVITIES)[number]

export const SENSITIVITY_LABEL: Record<Sensitivity, string> = {
  Normal: 'Thường',
  Internal: 'Nội bộ',
  Confidential: 'Hạn chế',
  Secret: 'Mật',
}

/** Whether attendees gather in a room, join remotely, or both. */
export type MeetingMode = 'InPerson' | 'Online' | 'Hybrid'

export const MEETING_MODE_LABEL: Record<MeetingMode, string> = {
  InPerson: 'Trực tiếp',
  Online: 'Trực tuyến',
  Hybrid: 'Kết hợp',
}

export interface Meeting {
  id: string
  title: string
  status: MeetingStatus
  sensitivity: Sensitivity
  mode: MeetingMode
  location: string
  /** ISO 8601 timestamps. */
  startsAt: string
  endsAt: string
  createdAt: string
}
