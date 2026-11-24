/**
 * ADR-0072 — the room mic splits its take on its own. While the switch stays on, the take is saved (and transcribed)
 * and the next one opens about every {@link TAKE_SPLIT_SECONDS} of audio, so a long meeting never builds one huge take
 * and its words come in as it goes. A take also rolls over before it reaches the server's size limit (ADR-0041).
 *
 * ADR-0078 — the cut waits for a pause. Once a take holds {@link TAKE_SPLIT_SECONDS} it rolls at the first
 * {@link QUIET_MIN_MS} of quiet, so no sentence is split across two takes; if the room never pauses it rolls anyway at
 * {@link TAKE_MAX_EXTRA_SECONDS} past the split. Quiet is measured on the very samples the take records, against
 * the room's own noise floor, so it needs no second microphone and no timer a background tab would slow down.
 *
 * The meeting has one open take on the server, so the next opens once the last is saved; what the mic hears meanwhile
 * waits here and goes into the next take, so nothing is lost. Kept free of browser and app imports, so it runs under
 * `node --test` (takeRoller.test.ts).
 */

/**
 * ADR-0080 — thirty minutes (Lộc, 23/11/2026; five before). The room recording is transcribed again as a whole after
 * every take, and at five minutes the next take came before the last run had finished: runs kept cancelling each other.
 */
export const TAKE_SPLIT_SECONDS = 30 * 60
/** Past the split, how long a take waits for a pause before it is cut anyway: a take is never over 31 minutes. */
export const TAKE_MAX_EXTRA_SECONDS = 60
/** Quiet this long is a pause between sentences, not a gap between words. */
export const QUIET_MIN_MS = 600
/** Quiet is at most this far above the room's noise floor (as the personal mic's cutter: 9 dB). */
export const QUIET_MARGIN_DB = 9
/** Below this the room is always quiet, whatever the floor: an empty room under noise suppression. */
export const QUIET_DB_MIN = -55
/** Above this it never is: a loud room is talking, however loud its floor seemed. */
export const QUIET_DB_MAX = -35
/** The noise floor is the quietest tenth of the last half minute. */
const FLOOR_WINDOW_MS = 30_000
const FLOOR_PERCENTILE = 0.1
/** The floor moves slowly: worked out again once a second of audio, not on every block. */
const FLOOR_REFRESH_MS = 1000

/**
 * Whether a take rolls over now. `elapsedMs`: audio in it, this block included; `quietMs`: how long the room has been
 * quiet up to this block. Before the split it never rolls; from the split on it rolls at a pause; at the cap it rolls.
 */
export function shouldRollNow(
  elapsedMs: number,
  quietMs: number,
  { splitMs, capMs, minQuietMs = QUIET_MIN_MS }: { splitMs: number; capMs: number; minQuietMs?: number },
) {
  if (elapsedMs < splitMs) return false
  return elapsedMs >= capMs || quietMs >= minQuietMs
}

/** A block's loudness, in dB below full scale (RMS); silence reads -100. */
export function blockDb(samples: Float32Array) {
  if (samples.length === 0) return -100
  let sum = 0
  for (const sample of samples) sum += sample * sample
  return Math.max(-100, 10 * Math.log10(sum / samples.length + 1e-12))
}

/** Below what level a block counts as quiet, given the levels heard over the last half minute. */
export function quietThresholdDb(levels: readonly number[]) {
  if (levels.length === 0) return QUIET_DB_MIN
  const sorted = [...levels].sort((a, b) => a - b)
  const floor = sorted[Math.floor((sorted.length - 1) * FLOOR_PERCENTILE)]
  return Math.min(QUIET_DB_MAX, Math.max(QUIET_DB_MIN, floor + QUIET_MARGIN_DB))
}

/** Follows the room's loudness block by block: its noise floor, and how long it has been quiet. */
export class QuietMeter {
  private levels: number[] = []
  private durations: number[] = []
  private windowMs = 0
  private threshold: number | null = null
  private sinceThreshold = 0
  private quiet = 0

  /** Hears one block lasting `ms`; returns how long the room has been quiet, this block included. */
  push(samples: Float32Array, ms: number) {
    const db = blockDb(samples)
    this.levels.push(db)
    this.durations.push(ms)
    this.windowMs += ms
    while (this.windowMs > FLOOR_WINDOW_MS && this.durations.length > 1) {
      this.windowMs -= this.durations.shift()!
      this.levels.shift()
    }
    this.sinceThreshold += ms
    if (this.threshold === null || this.sinceThreshold >= FLOOR_REFRESH_MS) {
      this.threshold = quietThresholdDb(this.levels)
      this.sinceThreshold = 0
    }
    this.quiet = db < this.threshold ? this.quiet + ms : 0
    return this.quiet
  }

  get quietMs() {
    return this.quiet
  }
}

export interface RollingTape {
  push(samples: Float32Array): void
  /** The take's size so far, header included. */
  bytes(): number
  /** Another `headroom` bytes would take it past the server's limit. */
  full(headroom: number): boolean
}

export interface TakeRollerOptions<T extends RollingTape> {
  /** Bytes one second of audio takes on the tape. */
  bytesPerSecond: number
  /** Bytes one sample takes on the tape. */
  bytesPerSample: number
  /** Kept free below the server's size limit. */
  headroomBytes: number
  splitSeconds?: number
  /** How long past the split a take waits for a pause. */
  maxExtraSeconds?: number
  /** Saves a take that rolled over. The next opens only once this is done. */
  close: (tape: T) => Promise<void>
  /** Opens the next take, without asking for consent again: that is asked before the meeting's first take only. */
  open: () => Promise<T>
  /** The next take could not open: what was heard meanwhile is dropped and the mic must stop. */
  onFailed: () => void
}

export class TakeRoller<T extends RollingTape> {
  private tape: T | null = null
  // While the next take opens, blocks wait here.
  private rolling: Promise<void> | null = null
  private waiting: Float32Array[] = []
  private readonly o: TakeRollerOptions<T>
  private readonly splitMs: number
  private readonly capMs: number
  private readonly samplesPerMs: number
  private readonly meter = new QuietMeter()

  constructor(options: TakeRollerOptions<T>) {
    this.o = options
    this.splitMs = (options.splitSeconds ?? TAKE_SPLIT_SECONDS) * 1000
    this.capMs = this.splitMs + (options.maxExtraSeconds ?? TAKE_MAX_EXTRA_SECONDS) * 1000
    this.samplesPerMs = options.bytesPerSecond / options.bytesPerSample / 1000
  }

  /** The take being recorded into, or null while the next one opens. */
  get current() {
    return this.tape
  }

  /** Records into `next` once it has opened; what was heard meanwhile goes into it, or nowhere if it never does. */
  into(next: Promise<T>) {
    const rolling = next
      .then((tape) => {
        this.tape = tape
      })
      .finally(() => {
        this.rolling = null
        const blocks = this.waiting
        this.waiting = []
        for (const block of blocks) this.record(block)
      })
    this.rolling = rolling.catch(() => undefined)
    return rolling
  }

  push(samples: Float32Array) {
    // The room is heard all along, also while the next take opens, so a pause is known the moment it comes.
    this.meter.push(samples, samples.length / this.samplesPerMs)
    this.record(samples)
  }

  private record(samples: Float32Array) {
    if (this.rolling) {
      this.waiting.push(samples)
      return
    }
    const tape = this.tape
    if (!tape) return
    const block = samples.length * this.o.bytesPerSample
    const elapsedMs = ((tape.bytes() + block) / this.o.bytesPerSecond) * 1000
    const due = shouldRollNow(elapsedMs, this.meter.quietMs, { splitMs: this.splitMs, capMs: this.capMs })
    if (tape.full(this.o.headroomBytes) || due) {
      this.waiting.push(samples)
      this.tape = null
      // The meeting has one open take: this one is saved before the next opens. The block that made it due goes into
      // the next take, so the take ends inside the pause and the next one starts there.
      void this.into(this.o.close(tape).then(() => this.o.open())).catch(() => this.o.onFailed())
      return
    }
    tape.push(samples)
  }

  /** Waits for a take still opening, then hands the current one over to be saved; nothing records after this. */
  async detach() {
    await this.rolling
    const tape = this.tape
    this.tape = null
    this.waiting = []
    return tape
  }
}
