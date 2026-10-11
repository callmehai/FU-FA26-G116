import { HubConnectionState } from '@microsoft/signalr'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { createHubConnection, retryDelay } from '../../api/hubs'
import * as notificationsApi from '../../api/notifications'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { AppNotification, NotificationPush } from '../../types/notification'
import { NotificationsContext, type NotificationFeed, type NotificationsApi } from './notificationsContext'

const PAGE_SIZE = 20

const EMPTY_FEED: NotificationFeed = {
  items: [],
  page: 0,
  totalPages: 0,
  loaded: false,
  loading: false,
  error: null,
  loadingMore: false,
  moreError: null,
}

/**
 * The signed-in user's notifications (SCRUM-14): the unread count behind the bell, the list behind
 * its panel, and the `/hubs/notifications` connection that keeps both current without a reload.
 *
 * Mounted once by `RequireAuth`, above both workspaces' shells, so moving between management and
 * the calendar keeps the connection and the list instead of reconnecting.
 *
 * The backend is the source of truth. Realtime only says "something changed"; whenever the
 * connection comes back after a gap, the count — and the list, if it was loaded — are re-read,
 * because events pushed while disconnected are not replayed.
 */
export function NotificationsProvider({ children }: { children: ReactNode }) {
  const toast = useToast()
  const [unreadCount, setUnreadCount] = useState<number | null>(null)
  const [feed, setFeed] = useState<NotificationFeed>(EMPTY_FEED)
  const [arrivals, setArrivals] = useState(0)
  const [panelOpen, setPanelOpenState] = useState(false)

  // Read inside callbacks and hub handlers, which must not be recreated on every change.
  const feedRef = useRef(feed)
  const panelOpenRef = useRef(panelOpen)
  useEffect(() => {
    feedRef.current = feed
    panelOpenRef.current = panelOpen
  }, [feed, panelOpen])
  /** Ids pushed in realtime. The push is "at least once", so the same id can arrive twice. */
  const pushedIds = useRef(new Set<string>())
  /** Bumped by every first-page load, so an older response never overwrites a newer one. */
  const listGeneration = useRef(0)

  const setPanelOpen = useCallback((open: boolean) => setPanelOpenState(open), [])

  const refreshCount = useCallback(async () => {
    try {
      setUnreadCount((await notificationsApi.getUnreadCount()).unreadCount)
    } catch {
      // The badge is a hint: keep the last known count rather than flash a wrong one.
    }
  }, [])

  const loadFirstPage = useCallback(async () => {
    const generation = ++listGeneration.current
    setFeed((f) => ({ ...f, loading: true, error: null }))
    try {
      const page = await notificationsApi.listNotifications(1, PAGE_SIZE)
      if (generation !== listGeneration.current) return
      setFeed({ ...EMPTY_FEED, items: page.items, page: 1, totalPages: page.totalPages, loaded: true })
      setUnreadCount(page.unreadCount)
    } catch (error) {
      if (generation !== listGeneration.current) return
      setFeed((f) => ({ ...f, loading: false, error: errorMessage(error) }))
    }
  }, [])

  const loadMore = useCallback(async () => {
    const current = feedRef.current
    if (current.loading || current.loadingMore || current.page >= current.totalPages) return
    const generation = listGeneration.current
    setFeed((f) => ({ ...f, loadingMore: true, moreError: null }))
    try {
      const next = await notificationsApi.listNotifications(current.page + 1, PAGE_SIZE)
      if (generation !== listGeneration.current) return
      // New notifications arriving meanwhile push older ones onto later pages, so a page can repeat
      // rows already shown — never skip one. Dropping repeats by id is enough.
      setFeed((f) => ({
        ...f,
        items: [...f.items, ...next.items.filter((n) => !f.items.some((shown) => shown.id === n.id))],
        page: next.page,
        totalPages: next.totalPages,
        loadingMore: false,
      }))
      setUnreadCount(next.unreadCount)
    } catch (error) {
      if (generation !== listGeneration.current) return
      setFeed((f) => ({ ...f, loadingMore: false, moreError: errorMessage(error) }))
    }
  }, [])

  const markRead = useCallback(async (id: string) => {
    const before = feedRef.current.items.find((n) => n.id === id)
    if (before?.isRead) return

    // Optimistic: the row and the badge update at once, and are put back if the backend refuses.
    const readAt = new Date().toISOString()
    setFeed((f) => ({ ...f, items: f.items.map((n) => (n.id === id ? { ...n, isRead: true, readAt } : n)) }))
    if (before) setUnreadCount((c) => (c === null ? c : Math.max(0, c - 1)))

    try {
      const saved = await notificationsApi.markNotificationRead(id)
      setFeed((f) => ({ ...f, items: f.items.map((n) => (n.id === id ? saved : n)) }))
    } catch (error) {
      if (before) {
        setFeed((f) => ({ ...f, items: f.items.map((n) => (n.id === id ? before : n)) }))
        setUnreadCount((c) => (c === null ? c : c + 1))
      }
      throw error
    }
  }, [])

  const markAllRead = useCallback(async () => {
    // UC-67b / BR-51: the backend marks every unread notification of the account, not just the
    // loaded page, so every loaded row turns read and the count drops to what the backend reports.
    const result = await notificationsApi.markAllNotificationsRead()
    const readAt = new Date().toISOString()
    setFeed((f) => ({ ...f, items: f.items.map((n) => (n.isRead ? n : { ...n, isRead: true, readAt })) }))
    setUnreadCount(result.unreadCount)
  }, [])

  const openTarget = useCallback(
    async (notification: AppNotification) => {
      // UC-68 / BR-53: opening marks it read, but a failed save never blocks the way there.
      if (!notification.isRead) void markRead(notification.id).catch(() => undefined)
      // Nothing to open: the target endpoint would 404, which must not read as "no longer available".
      if (!notification.targetType) return null
      // The stored actionUrl is only a hint: the target endpoint re-checks that the meeting is
      // still readable by this user, and 404s otherwise.
      const target = await notificationsApi.getNotificationTarget(notification.id)
      if (!isInternalPath(target.url)) throw new Error('Đường dẫn của thông báo không hợp lệ.')
      return target.url
    },
    [markRead],
  )

  const receive = useCallback(
    (push: NotificationPush) => {
      const { notification } = push
      const isNew =
        !pushedIds.current.has(notification.id) && !feedRef.current.items.some((n) => n.id === notification.id)
      pushedIds.current.add(notification.id)

      setUnreadCount(push.unreadCount)
      // Before the panel was ever opened there is no list to update; opening it reads page 1.
      setFeed((f) => {
        if (!f.loaded) return f
        const rest = f.items.filter((n) => n.id !== notification.id)
        return {
          ...f,
          items: isNew ? [notification, ...rest] : f.items.map((n) => (n.id === notification.id ? notification : n)),
        }
      })

      if (!isNew) return
      setArrivals((a) => a + 1)
      // With the panel open the new row appearing at the top says it already.
      if (!panelOpenRef.current) toast.show(`${notification.title} — ${notification.body}`, 'info')
    },
    [toast],
  )

  /** After a gap in the connection: re-read the count, and the list if it was on screen. */
  const resync = useCallback(() => {
    void refreshCount()
    if (feedRef.current.loaded) void loadFirstPage()
  }, [refreshCount, loadFirstPage])

  // The connection outlives renders; its handlers reach the latest callbacks through refs.
  const receiveRef = useRef(receive)
  const resyncRef = useRef(resync)
  useEffect(() => {
    receiveRef.current = receive
    resyncRef.current = resync
  }, [receive, resync])

  useEffect(() => {
    const connection = createHubConnection('/hubs/notifications')
    let disposed = false
    let attempt = 0
    let retryTimer: number | undefined

    connection.on('notificationReceived', (push: NotificationPush) => receiveRef.current(push))
    // The built-in reconnect recovered a dropped connection; what was pushed meanwhile is lost.
    connection.onreconnected(() => resyncRef.current())
    // Closed for good: the server ended a connection whose token expired, or reconnecting failed.
    // Start a new one — `accessTokenFactory` refreshes the token on the way.
    connection.onclose(() => {
      if (!disposed) scheduleStart()
    })

    function scheduleStart() {
      window.clearTimeout(retryTimer)
      retryTimer = window.setTimeout(start, retryDelay(attempt++))
    }

    async function start() {
      if (disposed || connection.state !== HubConnectionState.Disconnected) return
      try {
        await connection.start()
        if (attempt > 0) resyncRef.current()
        attempt = 0
      } catch {
        if (!disposed) scheduleStart()
      }
    }

    // A tab coming back to the foreground, or the network coming back, catches up straight away
    // instead of waiting for the next retry.
    function catchUp() {
      if (document.visibilityState !== 'visible') return
      void refreshCount()
      if (connection.state === HubConnectionState.Disconnected) {
        window.clearTimeout(retryTimer)
        void start()
      }
    }

    document.addEventListener('visibilitychange', catchUp)
    window.addEventListener('online', catchUp)
    // The badge's first value; the hub only reports changes.
    notificationsApi.getUnreadCount().then(
      ({ unreadCount }) => {
        if (!disposed) setUnreadCount(unreadCount)
      },
      () => undefined,
    )
    void start()

    return () => {
      disposed = true
      window.clearTimeout(retryTimer)
      document.removeEventListener('visibilitychange', catchUp)
      window.removeEventListener('online', catchUp)
      void connection.stop()
    }
  }, [refreshCount])

  const value = useMemo<NotificationsApi>(
    () => ({
      unreadCount,
      feed,
      arrivals,
      panelOpen,
      setPanelOpen,
      loadFirstPage,
      loadMore,
      markRead,
      markAllRead,
      openTarget,
    }),
    [unreadCount, feed, arrivals, panelOpen, setPanelOpen, loadFirstPage, loadMore, markRead, markAllRead, openTarget],
  )

  return <NotificationsContext value={value}>{children}</NotificationsContext>
}

/** Only an in-app path: never another origin, even if a bad URL ever reached the database. */
function isInternalPath(url: string) {
  return url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/\\')
}
