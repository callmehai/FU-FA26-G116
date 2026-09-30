import type { CallJoinResponse, CallRoomState } from '../types/call'
import { api } from './client'

/**
 * Meeting call room (SCRUM-39, SCRUM-41). Contract in docs/03-api/09-phong-hop-truc-tuyen.md.
 *
 * Someone who may not enter the room gets the same empty 404 as an unknown meeting.
 */

export const getMeetingCall = (meetingId: string) => api.get<CallRoomState>(`/api/meetings/${meetingId}/call`)

/**
 * A short-lived LiveKit token. Ask for it when the user clicks "join", not when the page opens:
 * it expires in minutes and is only needed for the first connection.
 * 409 MSG117 not in progress · 409 MSG280 in-person meeting · 503 MSG281 LiveKit not configured.
 */
export const joinMeetingCall = (meetingId: string) =>
  api.post<CallJoinResponse>(`/api/meetings/${meetingId}/call/join`)

/**
 * Chair only; mutes, never unmutes. 204 on success.
 * 403 MSG123 not the chair · 409 MSG282 not in the room / no microphone · 502 MSG124 LiveKit down.
 */
export const muteCallParticipant = (meetingId: string, userId: string) =>
  api.post<void>(`/api/meetings/${meetingId}/call/participants/${userId}/mute`)
