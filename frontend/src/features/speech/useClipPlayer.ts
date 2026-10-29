import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { getSpeechClipPlayback } from '../../api/speech'
import { errorMessage } from '../../lib/errors'
import type { SpeechClip } from '../../types/speech'
import { rememberSpeed, storedSpeed } from '../recordings/playerSpeed'

/**
 * ADR-0039 — the per-mic record heard as one track, as AI-eCabinet's timeline player does. Silence between sentences
 * is cut out: the track is the spoken time only, so the total and the position count speech, not the meeting clock.
 * eCab mixes sentences said at the same time; one audio element plays one at a time, so here they follow each other
 * and nothing anyone said is lost. A sentence another mic heard louder (wholly someone else's) is that person's
 * sentence again and is left out of the track. Each sentence's audio is fetched on demand through its signed link;
 * one out of the record, or left out, plays alone from its own ▶ and never runs on into the track.
 */

/** Where one sentence sits on the spoken track, in ms. */
export interface TrackPlace {
  clip: SpeechClip
  at: number
}

export interface ClipPlayer {
  places: TrackPlace[]
  /** Where each sentence starts on the track, by id; missing for one that cannot be heard. */
  placeOf: Map<string, number>
  totalMs: number
  positionMs: number
  playing: boolean
  /** The sentence being heard. */
  currentId: string | null
  error: string | null
  /** Loudness over the whole track in `buckets` steps, 0–1, for the waveform. */
  waveform: number[]
  toggle: () => void
  seek: (ms: number) => void
  /** From this sentence on; one out of the record plays alone. */
  playFrom: (clip: SpeechClip) => void
  speed: number
  setSpeed: (speed: number) => void
  volume: number
  setVolume: (volume: number) => void
  muted: boolean
  setMuted: (muted: boolean) => void
}

const BUCKETS = 400
/** A link is fetched again this long before it expires. */
const LINK_MARGIN_MS = 60_000

const decode = (peaks: string) => Uint8Array.from(atob(peaks), (c) => c.charCodeAt(0))

/** Lays the sentences end to end in the order they were said, without the silence between them. */
export function layTrack(clips: SpeechClip[]) {
  const places: TrackPlace[] = []
  let trackEnd = 0
  for (const clip of clips) {
    if (!clip.hasAudio || clip.excluded || clip.leak?.whole) continue
    places.push({ clip, at: trackEnd })
    trackEnd += clip.durationMs
  }
  return { places, totalMs: trackEnd }
}

export function useClipPlayer(clips: SpeechClip[]): ClipPlayer {
  const { places, totalMs } = useMemo(() => layTrack(clips), [clips])
  const placeOf = useMemo(() => new Map(places.map((p) => [p.clip.id, p.at])), [places])

  const audio = useRef<HTMLAudioElement | null>(null)
  const links = useRef(new Map<string, { url: string; until: number }>())
  // What is loaded into the element: a place on the track, or a sentence out of the record played alone.
  const loaded = useRef<{ clip: SpeechClip; at: number | null } | null>(null)
  const request = useRef(0)
  const [positionMs, setPositionMs] = useState(0)
  const [playing, setPlaying] = useState(false)
  const [currentId, setCurrentId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [speed, setSpeedState] = useState(storedSpeed)
  const [volume, setVolumeState] = useState(1)
  const [muted, setMutedState] = useState(false)
  const settings = useRef({ speed, volume, muted })
  useLayoutEffect(() => {
    settings.current = { speed, volume, muted }
  }, [speed, volume, muted])

  const element = useCallback(() => {
    if (!audio.current) {
      const created = new Audio()
      created.preload = 'auto'
      audio.current = created
    }
    return audio.current
  }, [])

  const link = useCallback(async (clipId: string) => {
    const known = links.current.get(clipId)
    if (known && known.until - LINK_MARGIN_MS > Date.now()) return known.url
    const signed = await getSpeechClipPlayback(clipId)
    links.current.set(clipId, { url: signed.url, until: Date.parse(signed.expiresAt) })
    return signed.url
  }, [])

  const placesRef = useRef(places)
  useLayoutEffect(() => {
    placesRef.current = places
  }, [places])

  /** Plays `clip` from `offset` ms into it; `at` is its place on the track, null when it plays alone. */
  const start = useCallback(
    async (clip: SpeechClip, at: number | null, offset: number) => {
      const mine = ++request.current
      const el = element()
      setError(null)
      setCurrentId(clip.id)
      try {
        const url = await link(clip.id)
        if (mine !== request.current) return
        if (loaded.current?.clip.id !== clip.id || el.src !== new URL(url, location.href).href) el.src = url
        loaded.current = { clip, at }
        el.playbackRate = settings.current.speed
        el.volume = settings.current.volume
        el.muted = settings.current.muted
        el.currentTime = Math.max(0, offset) / 1000
        await el.play()
        if (mine === request.current) setPlaying(true)
      } catch (failure) {
        if (mine !== request.current) return
        setPlaying(false)
        setError(
          failure instanceof DOMException ? 'Không phát được đoạn này. Thử lại sau giây lát.' : errorMessage(failure),
        )
      }
    },
    [element, link],
  )

  /** The sentence that holds `ms` on the track, or the next one. */
  const placeAt = useCallback((ms: number) => {
    const list = placesRef.current
    return list.find((p) => ms >= p.at && ms < p.at + p.clip.durationMs) ?? list.find((p) => p.at >= ms) ?? null
  }, [])

  const seek = useCallback(
    (ms: number) => {
      const clamped = Math.max(0, Math.min(totalMs, ms))
      setPositionMs(clamped)
      const place = placeAt(clamped)
      if (!place) return
      if (playing) void start(place.clip, place.at, clamped - place.at)
      else {
        request.current++
        loaded.current = null
        setCurrentId(place.clip.id)
      }
    },
    [placeAt, playing, start, totalMs],
  )

  const toggle = useCallback(() => {
    const el = element()
    if (playing) {
      request.current++
      el.pause()
      setPlaying(false)
      return
    }
    const current = loaded.current
    // Paused inside the same sentence: carry on from there.
    if (current && current.clip.id === currentId && el.src) {
      el.playbackRate = settings.current.speed
      void el.play().then(
        () => setPlaying(true),
        () => setError('Không phát được đoạn này. Thử lại sau giây lát.'),
      )
      return
    }
    const from = positionMs >= totalMs ? 0 : positionMs
    const place = placeAt(from)
    if (place) void start(place.clip, place.at, from - place.at)
  }, [currentId, element, placeAt, playing, positionMs, start, totalMs])

  const playFrom = useCallback(
    (clip: SpeechClip) => {
      const at = placeOf.get(clip.id)
      if (at !== undefined) {
        setPositionMs(at)
        void start(clip, at, 0)
      } else if (clip.hasAudio) void start(clip, null, 0)
    },
    [placeOf, start],
  )

  // Follows the element: the position moves while it plays, and the next sentence starts when one ends.
  useEffect(() => {
    const el = element()
    let frame = 0
    let shownAt = 0
    // Ten steps a second move the playhead smoothly enough without redrawing a long record every frame.
    const tick = (now: number) => {
      const current = loaded.current
      if (current?.at != null && now - shownAt >= 100) {
        shownAt = now
        setPositionMs(current.at + el.currentTime * 1000)
      }
      frame = requestAnimationFrame(tick)
    }
    const onPlay = () => {
      cancelAnimationFrame(frame)
      frame = requestAnimationFrame(tick)
    }
    const onPause = () => cancelAnimationFrame(frame)
    const onEnded = () => {
      cancelAnimationFrame(frame)
      const current = loaded.current
      const list = placesRef.current
      const index = current?.at != null ? list.findIndex((p) => p.clip.id === current.clip.id) : -1
      const next = index >= 0 ? list[index + 1] : undefined
      if (next) {
        setPositionMs(next.at)
        void start(next.clip, next.at, 0)
        // The one after is fetched while this one plays, so the gap between them stays short.
        const after = list[index + 2]
        if (after) void link(after.clip.id).catch(() => undefined)
      } else {
        setPlaying(false)
        if (current?.at != null) setPositionMs(current.at + current.clip.durationMs)
      }
    }
    el.addEventListener('play', onPlay)
    el.addEventListener('pause', onPause)
    el.addEventListener('ended', onEnded)
    return () => {
      cancelAnimationFrame(frame)
      el.removeEventListener('play', onPlay)
      el.removeEventListener('pause', onPause)
      el.removeEventListener('ended', onEnded)
    }
  }, [element, link, start])

  // Leaving the page stops the sound.
  useEffect(
    () => () => {
      request.current++
      audio.current?.pause()
    },
    [],
  )

  const waveform = useMemo(() => {
    const bars = new Array<number>(BUCKETS).fill(0)
    if (totalMs <= 0) return bars
    for (const { clip, at } of places) {
      if (!clip.peaks) continue
      const bytes = decode(clip.peaks)
      bytes.forEach((value, i) => {
        const bucket = Math.min(BUCKETS - 1, Math.floor(((at + i * 100) / totalMs) * BUCKETS))
        bars[bucket] = Math.max(bars[bucket], value)
      })
    }
    const loudest = Math.max(1, ...bars)
    return bars.map((v) => v / loudest)
  }, [places, totalMs])

  return {
    places,
    placeOf,
    totalMs,
    positionMs: Math.min(positionMs, totalMs),
    playing,
    currentId,
    error,
    waveform,
    toggle,
    seek,
    playFrom,
    speed,
    setSpeed: (value) => {
      setSpeedState(value)
      if (audio.current) audio.current.playbackRate = value
      rememberSpeed(value)
    },
    volume,
    setVolume: (value) => {
      setVolumeState(value)
      if (audio.current) audio.current.volume = value
      if (value > 0 && muted) {
        setMutedState(false)
        if (audio.current) audio.current.muted = false
      }
    },
    muted,
    setMuted: (value) => {
      setMutedState(value)
      if (audio.current) audio.current.muted = value
    },
  }
}
