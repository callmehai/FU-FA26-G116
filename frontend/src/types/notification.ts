/**
 * In-app notifications (SCRUM-14). Mirrors the backend responses in NotificationEndpoints.cs —
 * contract in docs/03-api/06-thong-bao-trong-ung-dung.md.
 */

/**
 * What a notification is about. The backend sends the enum name as a string and may add values
 * later (SCRUM-19 adds `MeetingWaitingApproval`), so screens fall back to a generic look for a
 * value they do not know instead of failing.
 */
export type NotificationType =
  | 'MeetingInvited'
  | 'MeetingWaitingApproval'
  | 'MeetingApproved'
  | 'MeetingRejected'
  | 'MeetingPublished'
  | 'MeetingStartingSoon'
  /** SCRUM-69: the Chair started the meeting; never sent to the Chair who did. */
  | 'MeetingStarted'
  | 'MeetingRescheduled'
  | 'MeetingCancelled'
  | 'VotingOpened'
  | 'MinutesWaitingApproval'
  | 'MinutesIssued'
  | 'DocumentAdded'
  /** SCRUM-20: the caller's role in a meeting changed, or they were removed from it. */
  | 'MeetingRoleChanged'
  | 'MeetingParticipantRemoved'

export interface AppNotification {
  id: string
  type: NotificationType | (string & {})
  title: string
  body: string
  meetingId: string | null
  targetType: string | null
  targetId: string | null
  /** A hint only: opening a notification asks `GET /{id}/target`, which re-checks access. */
  actionUrl: string | null
  isRead: boolean
  readAt: string | null
  createdAt: string
}

/** GET /api/notifications — newest first. */
export interface NotificationPage {
  items: AppNotification[]
  unreadCount: number
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface UnreadCount {
  unreadCount: number
}

/** POST /api/notifications/read-all */
export interface MarkAllReadResult {
  markedCount: number
  unreadCount: number
}

/** GET /api/notifications/{id}/target — only answered while the caller may still open it. */
export interface NotificationTarget {
  targetType: string
  targetId: string
  url: string
}

/** The `notificationReceived` event on /hubs/notifications. Delivered at least once. */
export interface NotificationPush {
  notification: AppNotification
  unreadCount: number
}
