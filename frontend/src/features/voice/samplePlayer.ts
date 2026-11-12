import { useCallback, useEffect, useRef, useState } from 'react'
import { voiceSampleAudio } from '../../api/voice'

/**
 * One player for a list of voice samples, fetched with the caller's session and gone when the page closes. The owner
 * hears their own (the default fetch); a meeting's Secretary passes the collection's, which the server audits (ADR-0060).
 */
export function useSamplePlayer(fetchAudio: (sampleId: string) => Promise<Blob> = voiceSampleAudio) {
  const [playing, setPlaying] = useState<string | null>(null)
  const audio = useRef<HTMLAudioElement | null>(null)
  const url = useRef<string | null>(null)

  const stop = useCallback(() => {
    audio.current?.pause()
    if (url.current) URL.revokeObjectURL(url.current)
    url.current = null
    setPlaying(null)
  }, [])
  useEffect(() => stop, [stop])

  const toggle = async (sampleId: string) => {
    const wasPlaying = playing === sampleId
    stop()
    if (wasPlaying) return
    const blob = await fetchAudio(sampleId)
    url.current = URL.createObjectURL(blob)
    const el = (audio.current ??= new Audio())
    el.onended = stop
    el.src = url.current
    setPlaying(sampleId)
    await el.play()
  }

  return { playing, toggle, stop }
}
