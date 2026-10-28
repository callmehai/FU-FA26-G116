import { useState } from 'react'
import { useRecorder, type RecordedTake } from './useRecorder'
import { DEFAULT_PURPOSE, takeName, useRecordingSave } from './useRecordingSave'

/**
 * ADR-0040 — the Secretary's recording of a meeting held in a room. The meeting page holds it, so it outlives every
 * tab: the recorder, the purpose the Secretary confirmed, and the save that follows a stop, or the page being left
 * while it still records.
 */
export function useRoomRecorder(meetingId: string) {
  const save = useRecordingSave(meetingId)
  const [purpose, setPurpose] = useState(DEFAULT_PURPOSE)
  const keep = (take: RecordedTake) =>
    save({
      blob: take.blob,
      fileName: takeName(take),
      source: 'BrowserCapture',
      durationMs: take.durationMs,
      purpose: purpose.trim() || DEFAULT_PURPOSE,
    })
  const recorder = useRecorder(keep)

  return {
    recorder,
    purpose,
    setPurpose,
    /** Stops and saves the take. */
    stop: async () => {
      const take = await recorder.stop()
      if (take) keep(take)
    },
  }
}

export type RoomRecorder = ReturnType<typeof useRoomRecorder>
