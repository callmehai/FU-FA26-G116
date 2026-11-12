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
  /** Its recording was kept, so its owner can hear it back; false for a sample taken before that. */
  playable?: boolean
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
  /** ADR-0060: the second set, recorded for the person by a meeting's Secretary on the room's mic. */
  proxy: ProxyVoiceProfile
}

/** The samples Secretaries took for the person, under the person's own separate consent (ADR-0060). */
export interface ProxyVoiceProfile {
  consent: VoiceConsent | null
  samples: ProxySample[]
  maxSamples: number
  noticeVersion: string
  purpose: string
}

export interface ProxySample extends VoiceSample {
  meetingId: string
  meetingTitle: string
  collectedByName: string
}

/** GET /api/meetings/{id}/voice-collection — what the meeting's Secretary sees (ADR-0060). */
export interface VoiceCollection {
  /** Published or InProgress: samples can be added now, not only heard back and deleted. */
  canCollect: boolean
  maxSamples: number
  people: VoiceCollectionPerson[]
}

export interface VoiceCollectionPerson {
  userId: string
  fullName: string
  role: 'Chair' | 'Secretary' | 'Member'
  /** The person let Secretaries record for them; nothing can be taken without it. */
  consented: boolean
  /** Every sample Secretaries took for them, in any meeting, against `maxSamples`. */
  sampleCount: number
  /** Only the ones taken in this meeting. */
  samples: CollectedSample[]
}

export interface CollectedSample {
  id: string
  createdAt: string
  speechMs: number
  collectedByName: string
}

export interface VoiceMutation<T> {
  code: string
  message: string
  data: T
}

export type VoiceRejection =
  'too_short' | 'too_quiet' | 'clipped' | 'multiple_speakers' | 'too_long' | 'unreadable_audio'
