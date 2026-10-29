/** Why the Chair or the Secretary took a sentence out of the record. */
export type SpeechClipExclusion = 'Leak' | 'Other'

/**
 * The server's guess that a sentence, or the words `[from, to)` of it, are another attendee's, overheard by this mic
 * from their sentence `clipId` (AI-eCabinet's crosstalk marking). A mark only: nothing is hidden for it.
 */
export interface SpeechClipLeak {
  clipId: string
  speakerId: string
  speakerName: string
  whole: boolean
  words: [number, number][]
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
  /** Taken out of the record; only the Chair and the Secretary still get its words. */
  excluded: boolean
  exclusionReason: SpeechClipExclusion | null
  excludedByName: string | null
  leak: SpeechClipLeak | null
  /** The caller may hear it again (ADR-0039): false for one stored without audio, or excluded and not theirs to hear. */
  hasAudio: boolean
  /** Its waveform, base64: one byte (0–255) per 100 ms. */
  peaks: string | null
}

/** GET /api/meetings/{id}/speech-clips — the whole list, or what changed since `readAt` of the last read. */
export interface SpeechClipList {
  meetingId: string
  /** The caller's own mic may send sentences now: the meeting runs and is joined from afar (online or hybrid). */
  canCapture: boolean
  canEdit: boolean
  /** The caller may take sentences out of the record and put them back: the Chair and the Secretary. */
  canExclude: boolean
  /** The caller may save the transcript as text: the Chair or a Secretary, not in a Secret meeting. */
  canExport: boolean
  /** How long the words and audio of a sentence are kept, told to everyone before they join the call. */
  retentionDays: number
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

/** Hub `speechChanged`: carries no words, only "re-read". */
export interface SpeechChangedEvent {
  eventId: string
  meetingId: string
  /** `RecordingsChanged`: a room-mic take opened or was saved, a file was kept or erased, a transcript finished. */
  changeType: 'ClipAdded' | 'ClipEdited' | 'ClipDeleted' | 'RecordingsChanged'
  occurredAt: string
}
