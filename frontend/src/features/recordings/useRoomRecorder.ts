import { useCallback, useEffect, useRef, useState } from 'react'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { RoomTakeSaved } from '../../types/recording'
import { RoomMic, saveTake, sendPart } from './roomMic'
import { holdRoomMic } from './roomMicHeld'
import { RoomTape, TapeClosed, TapeRefused, type SavedTape } from './roomTape'
import { tapeStore } from './tapeStore'
import { DEFAULT_PURPOSE } from './useRecordingSave'

/**
 * ADR-0041 — the Secretary's room mic. One switch: on opens a take, off saves it into the meeting's room recording,
 * which is transcribed again as a whole. A take this device kept from a session cut off (a closed tab, a crash) is
 * offered to carry on or to save. It stops on its own when the meeting is no longer running, and when it unmounts.
 *
 * ADR-0072 — held above the routes (RoomMicProvider), so moving between the meeting page and its call room in one tab
 * keeps it on; in a hybrid meeting the call room sends its {@link RoomRecorder.track} to the people online.
 */

export type RoomMicPhase = 'off' | 'starting' | 'on' | 'saving'

/** One tab of this browser records the room at a time: two would open two takes of the same voices. */
const lockName = (meetingId: string) => `trustcabinet-room-mic:${meetingId}`

/**
 * ADR-0085 — how long a call room taking the room mic over waits for the tab holding it to save its take and let go.
 * Saving sends what is left of the take first, so it is a few seconds on a slow network.
 */
const TAKE_OVER_MS = 20_000

/** The call room asks the other tabs of this browser for the room mic: the one holding it switches it off. */
function askToRelease(meetingId: string) {
  if (typeof BroadcastChannel === 'undefined') return
  const channel = new BroadcastChannel(lockName(meetingId))
  channel.postMessage('release')
  channel.close()
}

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
  const [track, setTrack] = useState<MediaStreamTrack | null>(null)
  // The take open on the server is fed by another tab of this browser (the call room's, say), not another device.
  const [otherTab, setOtherTab] = useState(false)
  const [unfinished, setUnfinished] = useState<SavedTape | null>(null)
  const [purpose, setPurpose] = useState(DEFAULT_PURPOSE)
  const mic = useRef<RoomMic | null>(null)
  // The mic while the browser still asks for it: leaving the page then must stop it too, once it opens.
  const opening = useRef<RoomMic | null>(null)
  const gone = useRef(false)
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
    const elsewhere = await heldByAnotherTab(meetingId)
    setOtherTab(elsewhere)
    setUnfinished(saved && !elsewhere ? saved : null)
  }, [meetingId])
  useEffect(() => {
    void Promise.resolve().then(look)
  }, [look])

  // A take saved on its own every few minutes while the mic stays on is not announced: the switch never moved.
  const said = useCallback(
    (saved: RoomTakeSaved | null, rolled = false) => {
      if (saved && !rolled) toast.show(saved.message, saved.recordingId ? 'success' : 'info')
      changedRef.current()
    },
    [toast],
  )

  const run = useCallback(
    async (consent: boolean, saved?: SavedTape, deviceId?: string, takeOver = false) => {
      setError(null)
      setNotice(null)
      setPhase('starting')
      // The meeting has one open take: the last one is saved before the next opens.
      await saving.current
      const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
      if (locks) {
        const take = (options: LockOptions) =>
          new Promise<boolean>((resolve) => {
            void locks
              .request(lockName(meetingId), options, (lock) => {
                resolve(lock !== null)
                if (!lock) return
                // Held for as long as this tab records.
                return new Promise<void>((done) => (release.current = done))
              })
              .catch(() => resolve(false))
          })
        let got = await take({ ifAvailable: true })
        // ADR-0085: the call room is where a hybrid meeting's room mic lives. Another tab of this browser holding it (a
        // call room the call moved away from, a meeting page that recorded while the call could not be joined) is asked
        // to switch it off, and the mic opens here once that tab has saved its take.
        if (!got && takeOver) {
          askToRelease(meetingId)
          got = await take({ signal: AbortSignal.timeout(TAKE_OVER_MS) })
        }
        if (!got) {
          setError('Mic phòng đang bật ở một tab khác của trình duyệt này.')
          setPhase('off')
          return
        }
      }
      // The page was left while the last take was saved or the lock was asked for: no mic is opened for nobody.
      if (gone.current) {
        release.current?.()
        return
      }
      const next = new RoomMic(meetingId, () => purposeRef.current.trim() || DEFAULT_PURPOSE, tapeStore, {
        onLost: (message, failed) => {
          // A part turned down is said even when it was the last one, sent after the switch went off.
          if (failed) setError(message)
          if (mic.current !== next) return
          mic.current = null
          release.current?.()
          setStartedAt(null)
          setTrack(null)
          setPhase('off')
          if (!failed) setNotice(message)
          void look()
        },
        onSaved: said,
      })
      opening.current = next
      try {
        await next.start(consent, saved, deviceId)
      } catch (failure) {
        if (opening.current !== next) return
        opening.current = null
        release.current?.()
        setError(errorMessage(failure))
        setPhase('off')
        changedRef.current()
        return
      }
      // Left while the browser asked for the mic: the unmount stopped it, so it let the mic go as it opened.
      if (opening.current !== next) return
      opening.current = null
      mic.current = next
      setUnfinished(null)
      setTrack(next.track())
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
    setTrack(null)
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
      const refused = await RoomTape.resume(saved, { send: sendPart(saved.recordingId), store: tapeStore })
        .finish()
        .then(
          () => null,
          (failure: unknown) => {
            if (failure instanceof TapeRefused) return failure
            throw failure
          },
        )
      said(await saveTake(saved.recordingId))
      // A part turned down: what the server took before it is saved, the rest on the device never will be.
      if (refused) setError(`${errorMessage(refused.cause)} Phần còn trên máy không gửi được.`)
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

  /** Another mic picked in the call room while this one records: the take goes on from it. */
  const switchDevice = useCallback(async (deviceId: string) => {
    const current = mic.current
    if (!current) return
    try {
      await current.switchDevice(deviceId)
      if (mic.current === current) setTrack(current.track())
    } catch (failure) {
      toast.show(errorMessage(failure), 'error')
    }
  }, [toast])

  // Read by whoever draws the meter, a few times a second: polled there, so the page holding the mic is not drawn
  // again at that pace (the call room would be).
  const meter = useCallback(
    () => ({ level: mic.current?.level() ?? 0, behindSeconds: mic.current?.behindSeconds() ?? 0 }),
    [],
  )

  // ADR-0085: a call room of this browser takes the room mic over; this tab lets it go, saving its take.
  useEffect(() => {
    if (typeof BroadcastChannel === 'undefined') return
    const channel = new BroadcastChannel(lockName(meetingId))
    channel.onmessage = (event: MessageEvent) => {
      if (event.data === 'release') stop()
    }
    return () => channel.close()
  }, [meetingId, stop])

  // The meeting ended while the mic was on: the take is saved.
  useEffect(() => {
    if (phase === 'on' && !running) stop()
  }, [phase, running, stop])

  // A reload or a closed tab cuts the mic off; the browser asks first. What it recorded waits on the device.
  const holding = phase === 'on' || phase === 'saving'
  useEffect(() => {
    holdRoomMic(meetingId, holding)
    return () => holdRoomMic(meetingId, false)
  }, [meetingId, holding])
  useEffect(() => {
    if (!holding) return
    const warn = (event: BeforeUnloadEvent) => event.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [holding])

  // Leaving the page with the mic on saves the take, as switching it off would; one still opening never opens.
  useEffect(() => {
    gone.current = false
    return () => {
      gone.current = true
      const current = mic.current ?? opening.current
      mic.current = null
      opening.current = null
      const done = release.current
      void current
        ?.stop()
        .catch(() => undefined)
        .finally(() => done?.())
    }
  }, [])

  return {
    phase,
    error,
    notice,
    startedAt,
    /** The level (0–1) and the seconds recorded but not on the server yet, past the part still filling. */
    meter,
    /** The open mic while it is on, for the call room to send to the people online in a hybrid meeting. */
    track,
    otherTab,
    /** Looks again whether a take was kept on this device, and whether another tab of it records. */
    recheck: look,
    unfinished,
    purpose,
    setPurpose,
    /**
     * `consent`: the participants were told just now; asked before the meeting's first take only. `deviceId`: the mic
     * picked in the call room. `takeOver`: the call room's switch, which has another tab of this browser holding the
     * mic let it go rather than refusing (ADR-0085).
     */
    start: (consent: boolean, deviceId?: string, takeOver?: boolean) => void run(consent, undefined, deviceId, takeOver),
    switchDevice: (deviceId: string) => void switchDevice(deviceId),
    stop,
    resumeUnfinished: (deviceId?: string, takeOver?: boolean) => {
      if (unfinished) void run(false, unfinished, deviceId, takeOver)
    },
    finishUnfinished: () => void finishUnfinished(),
  }
}

export type RoomRecorder = ReturnType<typeof useRoomRecorder>
