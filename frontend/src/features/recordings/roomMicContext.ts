import { createContext, useContext, useEffect, useRef } from 'react'
import type { RoomRecorder } from './useRoomRecorder'

/**
 * ADR-0072 — the room mic of the meeting whose page or call room this tab shows, held above the routes by
 * RoomMicProvider so that going from one to the other in the same tab never stops it.
 */
export interface RoomMicHold {
  meetingId: string
  recorder: RoomRecorder
  /** Whether the meeting runs, as the page showing it knows; the mic stops when it does not. */
  setRunning: (running: boolean) => void
  /** Called when a take opened or was saved; returns the way to stop listening. */
  listen: (onChanged: () => Promise<void> | void) => () => void
}

export const RoomMicContext = createContext<RoomMicHold | null>(null)

/**
 * The room mic of `meetingId`, or null outside a signed-in page of that meeting (a guest's room). `running` and
 * `onChanged` are this page's: the one shown says whether the meeting still runs (undefined: it does not know), and
 * reads its recordings again.
 */
export function useHeldRoomMic(
  meetingId: string,
  running: boolean | undefined,
  onChanged?: () => Promise<void> | void,
): RoomRecorder | null {
  const hold = useContext(RoomMicContext)
  const mine = hold && hold.meetingId.toLowerCase() === meetingId.toLowerCase() ? hold : null
  const setRunning = mine?.setRunning
  useEffect(() => {
    if (running !== undefined) setRunning?.(running)
  }, [setRunning, running])
  const changed = useRef(onChanged)
  useEffect(() => {
    changed.current = onChanged
  })
  const listen = mine?.listen
  useEffect(() => listen?.(() => changed.current?.()), [listen])
  return mine?.recorder ?? null
}
