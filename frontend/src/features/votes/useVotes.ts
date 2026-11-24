import { useCallback, useEffect, useRef, useState } from 'react'
import { getVote, getVotes } from '../../api/votes'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { VoteDetail, VoteList, VoteMutationResponse } from '../../types/vote'
import { useAccessLost } from '../auth/useAccessLost'
import { useMeetingEvents } from '../meetings/useMeetingEvents'
import { VIEWER_CLOSED_EVENT } from '../documents/viewerOverlay'

/** Events can be lost (no outbox), so a panel with an open vote re-reads on its own this often while visible. */
const POLL_MS = 15_000

type State =
  | { kind: 'loading' }
  | { kind: 'loaded'; data: VoteList }
  | { kind: 'error'; message: string; data: VoteList | null }
  | { kind: 'gone' }

/**
 * A meeting's votes (SCRUM-28), shared by the detail tab and the call room's panel.
 *
 * It re-reads on `voteChanged`, on `participantsChanged` (a role taken away changes what the caller
 * may do), on `meetingEnded`, when the window regains focus, and every 15 s while a vote is open and
 * the tab is visible — never two reads at once. Every write answers only a message, so `run` shows
 * it and re-reads; a refusal (someone else closed the vote, the meeting ended) is said once and the
 * re-read shows what is true now.
 */
export function useVotes(meetingId: string) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const generation = useRef(0)
  const inFlight = useRef(false)
  const again = useRef(false)

  const read = useCallback(async () => {
    const current = ++generation.current
    try {
      const data = await getVotes(meetingId)
      if (current === generation.current) setState({ kind: 'loaded', data })
    } catch (err) {
      if (current !== generation.current || handleAccessLost(err)) return
      if (isApiError(err, 404)) return setState({ kind: 'gone' })
      setState((s) => ({
        kind: 'error',
        message: errorMessage(err),
        data: s.kind === 'loaded' || s.kind === 'error' ? s.data : null,
      }))
    }
  }, [meetingId, handleAccessLost])

  /** A call while a read runs asks for one more read after it, instead of a second one alongside. */
  const load = useCallback(async () => {
    if (inFlight.current) {
      again.current = true
      return
    }
    inFlight.current = true
    try {
      do {
        again.current = false
        await read()
      } while (again.current)
    } finally {
      inFlight.current = false
    }
  }, [read])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  useMeetingEvents(meetingId, () => void load(), ['voteChanged', 'participantsChanged', 'meetingEnded'])

  const live = state.kind === 'loaded' && state.data.votes.some((v) => v.status === 'Open')
  useEffect(() => {
    const onFocus = () => void load()
    window.addEventListener('focus', onFocus)
    // The viewer opened over the page closed: a read confirmed there unlocks "Gửi phiếu".
    window.addEventListener(VIEWER_CLOSED_EVENT, onFocus)
    const timer = live
      ? window.setInterval(() => document.visibilityState === 'visible' && void load(), POLL_MS)
      : undefined
    return () => {
      window.removeEventListener('focus', onFocus)
      window.removeEventListener(VIEWER_CLOSED_EVENT, onFocus)
      window.clearInterval(timer)
    }
  }, [live, load])

  /** Runs one write, says how it went, then re-reads; true when it was saved. */
  const run = useCallback(
    async (write: () => Promise<VoteMutationResponse>) => {
      try {
        toast.show((await write()).message)
        return true
      } catch (err) {
        if (!handleAccessLost(err)) toast.show(errorMessage(err), 'error')
        return false
      } finally {
        await load()
      }
    },
    [toast, handleAccessLost, load],
  )

  return { state, reload: load, run }
}

type DetailState =
  | { kind: 'loading' }
  | { kind: 'loaded'; data: VoteDetail }
  | { kind: 'error'; message: string; data: VoteDetail | null }
  | { kind: 'gone' }

/**
 * One vote's detail — options, the caller's eligibility, documents, the result — read while its card
 * is open, and again whenever the list was re-read (`stamp` is the list's `serverTime`), so a ballot
 * or a close elsewhere shows here too.
 */
export function useVoteDetail(meetingId: string, voteId: string, stamp: string, enabled: boolean) {
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<DetailState>({ kind: 'loading' })
  const generation = useRef(0)

  const read = useCallback(async () => {
    const current = ++generation.current
    try {
      const data = await getVote(meetingId, voteId)
      if (current === generation.current) setState({ kind: 'loaded', data })
    } catch (err) {
      if (current !== generation.current || handleAccessLost(err)) return
      if (isApiError(err, 404)) return setState({ kind: 'gone' })
      setState((s) => ({
        kind: 'error',
        message: errorMessage(err),
        data: s.kind === 'loaded' || s.kind === 'error' ? s.data : null,
      }))
    }
  }, [meetingId, voteId, handleAccessLost])

  useEffect(() => {
    if (!enabled) return
    void Promise.resolve().then(read)
    const reads = generation
    return () => {
      reads.current++
    }
  }, [enabled, stamp, read])

  return { state, reload: read }
}

/** What the panel holds for one vote's detail; `stamp` is the list's `serverTime` it was read for. */
export type VoteDetailEntry =
  | { kind: 'loaded'; data: VoteDetail; stamp: string }
  | { kind: 'error'; message: string; data: VoteDetail | null; stamp: string }
  | { kind: 'gone' }

/**
 * The details of a panel's votes, kept while the panel is on screen so a card folded and unfolded again opens at once,
 * and read ahead when the pointer or the keyboard reaches a card's title: opening a card then moves the page once, with
 * its whole body, instead of a loading bar first and the body after it (Hải 23/11/2026). The open cards are read again
 * whenever the list was (`stamp`), as before; the detail already on screen stays there meanwhile.
 */
export function useVoteDetails(meetingId: string, stamp: string | null, openIds: readonly string[]) {
  const handleAccessLost = useAccessLost()
  const [entries, setEntries] = useState<ReadonlyMap<string, VoteDetailEntry>>(new Map())
  /** The list stamp each vote is being read for, so a hover and the click after it do not read it twice. */
  const reading = useRef(new Map<string, string>())

  const read = useCallback(
    async (voteId: string, forStamp: string) => {
      if (reading.current.get(voteId) === forStamp) return
      reading.current.set(voteId, forStamp)
      // An answer for an older list never replaces one for a newer list that came back first.
      const newer = (entry: VoteDetailEntry | undefined) => !!entry && entry.kind !== 'gone' && entry.stamp > forStamp
      try {
        const data = await getVote(meetingId, voteId)
        setEntries((map) =>
          newer(map.get(voteId)) ? map : new Map(map).set(voteId, { kind: 'loaded', data, stamp: forStamp }),
        )
      } catch (err) {
        if (handleAccessLost(err)) return
        setEntries((map) => {
          const was = map.get(voteId)
          if (newer(was)) return map
          if (isApiError(err, 404)) return new Map(map).set(voteId, { kind: 'gone' })
          const data = was && was.kind !== 'gone' ? was.data : null
          return new Map(map).set(voteId, { kind: 'error', message: errorMessage(err), data, stamp: forStamp })
        })
      } finally {
        if (reading.current.get(voteId) === forStamp) reading.current.delete(voteId)
      }
    },
    [meetingId, handleAccessLost],
  )

  /** Reads a vote unless what is held was read for the current list (or the vote is gone). */
  const prefetch = useCallback(
    (voteId: string) => {
      if (!stamp) return
      const entry = entries.get(voteId)
      if (entry && (entry.kind === 'gone' || entry.stamp === stamp)) return
      void read(voteId, stamp)
    },
    [stamp, entries, read],
  )

  const openKey = openIds.join(',')
  useEffect(() => {
    if (!openKey) return
    // After the render, as the list's own first read.
    void Promise.resolve().then(() => openKey.split(',').forEach(prefetch))
  }, [openKey, prefetch])

  /** "Thử lại": reads again, also for the current list. */
  const reload = useCallback(
    (voteId: string) => {
      if (!stamp) return
      reading.current.delete(voteId)
      void read(voteId, stamp)
    },
    [stamp, read],
  )

  return { entries, prefetch, reload }
}
