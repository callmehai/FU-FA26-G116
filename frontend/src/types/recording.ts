/** Mirrors the recording responses of docs/03-api/12-ban-ghi-cuoc-hop.md (SCRUM-29). */

export type RecordingSource = 'Upload' | 'BrowserCapture'
export type RecordingStatus = 'Available' | 'Deleted'

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
}

export interface RecordingList {
  meetingId: string
  canUpload: boolean
  items: Recording[]
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
