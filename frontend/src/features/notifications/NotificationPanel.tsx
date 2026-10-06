import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDateTime, formatRelative } from '../../lib/format'
import type { AppNotification } from '../../types/notification'
import { notificationKind } from './notificationKinds'
import { useNotifications } from './notificationsContext'

/** How often relative times ("5 phút trước") are recomputed while the panel is open. */
const CLOCK_TICK_MS = 30_000

/**
 * The list behind the bell (UC-18 … UC-21): newest first, unread rows tinted, "Xem thêm" for older
 * pages. Each open re-reads page 1, but keeps what was already on screen while it does, so the
 * panel never flashes empty.
 */
export function NotificationPanel({ id, onClose }: { id: string; onClose: () => void }) {
  const { unreadCount, feed, loadFirstPage, loadMore, markRead, markAllRead, openTarget } = useNotifications()
  const navigate = useNavigate()
  const toast = useToast()
  const now = useNow(CLOCK_TICK_MS)
  const [markingAll, setMarkingAll] = useState(false)
  const [opening, setOpening] = useState<string | null>(null)

  useEffect(() => {
    void loadFirstPage()
  }, [loadFirstPage])

  async function open(notification: AppNotification) {
    setOpening(notification.id)
    try {
      const url = await openTarget(notification)
      // A notification with no target is only read; the panel stays where the user is.
      if (url === null) return
      onClose()
      navigate(url)
    } catch (error) {
      // UC-19 EX-1: the meeting was deleted, or the user may no longer read it.
      toast.show(isApiError(error, 404) ? 'Thông báo này không còn khả dụng.' : errorMessage(error), 'error')
    } finally {
      setOpening(null)
    }
  }

  async function markOne(notification: AppNotification) {
    try {
      await markRead(notification.id)
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    }
  }

  async function markEverything() {
    setMarkingAll(true)
    try {
      await markAllRead()
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      setMarkingAll(false)
    }
  }

  const unread = unreadCount ?? 0
  const titleId = `${id}-title`

  return (
    <section id={id} className="notif-panel" role="dialog" aria-labelledby={titleId}>
      <div className="notif-head">
        <h2 id={titleId}>
          Thông báo
          {unread > 0 && <span className="badge badge-brand">{unread > 99 ? '99+' : unread} mới</span>}
        </h2>
        <button
          type="button"
          className="btn btn-ghost btn-sm notif-mark-all"
          title="Đánh dấu tất cả đã đọc"
          onClick={markEverything}
          disabled={unread === 0 || markingAll}
        >
          <Icon name="checkCheck" size={16} />
          <span className="notif-mark-all-label">{markingAll ? 'Đang đánh dấu…' : 'Đánh dấu tất cả đã đọc'}</span>
        </button>
      </div>

      <div className="notif-body" aria-busy={feed.loading}>
        {!feed.loaded ? (
          feed.error ? (
            <div className="notif-state" role="alert">
              <p>{feed.error}</p>
              <button type="button" className="btn btn-outline btn-sm" onClick={() => void loadFirstPage()}>
                <Icon name="refresh" size={16} />
                Thử lại
              </button>
            </div>
          ) : (
            <SkeletonRows />
          )
        ) : feed.items.length === 0 ? (
          <div className="notif-state">
            <span className="notif-state-icon">
              <Icon name="bell" size={22} />
            </span>
            <p className="notif-state-title">Chưa có thông báo nào</p>
            <p>Lời mời họp và nhắc lịch họp sẽ hiện ở đây.</p>
          </div>
        ) : (
          <>
            {feed.error && (
              <p className="notif-inline-error" role="alert">
                Không làm mới được danh sách.{' '}
                <button type="button" className="link-button" onClick={() => void loadFirstPage()}>
                  Thử lại
                </button>
              </p>
            )}
            <ul className="notif-list">
              {feed.items.map((notification) => (
                <NotificationRow
                  key={notification.id}
                  notification={notification}
                  now={now}
                  opening={opening === notification.id}
                  onOpen={() => void open(notification)}
                  onMarkRead={() => void markOne(notification)}
                />
              ))}
            </ul>
          </>
        )}
      </div>

      {feed.loaded && feed.page < feed.totalPages && (
        <div className="notif-foot">
          {feed.moreError ? (
            <p className="notif-inline-error" role="alert">
              {feed.moreError}{' '}
              <button type="button" className="link-button" onClick={() => void loadMore()}>
                Thử lại
              </button>
            </p>
          ) : (
            <button
              type="button"
              className="btn btn-ghost btn-sm btn-block"
              onClick={() => void loadMore()}
              disabled={feed.loadingMore}
            >
              {feed.loadingMore ? 'Đang tải…' : 'Xem thông báo cũ hơn'}
            </button>
          )}
        </div>
      )}
    </section>
  )
}

function NotificationRow({
  notification,
  now,
  opening,
  onOpen,
  onMarkRead,
}: {
  notification: AppNotification
  now: number
  opening: boolean
  onOpen: () => void
  onMarkRead: () => void
}) {
  const kind = notificationKind(notification.type)
  const unread = !notification.isRead
  const openRef = useRef<HTMLButtonElement>(null)

  function markRead() {
    // The check button disappears with the unread state; keep the keyboard focus on this row.
    openRef.current?.focus()
    onMarkRead()
  }

  return (
    <li className={`notif-item${unread ? ' notif-item-unread' : ''}`}>
      <button
        ref={openRef}
        type="button"
        className="notif-open"
        onClick={onOpen}
        disabled={opening}
        aria-busy={opening}
      >
        <span className={`notif-icon notif-tone-${kind.tone}`}>
          <Icon name={kind.icon} size={16} />
        </span>
        <span className="notif-content">
          <span className="notif-title">
            {unread && <span className="visually-hidden">Chưa đọc: </span>}
            {notification.title}
          </span>
          {notification.body && <span className="notif-text">{notification.body}</span>}
          <time className="notif-time" dateTime={notification.createdAt} title={formatDateTime(notification.createdAt)}>
            {formatRelative(notification.createdAt, now)}
          </time>
        </span>
      </button>
      {unread && (
        <>
          <span className="notif-dot" aria-hidden="true" />
          <button
            type="button"
            className="icon-button notif-mark"
            title="Đánh dấu đã đọc"
            aria-label={`Đánh dấu đã đọc: ${notification.title}`}
            onClick={markRead}
          >
            <Icon name="check" size={16} />
          </button>
        </>
      )}
    </li>
  )
}

/** Placeholder rows shaped like real ones, so the panel does not jump when the list arrives. */
function SkeletonRows() {
  return (
    <ul className="notif-list" aria-label="Đang tải thông báo">
      {[0, 1, 2, 3].map((i) => (
        <li key={i} className="notif-item notif-skeleton">
          <span className="notif-icon" />
          <span className="notif-content">
            <span className="skeleton" />
            <span className="skeleton" />
            <span className="skeleton" />
          </span>
        </li>
      ))}
    </ul>
  )
}

/** The current time, ticking while the component is on screen. */
function useNow(intervalMs: number) {
  const [now, setNow] = useState(Date.now)
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), intervalMs)
    return () => window.clearInterval(timer)
  }, [intervalMs])
  return now
}
