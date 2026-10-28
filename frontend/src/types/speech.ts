/** SCRUM-109 — how a meeting's speech is captured; fixed once it starts. */
export type CaptureMode = 'PerMic' | 'Room'

export const CAPTURE_MODE_LABEL: Record<CaptureMode, string> = {
  PerMic: 'Micro từng người',
  Room: 'Thu chung cả phòng',
}

/** One sentence from an attendee's own mic, as everyone in the meeting reads it. */
export interface SpeechClip {
  id: string
  speakerId: string
  speakerName: string
  startedAt: string
  durationMs: number
  text: string
  updatedAt: string
  editedAt: string | null
  editedByName: string | null
  canDelete: boolean
}

/** GET /api/meetings/{id}/speech-clips — the whole list, or what changed since `readAt` of the last read. */
export interface SpeechClipList {
  meetingId: string
  captureMode: CaptureMode
  /** The caller's own mic may send sentences now: the meeting runs and is captured per mic. */
  canCapture: boolean
  canEdit: boolean
  canChangeMode: boolean
  readAt: string
  items: SpeechClip[]
}

export interface SpeechClipSaved {
  code: string
  message: string
  clip: SpeechClip
}

export interface SpeechMessage {
  code: string
  message: string
}

export interface CaptureModeChanged extends SpeechMessage {
  captureMode: CaptureMode
}

/** Hub `speechChanged`: carries no words, only "re-read". */
export interface SpeechChangedEvent {
  eventId: string
  meetingId: string
  changeType: 'ClipAdded' | 'ClipEdited' | 'ClipDeleted' | 'CaptureModeChanged'
  occurredAt: string
}
