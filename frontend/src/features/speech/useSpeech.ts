import { useCallback, useEffect, useRef, useState } from 'react'
import { listSpeechClips } from '../../api/speech'
import { errorMessage } from '../../lib/errors'
import type { SpeechClip, SpeechClipList } from '../../types/speech'
import { useMeetingEvents } from '../meetings/useMeetingEvents'
import { useMicCapture, type MicCapture } from './useMicCapture'

/**
 * SCRUM-109 — a meeting's per-mic transcript as it grows, and the caller's own mic feeding it. The call room keeps one
 * for the whole call (so capture goes on with the panel closed); the meeting page makes its own.
 */
export interface Speech {
  meetingId: string
  list: SpeechClipList | null
  clips: SpeechClip[]
  error: string | null
  reload: () => void
  /** The person's own switch: on by default in the call, off on the meeting page until they turn it on. */
  mine: boolean
  setMine: (on: boolean) => void
  /** Whether their sentences are being captured now: the meeting allows it, their switch is on, so is their mic. */
  capturing: boolean
  capture: MicCapture
}

/** A sentence that arrives a moment after a read began is still caught by the next one. */
const OVERLAP_MS = 5000

const byTime = (a: SpeechClip, b: SpeechClip) =>
  a.startedAt === b.startedAt ? a.id.localeCompare(b.id) : a.startedAt < b.startedAt ? -1 : 1

export function useSpeech(
  meetingId: string,
  { defaultOn, micOn = true, deviceId }: { defaultOn: boolean; micOn?: boolean; deviceId?: string },
): Speech {
  const [list, setList] = useState<SpeechClipList | null>(null)
  const [clips, setClips] = useState<SpeechClip[]>([])
  const [error, setError] = useState<string | null>(null)
  const [mine, setMine] = useState(defaultOn)
  const readAt = useRef<string | null>(null)
  const generation = useRef(0)

  const load = useCallback(
    async (full: boolean) => {
      const current = ++generation.current
      const since =
        !full && readAt.current ? new Date(Date.parse(readAt.current) - OVERLAP_MS).toISOString() : undefined
      try {
        const page = await listSpeechClips(meetingId, since)
        if (current !== generation.current) return
        readAt.current = page.readAt
        setList(page)
        setError(null)
        setClips((before) => {
          if (!since) return [...page.items].sort(byTime)
          const merged = new Map(before.map((c) => [c.id, c]))
          for (const clip of page.items) merged.set(clip.id, clip)
          return [...merged.values()].sort(byTime)
        })
      } catch (failure) {
        if (current === generation.current) setError(errorMessage(failure))
      }
    },
    [meetingId],
  )

  useEffect(() => {
    void Promise.resolve().then(() => load(true))
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  useMeetingEvents(meetingId, () => void load(false), ['speechChanged'])
  // A meeting starting or ending changes whether anyone may capture: the agenda event carries both.
  useMeetingEvents(meetingId, () => void load(true), ['speechRemoved', 'agendaChanged', 'meetingEnded'])

  const capturing = !!list?.canCapture && mine && micOn
  const capture = useMicCapture(meetingId, capturing, deviceId)

  return {
    meetingId,
    list,
    clips,
    error,
    reload: () => void load(true),
    mine,
    setMine,
    capturing,
    capture,
  }
}
