import { useCallback, useEffect, useRef, useState } from 'react'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { RoomTakeSaved } from '../../types/recording'
import { RoomMic, saveTake, sendPart } from './roomMic'
import { RoomTape, TapeClosed, type SavedTape } from './roomTape'
import { tapeStore } from './tapeStore'
import { DEFAULT_PURPOSE } from './useRecordingSave'

/**
 * ADR-0041 — the Secretary's room mic, held by the meeting page so that moving between its tabs never stops it. One
 * switch: on opens a take, off saves it into the meeting's room recording, which is transcribed again as a whole. A
 * take this device kept from a session cut off (a closed tab, a crash) is offered to carry on or to save. It stops on
 * its own when the meeting is no longer running, and when the page is left.
 */

export type RoomMicPhase = 'off' | 'starting' | 'on' | 'saving'

/** One tab of this browser records the room at a time: two would open two takes of the same voices. */
const lockName = (meetingId: string) => `trustcabinet-room-mic:${meetingId}`

async function heldByAnotherTab(meetingId: string) {
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
  if (!locks) return false
  const state = await locks.query().catch(() => null)
  return state?.held?.some((lock) => lock.name === lockName(meetingId)) ?? false
}

/** `onChanged`: a take opened or was saved, so the recordings on screen are read again. */
export function useRoomRecorder(meetingId: string, running: boolean, onChanged: () => Promise<void> | void) {
  const toast = useToast()
  const [phase, setPhase] = useState<RoomMicPhase>('off')
  const [error, setError] = useState<string | null>(null)
  // The mic stopped on its own and nothing was lost: the take was saved, here or elsewhere. Said, not alarmed.
  const [notice, setNotice] = useState<string | null>(null)
  const [startedAt, setStartedAt] = useState<number | null>(null)
  const [level, setLevel] = useState(0)
  const [behind, setBehind] = useState(0)
  const [unfinished, setUnfinished] = useState<SavedTape | null>(null)
  const [purpose, setPurpose] = useState(DEFAULT_PURPOSE)
  const mic = useRef<RoomMic | null>(null)
  const release = useRef<(() => void) | null>(null)
  const saving = useRef<Promise<void> | null>(null)
  const purposeRef = useRef(purpose)
  const changedRef = useRef(onChanged)
  useEffect(() => {
    purposeRef.current = purpose
    changedRef.current = onChanged
  })

  // A take this device kept, unless another tab of it is recording that take right now.
  const look = useCallback(async () => {
    const saved = await tapeStore.load(meetingId)
    setUnfinished(saved && !(await heldByAnotherTab(meetingId)) ? saved : null)
  }, [meetingId])
  useEffect(() => {
    void Promise.resolve().then(look)
  }, [look])

  const said = useCallback(
    (saved: RoomTakeSaved | null) => {
      if (saved) toast.show(saved.message, saved.recordingId ? 'success' : 'info')
      changedRef.current()
    },
    [toast],
  )

  const run = useCallback(
    async (consent: boolean, saved?: SavedTape) => {
      setError(null)
      setNotice(null)
      setPhase('starting')
      // The meeting has one open take: the last one is saved before the next opens.
      await saving.current
      const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
      if (locks) {
        const got = await new Promise<boolean>((resolve) => {
          void locks
            .request(lockName(meetingId), { ifAvailable: true }, (lock) => {
              resolve(lock !== null)
              if (!lock) return
              // Held for as long as this tab records.
              return new Promise<void>((done) => (release.current = done))
            })
            .catch(() => resolve(false))
        })
        if (!got) {
          setError('Mic phòng đang bật ở một tab khác của trình duyệt này.')
          setPhase('off')
          return
        }
      }
      const next = new RoomMic(meetingId, () => purposeRef.current.trim() || DEFAULT_PURPOSE, tapeStore, {
        onLost: (message) => {
          if (mic.current !== next) return
          mic.current = null
          release.current?.()
          setStartedAt(null)
          setPhase('off')
          setNotice(message)
          void look()
        },
        onSaved: said,
      })
      try {
        await next.start(consent, saved)
      } catch (failure) {
        release.current?.()
        setError(errorMessage(failure))
        setPhase('off')
        changedRef.current()
        return
      }
      mic.current = next
      setUnfinished(null)
      setStartedAt(Date.now())
      setPhase('on')
      changedRef.current()
    },
    [meetingId, said, look],
  )

  // Off in the click: the mic is cut there and then; the rest goes up and the take is saved on its own.
  const stop = useCallback(() => {
    const current = mic.current
    if (!current) return
    mic.current = null
    setStartedAt(null)
    setLevel(0)
    setBehind(0)
    setPhase('saving')
    saving.current = current
      .stop()
      .catch((failure) => setError(`Chưa lưu được lượt ghi: ${errorMessage(failure)}`))
      // Read again before the switch shows off: the take just saved must not look like one left open elsewhere.
      .then(() => changedRef.current())
      .finally(() => {
        release.current?.()
        saving.current = null
        setPhase((p) => (p === 'saving' ? 'off' : p))
      })
  }, [])

  /** Saves the take this device kept, without the mic: what it holds goes up, then the take is saved. */
  const finishUnfinished = useCallback(async () => {
    const saved = unfinished
    if (!saved) return
    setError(null)
    setPhase('saving')
    try {
      await RoomTape.resume(saved, { send: sendPart(saved.recordingId), store: tapeStore }).finish()
      said(await saveTake(saved.recordingId))
    } catch (failure) {
      // Saved elsewhere meanwhile (another Secretary, or the server after hours): nothing is left to add.
      setError(
        failure instanceof TapeClosed
          ? 'Lượt ghi dở đã được lưu ở nơi khác, phần còn trên máy không thêm vào được.'
          : `Chưa lưu được lượt ghi dở: ${errorMessage(failure)}`,
      )
    }
    await look()
    await changedRef.current()
    setPhase('off')
  }, [unfinished, said, look])

  // The meter and the "still sending" notice, a few times a second.
  useEffect(() => {
    if (phase !== 'on') return
    const timer = window.setInterval(() => {
      setLevel(mic.current?.level() ?? 0)
      setBehind(mic.current?.behindSeconds() ?? 0)
    }, 125)
    return () => window.clearInterval(timer)
  }, [phase])

  // The meeting ended while the mic was on: the take is saved.
  useEffect(() => {
    if (phase === 'on' && !running) stop()
  }, [phase, running, stop])

  // A reload or a closed tab cuts the mic off; the browser asks first. What it recorded waits on the device.
  const holding = phase === 'on' || phase === 'saving'
  useEffect(() => {
    if (!holding) return
    const warn = (event: BeforeUnloadEvent) => event.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [holding])

  // Leaving the page with the mic on saves the take, as switching it off would.
  useEffect(
    () => () => {
      const current = mic.current
      mic.current = null
      const done = release.current
      void current
        ?.stop()
        .catch(() => undefined)
        .finally(() => done?.())
    },
    [],
  )

  return {
    phase,
    error,
    notice,
    startedAt,
    level: phase === 'on' ? level : 0,
    /** Seconds recorded but not on the server yet, past the part still filling. */
    behindSeconds: behind,
    unfinished,
    purpose,
    setPurpose,
    /** `consent`: the participants were told just now; asked before the meeting's first take only. */
    start: (consent: boolean) => void run(consent),
    stop,
    resumeUnfinished: () => {
      if (unfinished) void run(false, unfinished)
    },
    finishUnfinished: () => void finishUnfinished(),
  }
}

export type RoomRecorder = ReturnType<typeof useRoomRecorder>
