import type { MeetingStatus } from './meeting'

/**
 * Votes during a meeting (SCRUM-28, UC-52a–UC-57). Contract in docs/03-api/15-bieu-quyet.md.
 *
 * A vote is a Draft until the Chair opens it while the meeting is in progress; who may vote is
 * fixed when it opens. One ballot per person, never changed or withdrawn. A secret vote only ever
 * shows totals. What the caller may do comes from `permissions` and each vote's `actions`.
 */
export type VoteStatus = 'Draft' | 'Open' | 'Closed'

export type VoteRole = 'Chair' | 'Secretary' | 'Member' | 'Guest'

export const VOTE_ROLES: readonly VoteRole[] = ['Chair', 'Secretary', 'Member', 'Guest']

export const VOTE_ROLE_LABEL: Record<VoteRole, string> = {
  Chair: 'Chủ toạ',
  Secretary: 'Thư ký',
  Member: 'Thành viên',
  Guest: 'Khách mời',
}

export type VoteConclusionValue = 'Passed' | 'NotPassed' | 'Deferred'

export const VOTE_CONCLUSION_LABEL: Record<VoteConclusionValue, string> = {
  Passed: 'Thông qua',
  NotPassed: 'Không thông qua',
  Deferred: 'Hoãn',
}

/** The Chair's conclusion of a closed vote; recorded once, never edited (BR-262). */
export interface VoteConclusion {
  value: VoteConclusionValue
  note: string
  concludedById: string | null
  concludedByName: string | null
  concludedAt: string | null
}

export interface VotePermissions {
  canManageDraft: boolean
  canOpen: boolean
  canClose: boolean
  canRecordConclusion: boolean
}

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

/** One row of GET /api/meetings/{id}/votes. Drafts reach only the Chair and the Secretary (BR-253). */
export interface VoteSummary {
  id: string
  title: string
  status: VoteStatus
  isAnonymous: boolean
  eligibleRoles: VoteRole[]
  openedAt: string | null
  closedAt: string | null
  /** Fixed when the vote opens; 0 for a draft. */
  eligibleCount: number
  receiptCount: number
  hasVoted: boolean
  conclusion: VoteConclusion | null
  documentCount: number
  actions: VoteActions
}

export interface VoteList {
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

/** A vote document still in the meeting; `readConfirmedAt` is the caller's, null = not read yet. */
export interface VoteDocument {
  id: string
  fileName: string
  contentType: string
  byteSize: number
  pageCount: number | null
  readConfirmedAt: string | null
}

export interface VoteOptionResult {
  optionId: string
  label: string
  count: number
  /** Of the ballots cast, two decimals; null when nobody voted (BR-261). */
  percentage: number | null
}

/** A public vote's voter; `optionId` null = did not vote (BR-273). */
export interface VoteVoter {
  id: string
  kind: 'Internal' | 'Guest'
  displayName: string
  optionId: string | null
}

export interface VoteResult {
  ballotCount: number
  options: VoteOptionResult[]
  /** Null for a secret vote (BR-259). */
  voters: VoteVoter[] | null
}

/** GET /api/meetings/{id}/votes/{voteId}. `result` is null until the vote is closed. */
export interface VoteDetail extends Omit<VoteSummary, 'hasVoted'> {
  meetingId: string
  meetingStatus: MeetingStatus
  serverTime: string
  description: string
  options: VoteOption[]
  /** On the list fixed when the vote opened (the caller's own answer). */
  isEligible: boolean
  hasVoted: boolean
  documents: VoteDocument[]
  result: VoteResult | null
}

/** POST and PUT body: the whole configuration of a draft. */
export interface VoteInput {
  title: string
  description: string
  options: string[]
  isAnonymous: boolean
  eligibleRoles: VoteRole[]
  /** Chair only, meeting in progress: save and open in one step (UC-53 AF-3). */
  openImmediately: boolean
}

export interface VoteMutationResponse {
  code: string
  message: string
  meetingId: string
  voteId: string | null
}

export interface VoteClosedResponse extends VoteMutationResponse {
  result: VoteResult
}

/** 200 MSG265 of UC-76a; `documents` are the meeting documents the call added (none when attaching existing ones). */
export interface VoteDocumentsAttached extends VoteMutationResponse {
  documents: Pick<VoteDocument, 'id' | 'fileName' | 'contentType' | 'byteSize' | 'pageCount'>[]
}

/** 200 MSG253 of UC-74; a confirmation recorded before keeps its `confirmedAt` (AF-1). */
export interface DocumentReadConfirmed {
  code: string
  message: string
  meetingId: string
  meetingDocumentId: string
  confirmedAt: string
  alreadyConfirmed: boolean
}

/**
 * `voteChanged` on `/hubs/meetings`: only says "re-read". The `Draft*` and `DocumentsChanged` events
 * reach the Chair and the Secretary only; `title` is set only on `Opened`; a ballot never names its voter.
 */
export interface VoteChangedEvent {
  eventId: string
  meetingId: string
  voteId: string
  changeType:
    | 'DraftCreated'
    | 'DraftUpdated'
    | 'DraftDeleted'
    | 'Opened'
    | 'BallotCast'
    | 'Closed'
    | 'ConclusionRecorded'
    | 'DocumentsChanged'
  title: string | null
  occurredAt: string
}
