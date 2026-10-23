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
}

export interface TranscriptMessage {
  code: string
  message: string
}

export interface SegmentSaved extends TranscriptMessage {
  segment: { segmentId: string; text: string; editedAt: string }
}
