import { useCallback, useEffect, useRef, useState } from 'react'
import { getMeetingHistory } from '../../../api/meetings'
import { errorMessage } from '../../../lib/errors'
import type { MeetingHistoryItem } from '../../../types/meeting'

/** A meeting's lifecycle is short; one page covers all but the most re-submitted ones. */
const PAGE_SIZE = 50

export interface MeetingHistoryState {
  items: MeetingHistoryItem[]
  /** The first page has arrived — after that a refresh keeps the rows on screen. */
  loaded: boolean
  error: string | null
  /** The raw failure, so the page can tell a 404 (meeting gone) or 403 from a transient error. */
  failure: unknown
  hasMore: boolean
  loadingMore: boolean
}

const INITIAL: MeetingHistoryState = {
  items: [],
  loaded: false,
  error: null,
  failure: null,
  hasMore: false,
  loadingMore: false,
}

/**
 * GET /api/meetings/{id}/history, oldest first (SCRUM-19). `reload` re-reads it without blanking
 * the timeline; `append` adds the row a transition just returned, so the timeline moves with the
 * action instead of waiting for a second request.
 */
export function useMeetingHistory(meetingId: string) {
  const [state, setState] = useState<MeetingHistoryState>(INITIAL)
  const page = useRef(0)
  /** Bumped by every first-page load, so an older answer never overwrites a newer one. */
  const generation = useRef(0)

  const reload = useCallback(async () => {
    const current = ++generation.current
    setState((s) => ({ ...s, error: null, failure: null }))
    try {
      const result = await getMeetingHistory(meetingId, 1, PAGE_SIZE)
      if (current !== generation.current) return
      page.current = 1
      setState({ ...INITIAL, items: result.items, loaded: true, hasMore: result.totalPages > 1 })
    } catch (err) {
      if (current !== generation.current) return
      setState((s) => ({ ...s, error: errorMessage(err), failure: err }))
    }
  }, [meetingId])

  const loadMore = useCallback(async () => {
    const current = generation.current
    setState((s) => ({ ...s, loadingMore: true, error: null, failure: null }))
    try {
      const result = await getMeetingHistory(meetingId, page.current + 1, PAGE_SIZE)
      if (current !== generation.current) return
      page.current = result.page
      setState((s) => ({
        ...s,
        items: [...s.items, ...result.items.filter((item) => !s.items.some((shown) => shown.id === item.id))],
        hasMore: result.page < result.totalPages,
        loadingMore: false,
      }))
    } catch (err) {
      if (current !== generation.current) return
      setState((s) => ({ ...s, loadingMore: false, error: errorMessage(err), failure: err }))
    }
  }, [meetingId])

  const append = useCallback((item: MeetingHistoryItem) => {
    setState((s) => (s.items.some((shown) => shown.id === item.id) ? s : { ...s, items: [...s.items, item] }))
  }, [])

  useEffect(() => {
    const loads = generation
    // Defer past the effect's synchronous phase, as the other pages do.
    void Promise.resolve().then(reload)
    return () => {
      // Unmounting (or switching meetings) makes any answer still in flight stale.
      loads.current++
    }
  }, [reload])

  return { ...state, reload, loadMore, append }
}
