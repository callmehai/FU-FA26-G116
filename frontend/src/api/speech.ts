import type { RecordingPlayback } from '../types/recording'
import type {
  CaptureMode,
  CaptureModeChanged,
  SpeechClipExclusion,
  SpeechClipList,
  SpeechClipSaved,
  SpeechMessage,
} from '../types/speech'
import { api } from './client'

/**
 * Per-mic capture (SCRUM-109). Contract in docs/03-api/12-ban-ghi-cuoc-hop.md. Each attendee sends only their
 * own mic's sentences; nobody can switch on anyone else's.
 */

export const listSpeechClips = (meetingId: string, since?: string) =>
  api.get<SpeechClipList>(
    `/api/meetings/${meetingId}/speech-clips${since ? `?since=${encodeURIComponent(since)}` : ''}`,
  )

/** One sentence's audio. The same `clientClipId` again stores it once: a retry is safe. Undefined means silence. */
export const sendSpeechClip = (
  meetingId: string,
  clientClipId: string,
  audio: Blob,
  startedAt: Date,
  durationMs: number,
) =>
  api.putBlob<SpeechClipSaved | undefined>(
    `/api/meetings/${meetingId}/speech-clips/${clientClipId}?startedAt=${encodeURIComponent(startedAt.toISOString())}` +
      `&durationMs=${Math.round(durationMs)}`,
    audio,
  )

/** The per-mic transcript as a UTF-8 text file, without the sentences taken out of the record. */
export const getSpeechClipsText = (meetingId: string) => api.blob(`/api/meetings/${meetingId}/speech-clips.txt`)

/** A signed link to hear one sentence; an <audio> element plays it without a token. */
export const getSpeechClipPlayback = (clipId: string) =>
  api.get<RecordingPlayback>(`/api/speech-clips/${clipId}/playback`)

export const editSpeechClip = (clipId: string, text: string) =>
  api.put<SpeechMessage>(`/api/speech-clips/${clipId}/text`, { text })

export const deleteSpeechClip = (clipId: string) => api.delete<SpeechMessage>(`/api/speech-clips/${clipId}`)

/** The Chair or the Secretary takes a sentence out of the record, or (`reason` null) puts it back. Nothing is deleted. */
export const setSpeechClipExclusion = (clipId: string, reason: SpeechClipExclusion | null) =>
  api.put<SpeechMessage>(`/api/speech-clips/${clipId}/exclusion`, { excluded: reason !== null, reason })

export const setCaptureMode = (meetingId: string, mode: CaptureMode) =>
  api.put<CaptureModeChanged>(`/api/meetings/${meetingId}/capture-mode`, { mode })
