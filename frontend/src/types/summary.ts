/** SCRUM-36 / SCRUM-32 — mirrors SummaryContracts.cs and MinutesContracts.cs. Contract: docs/03-api/17-du-thao-bien-ban.md. */

export type SummaryStatus = 'Queued' | 'Running' | 'Ready' | 'Failed'

export interface SummarySource {
  agendaItemId: string | null
  title: string
  from: string | null
  to: string | null
  lines: number
}

/** AI-eCabinet's Dự thảo: the three sections of the minutes, as plain text. */
export interface SummaryDraft {
  objectives: string
  discussion: string
  conclusions: string
  sources: SummarySource[]
}

export interface SummaryPerson {
  id: string
  fullName: string
}

export interface MeetingSummary {
  status: SummaryStatus
  /** The last finished draft; kept while a new one is being written. */
  draft: SummaryDraft | null
  model: string
  generatedAt: string | null
  requestedAt: string
  requestedBy: SummaryPerson | null
  errorMessage: string
  reviewedBy: SummaryPerson | null
  reviewedAt: string | null
  editedBy: SummaryPerson | null
  editedAt: string | null
}

/** What the draft would miss if asked for now (AI-eCabinet's minutes readiness). Chair and Secretary only. */
export interface SummaryReadiness {
  sentences: number
  unnamedVoices: number
  transcribing: number
  failedTranscripts: number
  agendaItems: number
  agendaItemsTimed: number
  closedVotes: number
  /** The latest change to the transcript the draft draws on (a line landing, corrected or left out, a voice named). */
  transcriptChangedAt: string | null
}

export interface SummaryState {
  /** Null when there is none, and for anyone but the Chair and the Secretary until it is reviewed. */
  summary: MeetingSummary | null
  canManage: boolean
  aiReady: boolean
  readiness: SummaryReadiness | null
}

export interface SummaryEdit {
  objectives: string
  discussion: string
  conclusions: string
}

export type MinutesStatus = 'Draft' | 'WaitingApproval' | 'Rejected' | 'Issued' | 'Superseded'

export interface MeetingMinutes {
  id: string
  revisionNumber: number
  status: MinutesStatus
  /** Plain text with the line marks documented in MinutesContracts.cs. */
  content: string
  createdAt: string
  /** Sent back on save: the server refuses a save made over someone else's newer one (MSG401). */
  updatedAt: string
  createdBy: SummaryPerson | null
  /** The `draftChangedAt` of the draft the content was built from; null when built without one. */
  builtFromDraftAt: string | null
  /** SCRUM-33: the issued revision a correction replaces; null on the first minutes. */
  supersedesId: string | null
  /** Who sent it for approval and when — Chair and Secretary only. */
  submittedBy: SummaryPerson | null
  submittedAt: string | null
  approvedBy: SummaryPerson | null
  approvedAt: string | null
  issuedAt: string | null
  /** Who returned it, when and why — Chair and Secretary only. */
  returnedBy: SummaryPerson | null
  returnedAt: string | null
  rejectionReason: string | null
}

/** One line of the minutes' history, newest first; readers get only Issued and Superseded lines. */
export interface MinutesRevision {
  id: string
  revisionNumber: number
  status: MinutesStatus
  issuedAt: string | null
}

export interface MinutesState {
  minutes: MeetingMinutes | null
  canManage: boolean
  /** When the AI draft that fills sections I–III last changed; null without one, and for anyone but Chair and Secretary. */
  draftChangedAt: string | null
  history: MinutesRevision[]
  /** The caller is the current Secretary, who sends the minutes for approval (BR-404). */
  canSubmit: boolean
  /** The caller is the current Chair, who approves or returns them (BR-406). */
  canApprove: boolean
}
