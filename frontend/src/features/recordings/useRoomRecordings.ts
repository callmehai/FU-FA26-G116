import { useCallback, useEffect, useRef, useState } from 'react'
import { listRecordings } from '../../api/recordings'
import type { RecordingList } from '../../types/recording'
import { useMeetingEvents, type MeetingEvent } from '../meetings/useMeetingEvents'

export const RECORDINGS_EVENTS: readonly MeetingEvent[] = ['recordingsChanged']

/**
 * ADR-0041 — the meeting's recordings as the page needs them outside the Bản ghi tab: whether the room is being
 * recorded right now (everyone sees it), and, for the Secretary's mic, whether the meeting has its first take yet and
 * whether a take is open on another device. Read again whenever the hub says the recordings changed.
 */
export function useRoomRecordings(meetingId: string, enabled: boolean) {
  const [list, setList] = useState<RecordingList | null>(null)
  const generation = useRef(0)
  const reload = useCallback(async () => {
    if (!enabled) return
    const mine = ++generation.current
    try {
      const data = await listRecordings(meetingId)
      if (mine === generation.current) setList(data)
    } catch {
      // The chip and the card keep what they showed; the Bản ghi tab reports the failure itself.
    }
  }, [meetingId, enabled])
  useEffect(() => {
    void Promise.resolve().then(reload)
  }, [reload])
  useMeetingEvents(meetingId, () => void reload(), RECORDINGS_EVENTS)
  const open = enabled ? (list?.items.find((r) => r.status === 'Pending') ?? null) : null
  return { list: enabled ? list : null, open, reload }
}
