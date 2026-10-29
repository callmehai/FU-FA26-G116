/**
 * SCRUM-109 — cuts one person's mic into sentences by loudness alone. No speech model runs in the browser: the test
 * server's CSP blocks WebAssembly, and a personal mic held close needs nothing cleverer than "louder than the room".
 * The numbers follow AI-eCabinet's cutter, except the pause that ends a sentence: a sentence here, not a whole turn.
 */

export const SAMPLE_RATE = 16_000
const FRAME = SAMPLE_RATE / 50 // 20 ms
const FRAME_MS = 20

export interface CutterSettings {
  /** Silence that ends a sentence. */
  pauseMs: number
  /** Past this, the next short breath ends the sentence. */
  softMaxMs: number
  /** Never longer than this: the worker takes a minute at most. */
  hardMaxMs: number
}

export const DEFAULT_CUTTER: CutterSettings = { pauseMs: 1200, softMaxMs: 30_000, hardMaxMs: 40_000 }

/** One sentence: its samples at 16 kHz, and where it began, counted in samples since capture started. */
export interface Clip {
  startSample: number
  samples: Float32Array
}

const SPEECH_OVER_FLOOR_DB = 9
const SPEECH_MIN_DB = -50
const OPEN_FRAMES = 3 // 60 ms of speech opens a sentence
const PRE_ROLL_FRAMES = 15 // the 300 ms before it, so the first syllable is not cut
const TAIL_FRAMES = 10 // 200 ms kept after the last word
const BREATH_FRAMES = 6 // 120 ms: a breath, enough to cut a long sentence at
const MIN_SPEECH_FRAMES = 20 // under 400 ms of speech is a cough or a click
const FLOOR_FRAMES = 150 // the room's noise over the last 3 s

export const levelDb = (frame: Float32Array) => {
  let sum = 0
  for (const sample of frame) sum += sample * sample
  return 10 * Math.log10(sum / frame.length + 1e-12)
}

export class ClipCutter {
  private pending = new Float32Array(0)
  private sample = 0
  private floor: number[] = []
  private preRoll: Float32Array[] = []
  private run = 0
  private clip: Float32Array[] | null = null
  private clipStart = 0
  private speechFrames = 0
  private silentFrames = 0

  private readonly settings: CutterSettings

  constructor(settings: CutterSettings = DEFAULT_CUTTER) {
    this.settings = settings
  }

  /** Whether a sentence is being heard right now. */
  get speaking() {
    return this.clip !== null
  }

  /** The level of the last frame, in dBFS, for a meter. */
  lastDb = -100

  /** Feeds 16 kHz samples; returns the sentences they completed. */
  push(samples: Float32Array): Clip[] {
    const joined = new Float32Array(this.pending.length + samples.length)
    joined.set(this.pending)
    joined.set(samples, this.pending.length)
    const done: Clip[] = []
    let at = 0
    for (; at + FRAME <= joined.length; at += FRAME) {
      const clip = this.frame(joined.slice(at, at + FRAME))
      if (clip) done.push(clip)
    }
    this.pending = joined.slice(at)
    return done
  }

  /** The sentence in progress, when the mic is switched off mid-sentence. */
  flush(): Clip[] {
    const clip = this.close(0)
    this.pending = new Float32Array(0)
    this.run = 0
    this.preRoll = []
    return clip ? [clip] : []
  }

  private frame(frame: Float32Array): Clip | null {
    const db = levelDb(frame)
    this.lastDb = db
    const floor = this.noiseFloor()
    const speech = db > Math.max(floor + SPEECH_OVER_FLOOR_DB, SPEECH_MIN_DB)
    const start = this.sample
    this.sample += FRAME

    if (!this.clip) {
      if (!speech) {
        this.floor.push(db)
        if (this.floor.length > FLOOR_FRAMES) this.floor.shift()
      }
      this.preRoll.push(frame)
      if (this.preRoll.length > PRE_ROLL_FRAMES + OPEN_FRAMES) this.preRoll.shift()
      this.run = speech ? this.run + 1 : 0
      if (this.run < OPEN_FRAMES) return null
      this.clip = this.preRoll
      this.clipStart = start + FRAME - this.preRoll.length * FRAME
      this.preRoll = []
      this.speechFrames = this.run
      this.silentFrames = 0
      return null
    }

    this.clip.push(frame)
    if (speech) {
      this.speechFrames++
      this.silentFrames = 0
    } else {
      this.silentFrames++
    }
    const lengthMs = this.clip.length * FRAME_MS
    if (this.silentFrames * FRAME_MS >= this.settings.pauseMs) return this.close(this.silentFrames - TAIL_FRAMES)
    if (lengthMs >= this.settings.softMaxMs && this.silentFrames >= BREATH_FRAMES) return this.close(0)
    if (lengthMs >= this.settings.hardMaxMs) return this.close(0)
    return null
  }

  /** Ends the sentence, dropping `trim` frames of trailing silence; too little speech yields nothing. */
  private close(trim: number): Clip | null {
    const frames = this.clip
    this.clip = null
    this.run = 0
    if (!frames || this.speechFrames < MIN_SPEECH_FRAMES) return null
    const kept = frames.slice(0, frames.length - Math.max(0, trim))
    const samples = new Float32Array(kept.length * FRAME)
    kept.forEach((frame, i) => samples.set(frame, i * FRAME))
    return { startSample: this.clipStart, samples }
  }

  /** The quietest tenth of the recent non-speech frames: the room's noise, not its pauses between words. */
  private noiseFloor() {
    if (this.floor.length < 10) return -60
    const sorted = [...this.floor].sort((a, b) => a - b)
    return sorted[Math.floor(sorted.length * 0.1)]
  }
}
