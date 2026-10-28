import type { TranscriptStatus } from './transcript'

/** Mirrors the recording responses of docs/03-api/12-ban-ghi-cuoc-hop.md (SCRUM-29). */

/** `BrowserCapture` is the room mic's recording, its takes appended one after another (ADR-0041). */
export type RecordingSource = 'Upload' | 'BrowserCapture'
/** `Pending`: a room-mic take still being recorded; everyone sees it, nobody hears it yet. */
export type RecordingStatus = 'Pending' | 'Available' | 'Deleted'

/** One stretch of the room mic between switching it on and off: where it starts in the audio, and when it was said. */
export interface RecordingTake {
  offsetMs: number
  durationMs: number
  startedAt: string
}

export interface Recording {
  id: string
  fileName: string
  contentType: string
  byteSize: number
  /** 0 when the browser could not tell; transcription fills in the real length. */
  durationMs: number
  source: RecordingSource
  status: RecordingStatus
  createdAt: string
  createdByName: string
  consentObtainedAt: string | null
  consentPurpose: string
  retainUntil: string | null
  deletedAt: string | null
  canErase: boolean
  /** SCRUM-31 — "None" for a recording kept before speech to text existed. */
  transcriptStatus: TranscriptStatus
  /** The Chair or a Secretary may save the original file; never in a Secret meeting. */
  canDownload: boolean
  createdById: string
  /** The room mic's takes, in order; null for an uploaded file. */
  takes: RecordingTake[] | null
  /** A Secretary may save a take left open on another device. */
  canClose: boolean
}

export interface RecordingList {
  meetingId: string
  canUpload: boolean
  /** The caller may switch the room mic on now: the meeting's Secretary, while it runs in a room. */
  canRecord: boolean
  items: Recording[]
}

/** A take opened: its audio goes up in parts of exactly `partSize` bytes, the last one shorter. */
export interface RoomTakeOpened {
  recordingId: string
  partSize: number
  maxBytes: number
}

export interface RoomTakeSaved {
  code: string
  message: string
  /** The room recording the take went into; null when the mic heard nothing. */
  recordingId: string | null
  appended: boolean
}

export interface RecordingPlayback {
  url: string
  expiresAt: string
}

export interface RecordingMutationResponse {
  code: string
  message: string
  recordingId: string
}

export interface RecordingUploadInput {
  file: Blob
  fileName: string
  source: RecordingSource
  consentPurpose: string
  durationMs?: number
}
