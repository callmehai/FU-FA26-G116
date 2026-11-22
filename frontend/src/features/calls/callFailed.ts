import { useSyncExternalStore } from 'react'

/**
 * ADR-0072 — joining a meeting's call failed on this browser (the room could not be reached, or the join was refused),
 * so a hybrid meeting's Secretary gets the room-mic card back on the meeting page. Kept in localStorage so the meeting
 * page's own tab hears it from the room's tab; a later join that works clears it, and it lapses after a while.
 */
const PREFIX = 'trustcabinet.call-failed:'
const LAPSE_MS = 30 * 60_000

const key = (meetingId: string) => `${PREFIX}${meetingId.toLowerCase()}`
const listeners = new Set<() => void>()

export function markCallFailed(meetingId: string, failed: boolean) {
  try {
    if (failed) localStorage.setItem(key(meetingId), String(Date.now()))
    else localStorage.removeItem(key(meetingId))
  } catch {
    // Blocked storage: the meeting page simply does not hear of it.
  }
  listeners.forEach((listener) => listener())
}

function failedRecently(meetingId: string) {
  try {
    const at = Number(localStorage.getItem(key(meetingId)))
    return at > 0 && Date.now() - at < LAPSE_MS
  } catch {
    return false
  }
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  // Another tab of this browser (the call room's) wrote it.
  const onStorage = (event: StorageEvent) => {
    if (event.key === null || event.key.startsWith(PREFIX)) listener()
  }
  window.addEventListener('storage', onStorage)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', onStorage)
  }
}

/** Whether joining this meeting's call failed here lately. */
export function useCallFailed(meetingId: string) {
  return useSyncExternalStore(subscribe, () => failedRecently(meetingId))
}
