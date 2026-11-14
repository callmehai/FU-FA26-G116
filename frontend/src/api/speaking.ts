import type { SpeakingQueue } from '../types/speaking'
import { api } from './client'

/**
 * Register to Speak (SCRUM-27, UC-79). Contract in docs/03-api/20-dang-ky-phat-bieu.md.
 *
 * No route takes a body, and every one answers the whole queue as it now is, without a message. Raising,
 * lowering, starting and finishing one's own turn need the caller connected to the call room (409 MSG458); the
 * Chair's actions need the current Chair (403 MSG457). Every write needs the meeting In Progress (409 MSG117).
 */

const base = (meetingId: string) => `/api/meetings/${meetingId}/speaking-requests`

export const getSpeakingQueue = (meetingId: string) => api.get<SpeakingQueue>(base(meetingId))

/** 201 the first time; 200 and nothing changes while the caller's hand is already up (BR-337). */
export const raiseHand = (meetingId: string) => api.post<SpeakingQueue>(base(meetingId))

/** The caller's own request, before the turn starts (UC-79 AF-1). */
export const lowerHand = (meetingId: string, requestId: string) =>
  api.delete<SpeakingQueue>(`${base(meetingId)}/${requestId}`)

/** The Chair removes a queued or invited request without a turn (AF-3). */
export const declineSpeakingRequest = (meetingId: string, requestId: string) =>
  api.post<SpeakingQueue>(`${base(meetingId)}/${requestId}/decline`)

/** The Chair gives the floor; 409 MSG459 while someone else is invited or speaking (BR-338). */
export const inviteToSpeak = (meetingId: string, requestId: string) =>
  api.post<SpeakingQueue>(`${base(meetingId)}/${requestId}/invite`)

/** The invited person starts the turn, after turning their own microphone on (UC-79 step 6). */
export const startSpeaking = (meetingId: string, requestId: string) =>
  api.post<SpeakingQueue>(`${base(meetingId)}/${requestId}/start`)

/** The speaker, or the Chair from anywhere, closes the turn; an invitation not yet started is withdrawn. */
export const finishSpeaking = (meetingId: string, requestId: string) =>
  api.post<SpeakingQueue>(`${base(meetingId)}/${requestId}/finish`)
