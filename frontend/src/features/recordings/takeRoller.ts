/**
 * ADR-0072 — the room mic splits its take on its own. While the switch stays on, every {@link TAKE_SPLIT_SECONDS} of
 * audio the take is saved (and transcribed) and the next one opens, so a long meeting never builds one huge take and its
 * words come in as it goes. A take also rolls over before it reaches the server's size limit (ADR-0041).
 *
 * The meeting has one open take on the server, so the next opens once the last is saved; what the mic hears meanwhile
 * waits here and goes into the next take, so nothing is lost. Kept free of browser and app imports, so it runs under
 * `node --test` (takeRoller.test.ts).
 */

/** Five minutes, as the team asked for (team test 21/11/2026): short enough for words to come in as the meeting goes. */
export const TAKE_SPLIT_SECONDS = 5 * 60

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
  private readonly splitBytes: number

  constructor(options: TakeRollerOptions<T>) {
    this.o = options
    this.splitBytes = (options.splitSeconds ?? TAKE_SPLIT_SECONDS) * options.bytesPerSecond
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
        for (const block of blocks) this.push(block)
      })
    this.rolling = rolling.catch(() => undefined)
    return rolling
  }

  push(samples: Float32Array) {
    if (this.rolling) {
      this.waiting.push(samples)
      return
    }
    const tape = this.tape
    if (!tape) return
    const block = samples.length * this.o.bytesPerSample
    if (tape.full(this.o.headroomBytes) || tape.bytes() + block > this.splitBytes) {
      this.waiting.push(samples)
      this.tape = null
      // The meeting has one open take: this one is saved before the next opens.
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
