import type { MeetingStatus } from './meeting'

/**
 * Votes during a meeting (SCRUM-28, UC-52a–UC-57). Contract in docs/03-api/15-bieu-quyet.md.
 *
 * A draft is prepared by the Chair or Secretary and seen only by them; only the Chair opens it, and only while
 * the meeting is InProgress. Who may vote is fixed when it opens. A ballot is final. Ending the meeting does not
 * close a vote: the Chair still closes it and records the conclusion afterwards.
 */
export type VoteStatus = 'Draft' | 'Open' | 'Closed'

/** Guest is a Guest invitation, not an internal role. */
export type VoteRole = 'Chair' | 'Secretary' | 'Member' | 'Guest'

export const VOTE_ROLES: readonly VoteRole[] = ['Chair', 'Secretary', 'Member', 'Guest']

export type VoteConclusionValue = 'Passed' | 'NotPassed' | 'Deferred'

export interface VotePermissions {
  canManageDraft: boolean
  canOpen: boolean
  canClose: boolean
  canRecordConclusion: boolean
}

/** What the caller may do with this vote right now. The server re-checks every write. */
export interface VoteActions {
  canEdit: boolean
  canDelete: boolean
  canAttachDocuments: boolean
  canOpen: boolean
  canCast: boolean
  canClose: boolean
  canRecordConclusion: boolean
  canViewResult: boolean
}

export interface VoteConclusion {
  value: VoteConclusionValue
  note: string
  concludedById: string | null
  concludedByName: string | null
  concludedAt: string | null
}

export interface VoteSummary {
  id: string
  title: string
  status: VoteStatus
  isAnonymous: boolean
  eligibleRoles: VoteRole[]
  openedAt: string | null
  closedAt: string | null
  /** Fixed when the vote opened; 0 for a draft. */
  eligibleCount: number
  receiptCount: number
  hasVoted: boolean
  conclusion: VoteConclusion | null
  documentCount: number
  actions: VoteActions
}

/** GET /api/meetings/{id}/votes. Empty `votes` is MSG207, shown by the FE. */
export interface VoteListSnapshot {
  meetingId: string
  meetingStatus: MeetingStatus
  serverTime: string
  permissions: VotePermissions
  votes: VoteSummary[]
}

export interface VoteOption {
  id: string
  position: number
  label: string
}

export interface VoteOptionResult {
  optionId: string
  label: string
  count: number
  /** Null when nobody voted. */
  percentage: number | null
}

export interface VoteVoterResult {
  /** The user id, or the invitation id for a Guest. */
  id: string
  kind: 'Internal' | 'Guest'
  displayName: string
  /** Null: did not vote. */
  optionId: string | null
}

export interface VoteResult {
  ballotCount: number
  options: VoteOptionResult[]
  /** Null for a secret vote: only the totals exist. */
  voters: VoteVoterResult[] | null
}

/** GET /api/meetings/{id}/votes/{voteId}. `result` is null until the vote is Closed. */
export interface VoteDetail extends VoteSummary {
  meetingId: string
  meetingStatus: MeetingStatus
  serverTime: string
  description: string
  options: VoteOption[]
  isEligible: boolean
  result: VoteResult | null
}

export interface VoteDraftInput {
  title: string
  description: string
  /** At least two distinct options. The usual three are Đồng ý / Không đồng ý / Không ý kiến. */
  options: string[]
  isAnonymous: boolean
  eligibleRoles: VoteRole[]
}

export interface VoteMutationResponse {
  code: string
  message: string
  meetingId: string
  voteId: string
}

export interface VoteClosedResponse extends VoteMutationResponse {
  result: VoteResult
}

/** The `voteChanged` event on /hubs/meetings. Refetch on it; `title` is set only when a vote opens. */
export interface VoteChange {
  eventId: string
  meetingId: string
  voteId: string
  changeType: 'DraftCreated' | 'DraftUpdated' | 'DraftDeleted' | 'Opened' | 'BallotCast' | 'Closed' | 'ConclusionRecorded'
  title: string | null
  occurredAt: string
}
