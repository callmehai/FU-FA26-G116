import { useEffect, useRef, useState } from 'react'
import { sendSpeechClip } from '../../api/speech'
import { ApiError } from '../../api/client'
import { isApiError } from '../../lib/errors'
import { ClipCutter, SAMPLE_RATE } from './clipCutter'
import { clipStore } from './clipStore'
import { encodeWav, Resampler } from './wav'
// Always a file of our own origin: Vite would inline a file this small as a data: URL, which the test server's CSP
// (script-src 'self') refuses to run as a worklet.
import speechTapUrl from './speechTap.worklet.js?url&no-inline'

/**
 * SCRUM-109 — captures the signed-in person's own mic and sends what they say a sentence at a time. Nothing here can
 * capture anyone else: the server takes sentences only from their speaker's session.
 *
 * Two things come from AI-eCabinet. A sentence waits on the device (clipStore) until the server has it, so a closed
 * tab or a dropped network loses nothing. And one tab at a time records a person's mic in a meeting (a Web Lock): two
 * tabs of the same person would write every sentence twice.
 */

/** `elsewhere`: another tab of this browser records this meeting; this one can take over. */
export type CaptureState = 'off' | 'starting' | 'listening' | 'speaking' | 'error' | 'elsewhere'

export interface MicCapture {
  state: CaptureState
  /** Why the mic could not be opened, in words for the person. */
  error: string | null
  /** 0–1, for a meter; updated a few times a second. */
  level: number
  /** Sentences waiting to be sent or being sent. */
  waiting: number
  /** Of those, the ones that already failed once and are being retried: something is wrong, not just slow. */
  retrying: number
  /** The server has no transcriber running (MSG312): nothing becomes words until it is back. */
  engineDown: boolean
  /** Sentences the server turned down for good (not an outage, which is retried). */
  refused: number
  /** Record in this tab, stopping the one that does now. */
  takeOver: () => void
}

interface Pending {
  id: string
  meetingId: string
  startedAt: Date
  durationMs: number
  audio: Blob
  attempt: number
  sending: boolean
  dueAt: number
}

const PARALLEL = 2
const MAX_WAIT_MS = 30_000
/** A short meeting holds a few dozen sentences; past this the oldest waiting ones go, not the tab's memory. */
const MAX_QUEUE = 200

/** What the browser said, turned into what to do about it. */
function micError(error: unknown) {
  const name = error instanceof DOMException ? error.name : ''
  if (name === 'NotAllowedError') return 'Trình duyệt chưa cho dùng micro.'
  if (name === 'NotFoundError' || name === 'OverconstrainedError') return 'Không tìm thấy micro.'
  if (name === 'NotReadableError') return 'Micro đang bị ứng dụng khác dùng.'
  return 'Không mở được micro.'
}

/** A server error worth retrying: it may pass. Anything else is the sentence's own fault and never will. */
const retryable = (error: unknown) =>
  !(error instanceof ApiError) || error.status >= 500 || error.status === 408 || error.status === 429

const lockName = (meetingId: string) => `trustcabinet-mic:${meetingId}`

export function useMicCapture(meetingId: string, active: boolean, deviceId?: string): MicCapture {
  const [state, setState] = useState<CaptureState>('off')
  const [error, setError] = useState<string | null>(null)
  const [level, setLevel] = useState(0)
  const [waiting, setWaiting] = useState(0)
  const [retrying, setRetrying] = useState(0)
  const [engineDown, setEngineDown] = useState(false)
  const [refused, setRefused] = useState(0)
  const [claim, setClaim] = useState(0)
  // The queue outlives a capture session: switching the mic off must not drop what was already said.
  const queue = useRef<Pending[]>([])
  const timer = useRef<number | undefined>(undefined)
  const pumpRef = useRef<() => void>(() => undefined)

  useEffect(() => {
    const pump = () => {
      window.clearTimeout(timer.current)
      const now = Date.now()
      for (const item of queue.current) {
        if (queue.current.filter((q) => q.sending).length >= PARALLEL) break
        if (item.sending || item.dueAt > now) continue
        item.sending = true
        sendSpeechClip(item.meetingId, item.id, item.audio, item.startedAt, item.durationMs).then(
          () => {
            setEngineDown(false)
            settle(item)
          },
          (failure: unknown) => {
            if (retryable(failure)) {
              item.sending = false
              item.attempt++
              item.dueAt = Date.now() + Math.min(MAX_WAIT_MS, 1000 * 2 ** (item.attempt - 1))
              setEngineDown(isApiError(failure, 503, 'MSG312'))
              count()
              schedule()
            } else {
              setRefused((n) => n + 1)
              settle(item)
            }
          },
        )
      }
      schedule()
    }
    const settle = (item: Pending) => {
      queue.current = queue.current.filter((q) => q !== item)
      void clipStore.remove(item.id)
      count()
      pump()
    }
    const count = () => {
      setWaiting(queue.current.length)
      setRetrying(queue.current.filter((q) => q.attempt > 0).length)
    }
    const schedule = () => {
      const next = queue.current.filter((q) => !q.sending).reduce((at, q) => Math.min(at, q.dueAt), Infinity)
      if (next !== Infinity) timer.current = window.setTimeout(pump, Math.max(0, next - Date.now()))
    }
    pumpRef.current = pump
    let gone = false
    // What an earlier visit could not send, on this meeting or another: the server tells which it still takes.
    void clipStore.all().then((stored) => {
      if (gone) return
      const known = new Set(queue.current.map((q) => q.id))
      for (const clip of stored)
        if (!known.has(clip.id))
          queue.current.push({
            id: clip.id,
            meetingId: clip.meetingId,
            startedAt: new Date(clip.startedAt),
            durationMs: clip.durationMs,
            audio: clip.audio,
            attempt: 0,
            sending: false,
            dueAt: 0,
          })
      count()
      pump()
    })
    pump()
    return () => {
      gone = true
      window.clearTimeout(timer.current)
    }
  }, [meetingId])

  useEffect(() => {
    if (!active) return
    let stopped = false
    let stream: MediaStream | undefined
    let context: AudioContext | undefined
    const cutter = new ClipCutter()
    let anchor = 0
    let lastLevelAt = 0

    const enqueue = (clips: ReturnType<ClipCutter['push']>) => {
      for (const clip of clips) {
        const item: Pending = {
          id: crypto.randomUUID(),
          meetingId,
          startedAt: new Date(anchor + (clip.startSample / SAMPLE_RATE) * 1000),
          durationMs: (clip.samples.length / SAMPLE_RATE) * 1000,
          audio: encodeWav(clip.samples, SAMPLE_RATE),
          attempt: 0,
          sending: false,
          dueAt: 0,
        }
        queue.current.push(item)
        void clipStore.put({
          id: item.id,
          meetingId,
          startedAt: item.startedAt.toISOString(),
          durationMs: item.durationMs,
          audio: item.audio,
        })
        if (queue.current.length > MAX_QUEUE) {
          for (const dropped of queue.current.slice(0, -MAX_QUEUE)) void clipStore.remove(dropped.id)
          queue.current = queue.current.slice(-MAX_QUEUE)
        }
      }
      if (clips.length > 0) {
        setWaiting(queue.current.length)
        pumpRef.current()
      }
    }

    async function start() {
      if (stopped) return
      setState('starting')
      setError(null)
      try {
        // Echo cancellation keeps the call's loudspeaker out; automatic gain stays off, or a quiet owner's mic is
        // lifted to the level of the neighbour it overhears (AI-eCabinet, crosstalk between personal mics).
        stream = await navigator.mediaDevices.getUserMedia({
          audio: {
            ...(deviceId ? { deviceId: { exact: deviceId } } : {}),
            echoCancellation: true,
            autoGainControl: false,
            noiseSuppression: true,
            channelCount: 1,
          },
        })
        if (stopped) return
        context = new AudioContext()
        await context.audioWorklet.addModule(speechTapUrl)
        if (stopped) return
        const source = context.createMediaStreamSource(stream)
        const tap = new AudioWorkletNode(context, 'speech-tap')
        const resampler = new Resampler(context.sampleRate, SAMPLE_RATE)
        anchor = Date.now()
        tap.port.onmessage = (event: MessageEvent<Float32Array>) => {
          enqueue(cutter.push(resampler.push(event.data)))
          const now = performance.now()
          if (now - lastLevelAt > 120) {
            lastLevelAt = now
            setLevel(Math.max(0, Math.min(1, (cutter.lastDb + 60) / 50)))
            setState(cutter.speaking ? 'speaking' : 'listening')
          }
        }
        source.connect(tap)
        setState('listening')
      } catch (failure) {
        if (stopped) return
        setError(micError(failure))
        setState('error')
      }
    }

    let closed = false
    const close = () => {
      if (closed) return
      closed = true
      // Whatever was being said when the mic went off is still a sentence.
      enqueue(cutter.flush())
      stream?.getTracks().forEach((track) => track.stop())
      void context?.close()
    }

    let release: (() => void) | undefined
    const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
    if (!locks) void Promise.resolve().then(start)
    else
      locks
        .request(lockName(meetingId), claim > 0 ? { steal: true } : { ifAvailable: true }, (lock) => {
          if (stopped) return
          if (!lock) {
            setState('elsewhere')
            return
          }
          void start()
          // Held for as long as this tab records.
          return new Promise<void>((resolve) => (release = resolve))
        })
        .catch(() => {
          // Another tab of this person took the mic over: this one stops and says so.
          if (stopped) return
          close()
          setState('elsewhere')
        })

    return () => {
      stopped = true
      close()
      release?.()
    }
  }, [active, deviceId, meetingId, claim])

  return {
    state: active ? state : 'off',
    error,
    level: active && state !== 'elsewhere' ? level : 0,
    waiting,
    retrying,
    engineDown,
    refused,
    takeOver: () => setClaim((n) => n + 1),
  }
}
