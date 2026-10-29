import type { InvitationResponse, MeetingRole, ParticipantSeed } from './participant'

/**
 * Meeting lifecycle. The order below is the state machine: a meeting is drafted, submitted for
 * approval, approved (or rejected and resubmitted), published to attendees, run, then ended. The
 * minutes have their own status (ADR-0013). Cancelled is the other end, reached only before the start
 * (UC-75, ADR-0032).
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
  'Cancelled',
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
  Cancelled: 'Đã huỷ',
}

/** UC-75: the organizer may cancel any meeting that has not started (BR-297). The backend checks again. */
export const CANCELLABLE_STATUSES: readonly MeetingStatus[] = [
  'Draft',
  'WaitingApproval',
  'Rejected',
  'Approved',
  'Published',
]

/** UC-15: the only states whose information can still be changed. The backend checks again on PUT. */
export const EDITABLE_STATUSES: readonly MeetingStatus[] = ['Draft', 'Rejected']

/**
 * UC-15 AF-1: once Published, the organizer may only move the times, place and link (BR-158). The
 * same PUT carries the whole form; any other field that differs answers MSG31.
 */
export const RESCHEDULABLE_STATUSES: readonly MeetingStatus[] = ['Published']

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
 * GET /api/meetings/{id}, and the `meeting` of every create/update response. One meeting is one
 * sitting and carries its own schedule (ADR-0013). Timestamps are UTC ISO 8601.
 */
export interface Meeting {
  id: string
  title: string
  /** Empty string when none was given. */
  description: string
  plannedStartTime: string
  /** Always after `plannedStartTime`. */
  plannedEndTime: string
  /** Set by the server when the Chair starts the meeting; null until then. */
  actualStartedAt: string | null
  /** Set by the server when the Chair ends the meeting; null until then. */
  actualEndedAt: string | null
  mode: MeetingMode
  /** Empty string when none was given. */
  location: string
  /** An external link; null means the TrustCabinet online room (BR-163). */
  onlineLink: string | null
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
  /** The current Chair; every meeting has exactly one (SCRUM-20). */
  chair: MeetingPerson | null
  secretaries: MeetingPerson[]
  /** The caller's role in this meeting; null when they are not on its roster (e.g. the organizer alone). */
  myMeetingRole: MeetingRole | null
  /** The caller's answer to the invitation; null while unanswered (counts as attending) and for the Chair. */
  myResponse: Exclude<InvitationResponse, 'Pending'> | null
  /** Whether to ask "Bạn có tham dự cuộc họp này?" now: invited, not the Chair, Published or InProgress, not answered yet. */
  canRespond: boolean
  /** Only on a Cancelled meeting: who called it off, when and why (BR-298). */
  cancellation: MeetingCancellation | null
}

export interface MeetingCancellation {
  cancelledAt: string
  /** Null when the system cancelled it because nobody started it a day after its planned start (BR-301). */
  cancelledById: string | null
  /** "Hệ thống" when `cancelledById` is null. */
  cancelledByName: string
  reason: string
}

export interface MeetingPerson {
  userId: string
  fullName: string
}

/** One row of the meeting list — only what the list shows. */
export interface MeetingListItem {
  id: string
  title: string
  plannedStartTime: string
  plannedEndTime: string
  mode: MeetingMode
  location: string
  createdAt: string
  status: MeetingStatus
  sensitivity: Sensitivity
  /** As on `Meeting`: lets the calendar ask for an answer without reading each meeting. */
  myResponse: Exclude<InvitationResponse, 'Pending'> | null
  canRespond: boolean
}

/** The columns GET /api/meetings can sort by. */
export type MeetingSortBy = 'title' | 'plannedStartTime' | 'createdAt'
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
  /** UTC ISO; both ends inclusive. A meeting matches when its planned interval overlaps the range (BR-30). */
  from?: string
  to?: string
  page: number
  pageSize: number
  sortBy: MeetingSortBy
  sortDir: SortDirection
  /** The calendar's view: leave out meetings the caller declined, as in AI-eCabinet. */
  excludeDeclined?: boolean
  /** Planned start after now (`upcoming`) or at/before it (`past`), by the server's clock. */
  timing?: MeetingTiming
}

export type MeetingTiming = 'upcoming' | 'past'

/**
 * Body of POST /api/meetings and PUT /api/meetings/{id}. PUT replaces every field. The approver is
 * not part of it: it is chosen when the meeting is sent for approval (SCRUM-19). Times are UTC ISO.
 */
export interface MeetingRequest {
  title: string
  plannedStartTime: string
  plannedEndTime: string
  mode: MeetingMode
  location: string
  /** Null or blank: use the TrustCabinet online room. At most 1,000 characters. */
  onlineLink: string | null
  sensitivity: Sensitivity
  description: string
}

/** POST /api/meetings also names who attends: 1–100 active accounts, exactly one of them Chair. */
export interface MeetingCreateRequest extends MeetingRequest {
  participants: ParticipantSeed[]
}

/** POST answers MSG29; PUT answers MSG30, or MSG138 when a Published meeting was rescheduled. */
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
  /** UC-75: the organizer cancels before the start, with a reason of at most 1,000 characters. */
  | { action: 'Cancel'; reason: string }
  | { action: 'Approve' | 'ReturnToDraft' | 'Publish' }
  /** SCRUM-69: the meeting's current Chair starts or ends it. */
  | { action: 'Start' | 'End' }

export type MeetingTransitionAction = MeetingTransitionRequest['action']

/**
 * Success: MSG78 submit, MSG43 approve, MSG44 reject, MSG79 return to draft, MSG45 publish, MSG110/111 start/end,
 * MSG261 cancel.
 */
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
  | 'Reschedule'
  | 'Start'
  | 'End'
  /** UC-75: the organizer cancelled it. */
  | 'Cancel'
  /** BR-301: the system cancelled it a day after its planned start; `actorId` is null. */
  | 'AutoCancel'
  | 'LegacyTransition'

/** One immutable row of GET /api/meetings/{id}/history. */
export interface MeetingHistoryItem {
  id: string
  meetingId: string
  action: MeetingHistoryAction
  /** Null on the row that created the meeting. */
  fromStatus: MeetingStatus | null
  resultingStatus: MeetingStatus
  /** Null when the system acted (an `AutoCancel` row); `actorName` then reads "Hệ thống". */
  actorId: string | null
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
  /** On a `Cancel` or `AutoCancel` row, for everyone who may read the meeting (BR-298). */
  cancellationReason: string | null
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

/**
 * `agendaChanged` on `/hubs/meetings` (SCRUM-25). Ending the meeting sends one with `changeType`
 * `MeetingEnded` (SCRUM-69); the item fields are null when no item was under discussion.
 */
export interface AgendaChangedEvent {
  eventId: string
  meetingId: string
  changeType: string
  agendaItemId: string | null
  title: string | null
  occurredAt: string
}
