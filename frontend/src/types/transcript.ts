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
  /** Who said this one line, when the Secretary moved it off its voice; null keeps the voice's name. */
  speakerUserId?: string | null
  speakerGuestId?: string | null
  speakerName?: string | null
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
  /** A guest of the meeting the voice was named as instead of a user; never set with `assignedUserId`. */
  assignedGuestId?: string | null
  assignedName: string | null
  /** Another voice suggested as the same person more surely: perhaps one person the machine split in two. */
  sameAsLabel?: string | null
  /** The machine named the voice itself, its match being sure enough; the Secretary has not changed it since. */
  autoAssigned?: boolean
}

export interface TranscriptPerson {
  id: string
  fullName: string
  /** A guest invited to the meeting rather than a user: never suggested by voice, named by hand. */
  guest?: boolean
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
  /** The meeting's people and guests the Secretary may name a voice as; empty for everyone else. */
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

export interface LineSpeakerAssigned extends TranscriptMessage {
  segment: { segmentId: string; speakerUserId: string | null; speakerGuestId: string | null; speakerName: string | null }
}
