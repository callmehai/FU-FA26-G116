import type {
  RecordingList,
  RecordingMutationResponse,
  RecordingPlayback,
  RecordingUploadInput,
} from '../types/recording'
import { api } from './client'

/**
 * Meeting recordings (SCRUM-29). Contract in docs/03-api/12-ban-ghi-cuoc-hop.md.
 *
 * Only the meeting's people hear a recording; only its Secretary stores or erases one. An <audio>
 * element cannot send the bearer token, so playback goes through a signed URL asked for first.
 */

export const listRecordings = (meetingId: string) => api.get<RecordingList>(`/api/meetings/${meetingId}/recordings`)

/** `consentConfirmed` is always sent as true: the dialog does not let the Secretary get here without it. */
export function uploadRecording(
  meetingId: string,
  input: RecordingUploadInput,
  options: { onProgress?: (loaded: number, total: number) => void; signal?: AbortSignal } = {},
) {
  const form = new FormData()
  form.append('consentConfirmed', 'true')
  form.append('consentPurpose', input.consentPurpose)
  form.append('source', input.source)
  if (input.durationMs !== undefined) form.append('durationMs', String(Math.round(input.durationMs)))
  form.append('file', input.file, input.fileName)
  return api.upload<RecordingMutationResponse>(`/api/meetings/${meetingId}/recordings`, form, options)
}

export const getRecordingPlayback = (recordingId: string) =>
  api.get<RecordingPlayback>(`/api/recordings/${recordingId}/playback`)

export const eraseRecording = (recordingId: string) =>
  api.delete<RecordingMutationResponse>(`/api/recordings/${recordingId}`)
