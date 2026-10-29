import type {
  RecordingList,
  RecordingMutationResponse,
  RecordingPlayback,
  RecordingUploadInput,
  RoomTakeDeleted,
  RoomTakeOpened,
  RoomTakeSaved,
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

/**
 * ADR-0041 — the room mic. A take opens while the meeting runs; the participants are told before the meeting's first
 * one only (`consentConfirmed`), a later take is appended to the same recording.
 */
export const beginRoomTake = (meetingId: string, consentConfirmed: boolean, consentPurpose: string) =>
  api.post<RoomTakeOpened>(`/api/meetings/${meetingId}/recordings/takes`, { consentConfirmed, consentPurpose })

/** One part of a take, written at its place: sending it again after a lost answer is harmless. */
export const sendRoomPart = (recordingId: string, part: number, bytes: Uint8Array, final: boolean) =>
  api.putBlob<void>(
    `/api/recordings/${recordingId}/parts/${part}${final ? '?final=true' : ''}`,
    new Blob([bytes.slice().buffer as ArrayBuffer], { type: 'application/octet-stream' }),
  )

/** Switches the take off: it is appended to the meeting's room recording, which is transcribed again. */
export const completeRoomTake = (recordingId: string) =>
  api.post<RoomTakeSaved>(`/api/recordings/${recordingId}/complete`)

/** Erases take `take` (from 1) of a room recording: its sound and lines go, the rest stays as edited. Secretary only. */
export const deleteRoomTake = (recordingId: string, take: number) =>
  api.delete<RoomTakeDeleted>(`/api/recordings/${recordingId}/takes/${take}`)

export const getRecordingPlayback = (recordingId: string) =>
  api.get<RecordingPlayback>(`/api/recordings/${recordingId}/playback`)

/** A link that saves the original file (Chair or Secretary), signed apart from the playback one. */
export const getRecordingDownload = (recordingId: string) =>
  api.get<RecordingPlayback>(`/api/recordings/${recordingId}/download`)

export const eraseRecording = (recordingId: string) =>
  api.delete<RecordingMutationResponse>(`/api/recordings/${recordingId}`)
