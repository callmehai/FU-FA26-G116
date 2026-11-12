import type { CollectedSample, VoiceCollection, VoiceMutation, VoiceProfile, VoiceSample } from '../types/voice'
import { api } from './client'

/**
 * The caller's own Voice ID (SCRUM-35). Contract in docs/03-api/13-nhan-dien-giong-noi.md. No
 * /api/me/voice call names another person: nobody consents for someone else. The one way to record
 * someone else's voice is the meeting Secretary's collection below (ADR-0060).
 */

export const getMyVoice = () => api.get<VoiceProfile>('/api/me/voice')

export const grantVoiceConsent = (noticeVersion: string) =>
  api.post<VoiceMutation<VoiceProfile>>('/api/me/voice/consent', { noticeVersion })

export const withdrawVoiceConsent = () => api.delete<VoiceMutation<VoiceProfile>>('/api/me/voice/consent')

export function addVoiceSample(audio: Blob, fileName: string) {
  const form = new FormData()
  form.append('file', audio, fileName)
  return api.postForm<VoiceMutation<VoiceSample>>('/api/me/voice/samples', form)
}

/** The caller's own sample recording, to hear it back (as AI-eCabinet does). Nobody else's can be fetched. */
export const voiceSampleAudio = (sampleId: string) => api.blob(`/api/me/voice/samples/${sampleId}/audio`)

export const deleteVoiceSample = (sampleId: string) =>
  api.delete<VoiceMutation<VoiceProfile>>(`/api/me/voice/samples/${sampleId}`)

/** The meeting Secretary's view: needs `VoiceCollector` and being this meeting's Secretary, rechecked server-side. */
export const getVoiceCollection = (meetingId: string) =>
  api.get<VoiceCollection>(`/api/meetings/${meetingId}/voice-collection`)

export function collectVoiceSample(meetingId: string, userId: string, audio: Blob, fileName: string) {
  const form = new FormData()
  form.append('file', audio, fileName)
  return api.postForm<VoiceMutation<CollectedSample>>(`/api/meetings/${meetingId}/voice-collection/${userId}/samples`, form)
}

/** Every play is audited server-side: the voice is someone else's. */
export const collectedSampleAudio = (meetingId: string, sampleId: string) =>
  api.blob(`/api/meetings/${meetingId}/voice-collection/samples/${sampleId}/audio`)

export const deleteCollectedSample = (meetingId: string, sampleId: string) =>
  api.delete<VoiceMutation<{ id: string }>>(`/api/meetings/${meetingId}/voice-collection/samples/${sampleId}`)
