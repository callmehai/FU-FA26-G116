import type { CaptureMode, CaptureModeChanged, SpeechClipList, SpeechClipSaved, SpeechMessage } from '../types/speech'
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

export const editSpeechClip = (clipId: string, text: string) =>
  api.put<SpeechMessage>(`/api/speech-clips/${clipId}/text`, { text })

export const deleteSpeechClip = (clipId: string) => api.delete<SpeechMessage>(`/api/speech-clips/${clipId}`)

export const setCaptureMode = (meetingId: string, mode: CaptureMode) =>
  api.put<CaptureModeChanged>(`/api/meetings/${meetingId}/capture-mode`, { mode })
