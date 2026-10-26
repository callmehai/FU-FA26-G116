/** Mirrors /api/me/voice in docs/03-api/13-nhan-dien-giong-noi.md (SCRUM-35). */

export interface VoiceConsent {
  grantedAt: string
  noticeVersion: string
  purpose: string
}

export interface VoiceSample {
  id: string
  createdAt: string
  speechMs: number
}

export interface VoiceProfile {
  /** Null until the person agrees, and again after they withdraw. */
  consent: VoiceConsent | null
  samples: VoiceSample[]
  maxSamples: number
  /** The notice in force: shown before agreeing, and sent back so consent is to the words read. */
  noticeVersion: string
  purpose: string
  /** Who controls the voice data; consent is valid only when the person knows (Luật 91/2025, Điều 9.2.b). */
  controller: string
}

export interface VoiceMutation<T> {
  code: string
  message: string
  data: T
}

export type VoiceRejection =
  'too_short' | 'too_quiet' | 'clipped' | 'multiple_speakers' | 'too_long' | 'unreadable_audio'
