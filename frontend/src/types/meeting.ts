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

/** UC-12: the only states whose information can still be changed. The backend checks again on PUT. */
export const EDITABLE_STATUSES: readonly MeetingStatus[] = ['Draft', 'Rejected']

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
export const MEETING_MODES = ['InPerson', 'Online', 'Hybrid'] as const
export type MeetingMode = (typeof MEETING_MODES)[number]

export const MEETING_MODE_LABEL: Record<MeetingMode, string> = {
  InPerson: 'Trực tiếp',
  Online: 'Trực tuyến',
  Hybrid: 'Kết hợp',
}

/** GET /api/meetings/{id}, and the `meeting` of every create/update response. Timestamps are UTC ISO 8601. */
export interface Meeting {
  id: string
  title: string
  /** Empty string when none was given. */
  description: string
  startsAt: string
  /** Null until the organizer settles when the meeting ends. */
  endsAt: string | null
  mode: MeetingMode
  /** Empty string when none was given. */
  location: string
  sensitivity: Sensitivity
  status: MeetingStatus
  createdById: string
  /** Optional while drafting; UC-15 requires one before the meeting is sent for approval. */
  assignedApproverId: string | null
  createdAt: string
  updatedAt: string
}

/** One row of the meeting list — only what the list shows. */
export interface MeetingListItem {
  id: string
  title: string
  startsAt: string
  createdAt: string
  status: MeetingStatus
  sensitivity: Sensitivity
}

/** The columns GET /api/meetings can sort by. */
export type MeetingSortBy = 'title' | 'startsAt' | 'createdAt'
export type SortDirection = 'asc' | 'desc'

/** GET /api/meetings — one page of the meetings the caller may see, filtered and sorted server-side. */
export interface MeetingListResponse {
  items: MeetingListItem[]
  page: number
  pageSize: number
  /** Counts only meetings the caller may see. */
  totalCount: number
  /** 0 when there are no results. */
  totalPages: number
}

/** Criteria for GET /api/meetings. Omitted fields are not filtered on. */
export interface MeetingListQuery {
  q?: string
  status?: MeetingStatus
  sensitivity?: Sensitivity
  /** UTC ISO; both ends inclusive, on the meeting's start time. */
  from?: string
  to?: string
  page: number
  pageSize: number
  sortBy: MeetingSortBy
  sortDir: SortDirection
}

/**
 * Body of POST /api/meetings and PUT /api/meetings/{id}. PUT replaces all eight fields, so an
 * omitted or null approver on PUT unassigns them.
 */
export interface MeetingRequest {
  title: string
  startsAt: string
  endsAt: string | null
  mode: MeetingMode
  location: string
  sensitivity: Sensitivity
  description: string
  assignedApproverId: string | null
}

/** POST answers MSG29, PUT answers MSG30. */
export interface MeetingMutationResponse {
  code: string
  message: string
  meeting: Meeting
}

/** GET /api/meetings/approvers — active accounts holding `MeetingApprover`, sorted by name. */
export interface MeetingApprover {
  id: string
  fullName: string
}
