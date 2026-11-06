import type {
  LineSpeakerAssigned,
  SegmentSaved,
  SpeakerAssigned,
  Transcript,
  TranscriptMessage,
  TranscriptPerson,
} from '../types/transcript'
import { api } from './client'

/**
 * Transcripts of meeting recordings (SCRUM-31), made by the in-house speech worker. Contract in
 * docs/03-api/12-ban-ghi-cuoc-hop.md. Same audience as the recording; only the Secretary re-runs or
 * corrects one.
 */

export const getTranscript = (recordingId: string) => api.get<Transcript>(`/api/recordings/${recordingId}/transcript`)

export const retryTranscript = (recordingId: string) =>
  api.post<TranscriptMessage>(`/api/recordings/${recordingId}/transcribe`)

/** The finished transcript as a UTF-8 text file (Chair or Secretary). */
export const getTranscriptText = (recordingId: string) => api.blob(`/api/recordings/${recordingId}/transcript.txt`)

export const editTranscriptSegment = (segmentId: string, text: string) =>
  api.put<SegmentSaved>(`/api/transcript-segments/${segmentId}`, { text })

/** SCRUM-35 — the Secretary names a voice, or clears it with null. */
/** Names a voice as a user or a guest of the meeting, or clears its name with `null`. */
export const assignSpeaker = (recordingId: string, label: string, who: TranscriptPerson | null) =>
  api.put<SpeakerAssigned>(`/api/recordings/${recordingId}/speakers/${encodeURIComponent(label)}`, {
    userId: who && !who.guest ? who.id : null,
    guestId: who?.guest ? who.id : null,
  })

/**
 * Who said one line (AI-eCabinet's "just this line"): it moves to that person's voice — a new one when no voice is
 * named as them — or, given a label, back to that voice of the recording. Every other line keeps its voice.
 */
export const assignLineSpeaker = (segmentId: string, to: TranscriptPerson | { label: string }) =>
  api.put<LineSpeakerAssigned>(
    `/api/transcript-segments/${segmentId}/speaker`,
    'label' in to ? { label: to.label } : to.guest ? { guestId: to.id } : { userId: to.id },
  )
