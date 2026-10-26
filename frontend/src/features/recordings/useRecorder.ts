import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Records the microphone in the browser (SCRUM-29: "ghi trực tiếp từ trình duyệt, có nút tạm dừng
 * và tiếp tục"). Chunks are collected every second, so a tab that crashes mid-meeting loses at most
 * that second rather than the whole take, and switching tabs does not stop the recorder.
 */

export type RecorderState =
  | { kind: 'idle' }
  | { kind: 'starting' }
  | { kind: 'recording' | 'paused'; elapsedMs: number }
  | { kind: 'error'; message: string }

/** Where a take's sound comes from, and how to let go of it afterwards. */
export interface RecordingInput {
  stream: MediaStream
  release: () => void
}

export interface RecordedTake {
  blob: Blob
  durationMs: number
  extension: string
}

/** Opus in WebM where the browser has it (Chrome, Firefox, Edge); AAC in MP4 on Safari. */
const MIME_TYPES = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4', 'audio/ogg;codecs=opus']

function pickMimeType() {
  if (typeof MediaRecorder === 'undefined') return null
  return MIME_TYPES.find((type) => MediaRecorder.isTypeSupported(type)) ?? ''
}

function micErrorMessage(error: unknown) {
  if (error instanceof DOMException) {
    if (error.name === 'NotAllowedError')
      return 'Trình duyệt chưa cho phép dùng micro. Hãy cho phép ở biểu tượng ổ khoá trên thanh địa chỉ.'
    if (error.name === 'NotFoundError') return 'Không tìm thấy micro trên máy này.'
    if (error.name === 'NotReadableError') return 'Micro đang bị ứng dụng khác dùng.'
  }
  return 'Không mở được micro.'
}

/** The microphone on its own: the meeting page, where everyone sits in one room around the Secretary's laptop. */
async function openMic(): Promise<RecordingInput> {
  const stream = await navigator.mediaDevices.getUserMedia({
    audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true, channelCount: 1 },
  })
  return { stream, release: () => stream.getTracks().forEach((track) => track.stop()) }
}

export type Recorder = ReturnType<typeof useRecorder>

export function useRecorder() {
  const [state, setState] = useState<RecorderState>({ kind: 'idle' })
  const recorder = useRef<MediaRecorder | null>(null)
  const input = useRef<RecordingInput | null>(null)
  const chunks = useRef<Blob[]>([])
  // Time spent recording, not counting pauses: the length the file will actually have.
  const activeMs = useRef(0)
  const resumedAt = useRef<number | null>(null)

  const elapsed = () => activeMs.current + (resumedAt.current === null ? 0 : performance.now() - resumedAt.current)

  useEffect(() => {
    if (state.kind !== 'recording') return
    // Only while still recording: a tick that lands between Stop and the re-render would otherwise turn the
    // stopped recorder back into one "recording" at 00:00, for good.
    const timer = window.setInterval(
      () =>
        setState((current) => (current.kind === 'recording' ? { kind: 'recording', elapsedMs: elapsed() } : current)),
      500,
    )
    return () => window.clearInterval(timer)
  }, [state.kind])

  const release = useCallback(() => {
    input.current?.release()
    input.current = null
    recorder.current = null
    resumedAt.current = null
  }, [])

  // Leaving the screen mid-recording must not leave the microphone on.
  useEffect(() => release, [release])

  /** `open` picks the sound: the microphone unless told otherwise (the call room records the whole call). */
  const start = useCallback(
    async (open: () => Promise<RecordingInput> | RecordingInput = openMic) => {
      const mimeType = pickMimeType()
      if (mimeType === null) {
        setState({
          kind: 'error',
          message: 'Trình duyệt này không ghi âm được. Hãy dùng Chrome, Edge, Firefox hoặc Safari mới.',
        })
        return
      }
      setState({ kind: 'starting' })
      try {
        input.current = await open()
        const next = new MediaRecorder(
          input.current.stream,
          mimeType ? { mimeType, audioBitsPerSecond: 64000 } : undefined,
        )
        chunks.current = []
        next.ondataavailable = (event) => {
          if (event.data.size > 0) chunks.current.push(event.data)
        }
        next.start(1000)
        recorder.current = next
        activeMs.current = 0
        resumedAt.current = performance.now()
        setState({ kind: 'recording', elapsedMs: 0 })
      } catch (error) {
        release()
        setState({ kind: 'error', message: micErrorMessage(error) })
      }
    },
    [release],
  )

  const pause = useCallback(() => {
    if (recorder.current?.state !== 'recording') return
    recorder.current.pause()
    activeMs.current = elapsed()
    resumedAt.current = null
    setState({ kind: 'paused', elapsedMs: activeMs.current })
  }, [])

  const resume = useCallback(() => {
    if (recorder.current?.state !== 'paused') return
    recorder.current.resume()
    resumedAt.current = performance.now()
    setState({ kind: 'recording', elapsedMs: activeMs.current })
  }, [])

  /** Stops and hands back the whole take; the microphone is released. */
  const stop = useCallback(
    () =>
      new Promise<RecordedTake | null>((resolve) => {
        const current = recorder.current
        if (!current || current.state === 'inactive') {
          resolve(null)
          return
        }
        const durationMs = elapsed()
        current.onstop = () => {
          const type = current.mimeType || 'audio/webm'
          const blob = new Blob(chunks.current, { type })
          chunks.current = []
          release()
          setState({ kind: 'idle' })
          resolve({
            blob,
            durationMs,
            extension: type.startsWith('audio/mp4') ? 'm4a' : type.startsWith('audio/ogg') ? 'ogg' : 'webm',
          })
        }
        current.stop()
      }),
    [release],
  )

  /** Throws the take away. */
  const discard = useCallback(() => {
    const current = recorder.current
    if (current && current.state !== 'inactive') {
      current.onstop = null
      current.stop()
    }
    chunks.current = []
    release()
    setState({ kind: 'idle' })
  }, [release])

  const active = state.kind === 'recording' || state.kind === 'paused'

  // Closing the tab while recording would lose the meeting: let the browser ask first.
  useEffect(() => {
    if (!active) return
    const warn = (event: BeforeUnloadEvent) => event.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [active])

  /** The take's length right now, for timers set in effects and handlers (not for rendering — use state). */
  const elapsedMs = useCallback(() => elapsed(), [])

  return { state, active, start, pause, resume, stop, discard, elapsedMs, reset: () => setState({ kind: 'idle' }) }
}
