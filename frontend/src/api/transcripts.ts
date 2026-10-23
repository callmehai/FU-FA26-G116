import type { SegmentSaved, Transcript, TranscriptMessage } from '../types/transcript'
import { api } from './client'

/**
 * Transcripts of meeting recordings (SCRUM-31), made by the in-house speech worker. Contract in
 * docs/03-api/12-ban-ghi-cuoc-hop.md. Same audience as the recording; only the Secretary re-runs or
 * corrects one.
 */

export const getTranscript = (recordingId: string) => api.get<Transcript>(`/api/recordings/${recordingId}/transcript`)

export const retryTranscript = (recordingId: string) =>
  api.post<TranscriptMessage>(`/api/recordings/${recordingId}/transcribe`)

export const editTranscriptSegment = (segmentId: string, text: string) =>
  api.put<SegmentSaved>(`/api/transcript-segments/${segmentId}`, { text })
