import { createContext, useContext } from 'react'
import type { AppNotification } from '../../types/notification'

/** The bell's list, as far as it has been read. Kept while the panel closes and opens again. */
export interface NotificationFeed {
  items: AppNotification[]
  /** The last page loaded; 0 before the first. */
  page: number
  totalPages: number
  /** The first page has arrived at least once — after that a reload keeps the list on screen. */
  loaded: boolean
  loading: boolean
  /** Why the first page could not be (re)loaded. */
  error: string | null
  loadingMore: boolean
  moreError: string | null
}

export interface NotificationsApi {
  /** Null until the backend has answered — the badge stays hidden rather than showing a guess. */
  unreadCount: number | null
  feed: NotificationFeed
  /** Increases once per notification pushed in realtime, so the bell can ring. */
  arrivals: number
  /**
   * The newest notification pushed in realtime, so an open screen about the same meeting can
   * re-read itself (a meeting the Chair just started publishes nothing else). Null until one comes.
   */
  lastArrival: AppNotification | null
  panelOpen: boolean
  setPanelOpen: (open: boolean) => void
  loadFirstPage: () => Promise<void>
  loadMore: () => Promise<void>
  /** Optimistic; rejects (after rolling back) when the backend refused. */
  markRead: (id: string) => Promise<void>
  markAllRead: () => Promise<void>
  /**
   * Marks it read and resolves to where it leads, or to null when it leads nowhere. Rejects with
   * 404 once the target is gone.
   */
  openTarget: (notification: AppNotification) => Promise<string | null>
}

/** Kept apart from the provider so the module exports no component — fast refresh needs that. */
export const NotificationsContext = createContext<NotificationsApi | null>(null)

export function useNotifications() {
  const ctx = useContext(NotificationsContext)
  if (!ctx) throw new Error('useNotifications must be used inside <NotificationsProvider>')
  return ctx
}
