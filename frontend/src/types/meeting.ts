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

/**
 * GET /api/meetings/{id}, and the `meeting` of every create/update response. Meeting-level fields
 * only: the schedule belongs to the meeting's sessions (SCRUM-70). Timestamps are UTC ISO 8601.
 */
export interface Meeting {
  id: string
  title: string
  /** Empty string when none was given. */
  description: string
  mode: MeetingMode
  /** Empty string when none was given. */
  location: string
  sensitivity: Sensitivity
  status: MeetingStatus
  /** The organizer: who created the meeting, and who moves it through review and publishing. */
  createdById: string
  /**
   * Who reviews the current cycle (SCRUM-19). Chosen when the meeting is sent for approval, so null
   * in Draft; kept through Rejected and Approved as a record of who decided.
   */
  assignedApproverId: string | null
  createdAt: string
  updatedAt: string
}

/** One row of the meeting list — only what the list shows. */
export interface MeetingListItem {
  id: string
  title: string
  createdAt: string
  status: MeetingStatus
  sensitivity: Sensitivity
}

/** The columns GET /api/meetings can sort by. */
export type MeetingSortBy = 'title' | 'createdAt'
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
  /**
   * UTC ISO; both ends inclusive. A meeting matches when one of its sessions that is not cancelled
   * overlaps the range, so a meeting without sessions never matches a range.
   */
  from?: string
  to?: string
  page: number
  pageSize: number
  sortBy: MeetingSortBy
  sortDir: SortDirection
}

/**
 * Body of POST /api/meetings and PUT /api/meetings/{id}. PUT replaces all five fields. The approver
 * is not part of it: it is chosen when the meeting is sent for approval (SCRUM-19).
 */
export interface MeetingRequest {
  title: string
  mode: MeetingMode
  location: string
  sensitivity: Sensitivity
  description: string
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

/**
 * POST /api/meetings/{id}/transitions (SCRUM-19). The actor, the source status and the time come
 * from the server; the client only says what to do.
 */
export type MeetingTransitionRequest =
  | { action: 'SubmitForApproval'; assignedApproverId: string }
  | { action: 'Reject'; reason: string }
  | { action: 'Approve' | 'ReturnToDraft' | 'Publish' }

export type MeetingTransitionAction = MeetingTransitionRequest['action']

/** Success: MSG78 submit, MSG43 approve, MSG44 reject, MSG79 return to draft, MSG45 publish. */
export interface MeetingTransitionResponse {
  code: string
  message: string
  meeting: Meeting
  /** The history row the transition just wrote. */
  history: MeetingHistoryItem
}

/** What a lifecycle history row records. `LegacyTransition` marks rows from before SCRUM-19. */
export type MeetingHistoryAction =
  | 'Create'
  | 'SubmitForApproval'
  | 'Approve'
  | 'Reject'
  | 'ReturnToDraft'
  | 'Publish'
  | 'TransferOrganizer'
  | 'LegacyTransition'

/** One immutable row of GET /api/meetings/{id}/history. */
export interface MeetingHistoryItem {
  id: string
  meetingId: string
  action: MeetingHistoryAction
  /** Null on the row that created the meeting. */
  fromStatus: MeetingStatus | null
  resultingStatus: MeetingStatus
  actorId: string
  actorName: string
  occurredAt: string
  /**
   * Set on a `Reject` row for the organizer and the current approver. Everyone else reads the
   * timeline with it hidden (null).
   */
  rejectionReason: string | null
  /** On a `SubmitForApproval` row: who the meeting was sent to. */
  assignedApproverId: string | null
  previousOrganizerId: string | null
  newOrganizerId: string | null
}

/** GET /api/meetings/{id}/history — oldest first. */
export interface MeetingHistoryPage {
  items: MeetingHistoryItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/**
 * GET /api/meetings/{id}/submission-readiness — the same check SubmitForApproval runs again.
 * Not being ready is a normal answer (200), with what is missing keyed by field.
 */
export interface MeetingSubmissionReadiness {
  meetingId: string
  status: MeetingStatus
  ready: boolean
  errors: Record<string, string>
}
