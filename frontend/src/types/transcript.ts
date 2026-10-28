/** Mirrors the transcript responses of docs/03-api/12-ban-ghi-cuoc-hop.md (SCRUM-31). */

export type TranscriptStatus = 'None' | 'Pending' | 'Processing' | 'Completed' | 'Failed'

export interface TranscriptSegment {
  id: string
  position: number
  startMs: number
  endMs: number
  text: string
  /** The diarizer's label within this one recording — "SPEAKER_00" — or empty when it could not tell. */
  speakerLabel: string
  editedAt: string | null
  editedByName: string | null
}

/** SCRUM-35 — one voice of the recording: the machine's suggestion and the Secretary's decision. */
export interface TranscriptSpeaker {
  label: string
  speechMs: number
  suggestedUserId: string | null
  suggestedName: string | null
  /** Cosine similarity, 0–1. */
  suggestionScore: number | null
  assignedUserId: string | null
  assignedName: string | null
}

export interface TranscriptPerson {
  id: string
  fullName: string
}

export interface Transcript {
  recordingId: string
  status: TranscriptStatus
  provider: string
  language: string
  errorMessage: string
  completedAt: string | null
  durationMs: number
  canEdit: boolean
  segments: TranscriptSegment[]
  speakers: TranscriptSpeaker[]
  /** The meeting's people the Secretary may name a voice as; empty for everyone else. */
  people: TranscriptPerson[]
  /** Finished, and the caller (Chair or Secretary, not a Secret meeting) may save it as text. */
  canExport: boolean
}

export interface TranscriptMessage {
  code: string
  message: string
}

export interface SegmentSaved extends TranscriptMessage {
  segment: { segmentId: string; text: string; editedAt: string }
}

export interface SpeakerAssigned extends TranscriptMessage {
  speaker: TranscriptSpeaker
}
