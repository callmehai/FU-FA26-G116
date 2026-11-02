import type { MeetingStatus } from './meeting'

/**
 * Guest access to one meeting by invitation (UC-04, SCRUM-116). Contract in
 * docs/03-api/16-truy-cap-khach-uc04.md.
 */

/** What a guest may know of the meeting before and after verifying: no sensitivity, place or roster. */
export interface GuestMeeting {
  id: string
  title: string
  status: MeetingStatus
  plannedStartTime: string
  plannedEndTime: string
}

/** POST /api/guest-access/invitations/{id} — the invitation behind a link, before any code. */
export interface GuestInvitation {
  invitationId: string
  meeting: GuestMeeting
  /** e.g. `ng***@example.com`. */
  maskedEmail: string
}

/** POST …/code — MSG375. */
export interface GuestCodeSent {
  code: string
  message: string
  expiresInSeconds: number
  resendAfterSeconds: number
}

/** POST …/verify — the guest token; valid as long as the session (12 hours), never refreshed. */
export interface GuestVerified {
  accessToken: string
  expiresAt: string
  meetingId: string
  profileCompleted: boolean
}

/** GET /api/guest-access/session. The email is the one verified; the name and job title are self-declared. */
export interface GuestSession {
  sessionId: string
  expiresAt: string
  meeting: GuestMeeting
  email: string
  invitedName: string
  displayName: string | null
  jobTitle: string | null
  profileCompleted: boolean
}

/** PUT /api/guest-access/session/profile — MSG376. */
export interface GuestProfileSaved {
  code: string
  message: string
  session: GuestSession
}
