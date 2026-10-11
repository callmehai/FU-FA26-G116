import type {
  AppNotification,
  MarkAllReadResult,
  NotificationPage,
  NotificationTarget,
  UnreadCount,
} from '../types/notification'
import { api } from './client'

/**
 * In-app notifications (SCRUM-14, UC-66 … UC-68). Contract in docs/03-api/06-thong-bao-trong-ung-dung.md.
 *
 * Every call is scoped by the backend to the signed-in account — the recipient never travels in
 * the request. Someone else's notification answers 404, the same as one that does not exist.
 */

/** UC-66 — newest first. 400 MSG39 for a bad page, 500 MSG40 when the account check fails. */
export function listNotifications(page = 1, pageSize = 20) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return api.get<NotificationPage>(`/api/notifications?${params}`)
}

/** The bell's badge. */
export const getUnreadCount = () => api.get<UnreadCount>('/api/notifications/unread-count')

/** UC-67a — idempotent: a second call keeps the first `readAt`. */
export const markNotificationRead = (id: string) => api.post<AppNotification>(`/api/notifications/${id}/read`)

/** UC-67b — every unread notification of the account, not only the ones on screen. */
export const markAllNotificationsRead = () => api.post<MarkAllReadResult>('/api/notifications/read-all')

/** UC-68 — where to go. 404 once the target is gone or no longer readable by the caller. */
export const getNotificationTarget = (id: string) => api.get<NotificationTarget>(`/api/notifications/${id}/target`)
