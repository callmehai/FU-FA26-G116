import { useCallback, useEffect, useRef, useState } from 'react'
import { listSpeechClips } from '../../api/speech'
import { errorMessage } from '../../lib/errors'
import type { SpeechClip, SpeechClipList } from '../../types/speech'
import { useAuth } from '../auth/useAuth'
import { useGuest } from '../guest/guestContext'
import { useMeetingEvents } from '../meetings/useMeetingEvents'
import { useMicCapture, type MicCapture } from './useMicCapture'

/**
 * SCRUM-109 — a meeting's per-mic transcript as it grows, and the caller's own mic feeding it. Only the call room feeds
 * it (ADR-0040): there the call's own mic is the one switch, so what someone says is captured while they are heard. The
 * meeting page only reads it, and sends on whatever an earlier visit left on the device.
 *
 * On a guest page (SCRUM-129) the list is the guest's own sentences only, as the server sends it, and no hub event
 * tells the guest one was stored, so it is read again after each.
 */
export interface Speech {
  meetingId: string
  list: SpeechClipList | null
  clips: SpeechClip[]
  error: string | null
  reload: () => void
  /** Whether their sentences are being captured now: the meeting allows it and their call mic is on. */
  capturing: boolean
  capture: MicCapture
}

/** A sentence that arrives a moment after a read began is still caught by the next one. */
const OVERLAP_MS = 5000

const byTime = (a: SpeechClip, b: SpeechClip) =>
  a.startedAt === b.startedAt ? a.id.localeCompare(b.id) : a.startedAt < b.startedAt ? -1 : 1

export function useSpeech(
  meetingId: string,
  { micOn, deviceId, enabled = true }: { micOn: boolean; deviceId?: string; enabled?: boolean },
): Speech {
  const [list, setList] = useState<SpeechClipList | null>(null)
  const [clips, setClips] = useState<SpeechClip[]>([])
  const [error, setError] = useState<string | null>(null)
  const readAt = useRef<string | null>(null)
  const generation = useRef(0)

  const load = useCallback(
    async (full: boolean) => {
      // Not on the roster: the server would answer 404, and there is nothing to show anyway.
      if (!enabled) return
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
    [meetingId, enabled],
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

  const guest = useGuest()
  const userId = useAuth().user?.id
  const owner = guest ? `guest:${guest.invitationId.toLowerCase()}` : `user:${userId ?? ''}`
  const capturing = !!list?.canCapture && micOn
  const reloadChanged = useCallback(() => void load(false), [load])
  const capture = useMicCapture(meetingId, capturing, deviceId, owner, guest ? reloadChanged : undefined)

  return {
    meetingId,
    list,
    clips,
    error,
    reload: () => void load(true),
    capturing,
    capture,
  }
}
