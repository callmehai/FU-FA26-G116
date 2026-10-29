import { SAMPLE_RATE } from '../speech/clipCutter'

/**
 * ADR-0041 — the room mic's take as it is recorded: 16 kHz PCM appended as the room speaks, sent to the server in parts
 * of exactly `partSize` bytes (a short last one when the mic goes off), and kept on the device until the server has
 * it, as AI-eCabinet does. A part's place in the file follows from its number, so one sent again after a lost answer
 * lands where it was; a closed tab or a crash loses nothing the device still holds.
 */

const HEADER_BYTES = 44

/**
 * A 16 kHz mono 16-bit WAV header for a file whose length is not known yet. The sizes are the largest a WAV can
 * name, so a decoder reads to the end; the server writes the real ones in when the take is saved.
 */
export function streamHeader(): Uint8Array {
  const header = new Uint8Array(HEADER_BYTES)
  const view = new DataView(header.buffer)
  const text = (at: number, value: string) => [...value].forEach((c, i) => (header[at + i] = c.charCodeAt(0)))
  text(0, 'RIFF')
  view.setUint32(4, 0xffffffff, true)
  text(8, 'WAVEfmt ')
  view.setUint32(16, 16, true)
  view.setUint16(20, 1, true)
  view.setUint16(22, 1, true)
  view.setUint32(24, SAMPLE_RATE, true)
  view.setUint32(28, SAMPLE_RATE * 2, true)
  view.setUint16(32, 2, true)
  view.setUint16(34, 16, true)
  text(36, 'data')
  view.setUint32(40, 0xffffffff - 36, true)
  return header
}

function pcm16(samples: Float32Array): Uint8Array {
  const out = new Uint8Array(samples.length * 2)
  const view = new DataView(out.buffer)
  samples.forEach((sample, i) => {
    const clamped = Math.max(-1, Math.min(1, sample))
    view.setInt16(i * 2, clamped < 0 ? clamped * 0x8000 : clamped * 0x7fff, true)
  })
  return out
}

/** What the server does not have yet, from part `nextPart` on: enough to carry on after the tab was closed. */
export interface SavedTape {
  meetingId: string
  recordingId: string
  partSize: number
  maxBytes: number
  nextPart: number
  pending: Uint8Array
  /** When the take began, for the card's clock after a reload. */
  startedAt: number
}

export interface TapeStore {
  load: (meetingId: string) => Promise<SavedTape | null>
  save: (tape: SavedTape) => Promise<void>
  clear: (meetingId: string) => Promise<void>
}

export type SendPart = (part: number, bytes: Uint8Array, final: boolean) => Promise<void>

/** Thrown by a `SendPart` when the server will never take the part: the take was saved already (MSG362). */
export class TapeClosed extends Error {}

const SAVE_EVERY_SAMPLES = 5 * SAMPLE_RATE
const RETRY_MS = [1000, 2000, 5000, 10000, 15000]

interface Options {
  meetingId: string
  recordingId: string
  partSize: number
  maxBytes: number
  startedAt: number
  send: SendPart
  store?: TapeStore
  /** The server refused the take for good: what is left on the device can never be added to it. */
  onClosed?: () => void
  nextPart?: number
  pending?: Uint8Array
  wait?: (ms: number) => Promise<void>
}

export class RoomTape {
  readonly meetingId: string
  readonly recordingId: string
  readonly startedAt: number
  private readonly o: Options
  private nextPart: number
  private chunks: Uint8Array[]
  private length: number
  private sent: number
  private finishing = false
  private closed = false
  private running: Promise<void> | null = null
  private again = false
  private sinceSave = 0
  private saving: Promise<void> = Promise.resolve()

  constructor(options: Options) {
    this.o = options
    this.meetingId = options.meetingId
    this.recordingId = options.recordingId
    this.startedAt = options.startedAt
    this.nextPart = options.nextPart ?? 1
    // A fresh take opens with its header; one carried on from the device already has it in what was sent or kept.
    this.chunks = [options.pending ?? (this.nextPart === 1 ? streamHeader() : new Uint8Array())]
    this.length = this.chunks[0].length
    this.sent = (this.nextPart - 1) * options.partSize
  }

  static resume(saved: SavedTape, options: Pick<Options, 'send' | 'store' | 'onClosed' | 'wait'>) {
    return new RoomTape({ ...options, ...saved })
  }

  push(samples: Float32Array) {
    if (this.closed) return
    const bytes = pcm16(samples)
    this.chunks.push(bytes)
    this.length += bytes.length
    this.sinceSave += samples.length
    if (this.sinceSave >= SAVE_EVERY_SAMPLES) {
      this.sinceSave = 0
      this.save()
    }
    this.kick()
  }

  /** The take's size so far, sent or not. */
  bytes() {
    return this.sent + this.length
  }

  /** Another `headroom` bytes would take the take past the server's limit: time to start the next one. */
  full(headroom: number) {
    return this.o.maxBytes > 0 && this.bytes() + headroom > this.o.maxBytes
  }

  /** What waits behind a full part the server has not taken yet; the part still filling is not late. */
  behind() {
    return Math.max(0, this.length - this.o.partSize)
  }

  /** Sends the rest, the last part short, and forgets the take on the device. */
  async finish() {
    this.finishing = true
    this.kick()
    while (this.running) await this.running
    await this.saving
    await this.o.store?.clear(this.meetingId).catch(() => undefined)
    if (this.closed) throw new TapeClosed()
  }

  private save() {
    const store = this.o.store
    if (!store || this.closed) return
    const snapshot: SavedTape = {
      meetingId: this.meetingId,
      recordingId: this.recordingId,
      partSize: this.o.partSize,
      maxBytes: this.o.maxBytes,
      nextPart: this.nextPart,
      pending: this.take(this.length),
      startedAt: this.startedAt,
    }
    this.saving = this.saving.then(() => store.save(snapshot)).catch(() => undefined)
  }

  // A kick while a run winds down is remembered: that run may already have looked for work.
  private kick() {
    if (this.running) {
      this.again = true
      return
    }
    this.running = this.run().finally(() => {
      this.running = null
      if (this.again) {
        this.again = false
        this.kick()
      }
    })
  }

  private async run() {
    const size = this.o.partSize
    let attempt = 0
    while (!this.closed && (this.length >= size || (this.finishing && this.length > 0))) {
      const n = Math.min(size, this.length)
      try {
        await this.o.send(this.nextPart, this.take(n), n < size)
      } catch (error) {
        if (error instanceof TapeClosed) {
          this.closed = true
          this.chunks = []
          this.length = 0
          await this.o.store?.clear(this.meetingId).catch(() => undefined)
          this.o.onClosed?.()
          return
        }
        await (this.o.wait ?? ((ms) => new Promise<void>((r) => window.setTimeout(r, ms))))(
          RETRY_MS[Math.min(attempt++, RETRY_MS.length - 1)],
        )
        continue
      }
      attempt = 0
      this.drop(n)
      this.sent += n
      this.nextPart++
      this.save()
    }
  }

  private take(n: number) {
    const out = new Uint8Array(n)
    let at = 0
    for (const chunk of this.chunks) {
      if (at >= n) break
      const part = chunk.subarray(0, n - at)
      out.set(part, at)
      at += part.length
    }
    return out
  }

  private drop(n: number) {
    this.length -= n
    while (n > 0) {
      const chunk = this.chunks[0]
      if (chunk.length <= n) {
        n -= chunk.length
        this.chunks.shift()
      } else {
        this.chunks[0] = chunk.subarray(n)
        n = 0
      }
    }
  }
}

/** 16 kHz mono 16-bit: what one second of the room weighs. */
export const BYTES_PER_SECOND = SAMPLE_RATE * 2
