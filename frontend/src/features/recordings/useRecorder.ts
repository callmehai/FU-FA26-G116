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

export function useRecorder() {
  const [state, setState] = useState<RecorderState>({ kind: 'idle' })
  const recorder = useRef<MediaRecorder | null>(null)
  const chunks = useRef<Blob[]>([])
  // Time spent recording, not counting pauses: the length the file will actually have.
  const activeMs = useRef(0)
  const resumedAt = useRef<number | null>(null)

  const elapsed = () => activeMs.current + (resumedAt.current === null ? 0 : performance.now() - resumedAt.current)

  useEffect(() => {
    if (state.kind !== 'recording') return
    const timer = window.setInterval(() => setState({ kind: 'recording', elapsedMs: elapsed() }), 500)
    return () => window.clearInterval(timer)
  }, [state.kind])

  const release = useCallback(() => {
    recorder.current?.stream.getTracks().forEach((track) => track.stop())
    recorder.current = null
    resumedAt.current = null
  }, [])

  // Leaving the screen mid-recording must not leave the microphone on.
  useEffect(() => release, [release])

  const start = useCallback(async () => {
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
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true, channelCount: 1 },
      })
      const next = new MediaRecorder(stream, mimeType ? { mimeType, audioBitsPerSecond: 64000 } : undefined)
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
  }, [release])

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

  return { state, active, start, pause, resume, stop, discard, reset: () => setState({ kind: 'idle' }) }
}
