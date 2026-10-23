import type { VoiceMutation, VoiceProfile, VoiceSample } from '../types/voice'
import { api } from './client'

/**
 * The caller's own Voice ID (SCRUM-35). Contract in docs/03-api/13-nhan-dien-giong-noi.md. There is
 * deliberately no call that names another person.
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

export const deleteVoiceSample = (sampleId: string) =>
  api.delete<VoiceMutation<VoiceProfile>>(`/api/me/voice/samples/${sampleId}`)
