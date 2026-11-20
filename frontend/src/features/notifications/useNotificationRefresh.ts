import { useEffect, useRef } from 'react'
import type { AppNotification } from '../../types/notification'
import { useNotifications } from './notificationsContext'

/**
 * Calls `refresh` once for each notification pushed to this user in realtime that `matches`, so a screen about the
 * same meeting or the same share re-reads itself instead of waiting for a reload. The notification is only the
 * signal: the screen reads what it shows again, through endpoints that re-check access.
 */
export function useNotificationRefresh(matches: (notification: AppNotification) => boolean, refresh: () => void) {
  const { lastArrival } = useNotifications()
  const seen = useRef(lastArrival)
  const matchesRef = useRef(matches)
  const refreshRef = useRef(refresh)
  useEffect(() => {
    matchesRef.current = matches
    refreshRef.current = refresh
  })
  useEffect(() => {
    if (!lastArrival || lastArrival === seen.current) return
    seen.current = lastArrival
    // Deferred past the effect's synchronous phase: a refresh usually sets state.
    if (matchesRef.current(lastArrival)) void Promise.resolve().then(() => refreshRef.current())
  }, [lastArrival])
}

/** Any notification about a meeting: an invitation, an approval, a start, a cancellation, the minutes… */
export const isMeetingNotification = (notification: AppNotification) => !!notification.meetingId
