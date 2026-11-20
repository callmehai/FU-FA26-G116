/**
 * The call room's short cues, as Google Meet plays them: someone joined, someone started presenting (Hải 20/11). They
 * are synthesised with Web Audio rather than shipped as files, so nothing new has to be allowed by the server's
 * Content-Security-Policy, and they play on the speaker the user picked in the room.
 */

export type RoomCue = 'join' | 'share'

/** Notes per cue, in Hz: two rising notes for a newcomer, one lower falling pair for a screen being shared. */
const NOTES: Record<RoomCue, readonly number[]> = {
  join: [659.25, 987.77],
  share: [783.99, 587.33],
}

const NOTE_SECONDS = 0.16
const GAP_SECONDS = 0.07
const VOLUME = 0.12
/** Ten people walking in together sound once, not ten times. */
const MIN_INTERVAL_MS = 1500

let context: AudioContext | null = null
let sinkId: string | undefined
const lastPlayed: Partial<Record<RoomCue, number>> = {}

type SinkableContext = AudioContext & { setSinkId?: (id: string) => Promise<void> }

function audio(): AudioContext | null {
  if (context) return context
  const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  if (!Ctor) return null
  context = new Ctor()
  route(context)
  return context
}

function route(ctx: AudioContext) {
  const sink = ctx as SinkableContext
  if (sinkId && sink.setSinkId) void sink.setSinkId(sinkId === 'default' ? '' : sinkId).catch(() => undefined)
}

/** Follows the room's speaker choice where the browser can route Web Audio (Chrome, Edge); elsewhere the default plays. */
export function setCueOutput(deviceId: string | undefined) {
  if (!deviceId || deviceId === sinkId) return
  sinkId = deviceId
  if (context) route(context)
}

export function playCue(cue: RoomCue, now = Date.now()) {
  const last = lastPlayed[cue]
  if (last !== undefined && now - last < MIN_INTERVAL_MS) return
  lastPlayed[cue] = now
  const ctx = audio()
  if (!ctx) return
  // The room opens after a click, so the context may start; a cue that still cannot play is simply skipped.
  if (ctx.state === 'suspended') void ctx.resume().catch(() => undefined)

  const start = ctx.currentTime + 0.02
  NOTES[cue].forEach((frequency, i) => {
    const at = start + i * (NOTE_SECONDS + GAP_SECONDS)
    const tone = ctx.createOscillator()
    const gain = ctx.createGain()
    tone.type = 'sine'
    tone.frequency.setValueAtTime(frequency, at)
    // A soft attack and a quick decay: a chime, not a beep.
    gain.gain.setValueAtTime(0.0001, at)
    gain.gain.exponentialRampToValueAtTime(VOLUME, at + 0.015)
    gain.gain.exponentialRampToValueAtTime(0.0001, at + NOTE_SECONDS)
    tone.connect(gain).connect(ctx.destination)
    tone.start(at)
    tone.stop(at + NOTE_SECONDS + 0.02)
  })
}
