import { completeRoomTake, beginRoomTake, sendRoomPart } from '../../api/recordings'
import { ApiError } from '../../api/client'
import { isApiError } from '../../lib/errors'
import type { RoomTakeSaved } from '../../types/recording'
import { levelDb, SAMPLE_RATE } from '../speech/clipCutter'
import { Resampler } from '../speech/wav'
// Always a file of our own origin: Vite would inline a file this small as a data: URL, which the test server's CSP
// (script-src 'self') refuses to run as a worklet.
import speechTapUrl from '../speech/speechTap.worklet.js?url&no-inline'
import { BYTES_PER_SECOND, RoomTape, TapeClosed, type SavedTape, type SendPart, type TapeStore } from './roomTape'

/**
 * ADR-0041 — one laptop as the room's microphone. Everything it hears goes into the meeting's open take; switching it
 * off saves the take, which the server appends to the room recording and transcribes again.
 */

/** What the browser said, turned into what to do about it. */
export function micError(error: unknown) {
  if (error instanceof DOMException) {
    if (error.name === 'NotAllowedError')
      return 'Trình duyệt chưa cho phép dùng micro. Hãy cho phép ở biểu tượng ổ khoá trên thanh địa chỉ.'
    if (error.name === 'NotFoundError' || error.name === 'OverconstrainedError')
      return 'Không tìm thấy micro trên máy này.'
    if (error.name === 'NotReadableError') return 'Micro đang bị ứng dụng khác dùng.'
  }
  return 'Không mở được micro.'
}

interface MicSource {
  /** 0–1, on the same scale as the per-mic meter. */
  level: () => number
  release: () => void
}

/**
 * The laptop's mic as 16 kHz blocks. Automatic gain stays on: unlike a personal mic, the room's must lift the voices
 * at the far end of the table.
 */
async function openMic(onBlock: (samples: Float32Array) => void, onEnded: () => void): Promise<MicSource> {
  const stream = await navigator.mediaDevices.getUserMedia({
    audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true, channelCount: 1 },
  })
  const context = new AudioContext()
  try {
    await context.audioWorklet.addModule(speechTapUrl)
  } catch (error) {
    stream.getTracks().forEach((track) => track.stop())
    void context.close()
    throw error
  }
  const tap = new AudioWorkletNode(context, 'speech-tap')
  const resampler = new Resampler(context.sampleRate, SAMPLE_RATE)
  let level = 0
  tap.port.onmessage = (event: MessageEvent<Float32Array>) => {
    const samples = resampler.push(event.data)
    level = Math.max(0, Math.min(1, (levelDb(event.data) + 60) / 50))
    onBlock(samples)
  }
  context.createMediaStreamSource(stream).connect(tap)
  stream.getAudioTracks()[0]?.addEventListener('ended', onEnded)
  void context.resume()
  return {
    level: () => level,
    release: () => {
      tap.port.onmessage = null
      stream.getTracks().forEach((track) => track.stop())
      void context.close()
    },
  }
}

/** The take was saved already (by another Secretary, or by the server after hours of silence): it takes nothing more. */
const closedTake = (error: unknown) => isApiError(error, 409, 'MSG362')

/** Sends one part; the server refusing the take for good reads as {@link TapeClosed}. */
export function sendPart(recordingId: string): SendPart {
  return async (part, bytes, final) => {
    try {
      await sendRoomPart(recordingId, part, bytes, final)
    } catch (error) {
      throw closedTake(error) || isApiError(error, 404) ? new TapeClosed() : error
    }
  }
}

const COMPLETE_RETRY_MS = [1000, 2000, 5000, 10000, 15000]

/** Saves the take, trying again through a network hiccup; one already saved, or gone, counts as saved. */
export async function saveTake(recordingId: string): Promise<RoomTakeSaved | null> {
  for (let attempt = 0; ; attempt++) {
    try {
      return await completeRoomTake(recordingId)
    } catch (error) {
      if (isApiError(error, 404) || closedTake(error)) return null
      const retryable =
        !(error instanceof ApiError) || error.status >= 500 || error.status === 408 || error.status === 429
      if (!retryable || attempt >= COMPLETE_RETRY_MS.length) throw error
      await new Promise<void>((resolve) => window.setTimeout(resolve, COMPLETE_RETRY_MS[attempt]))
    }
  }
}

export interface RoomMicEvents {
  /** The mic stopped on its own: the device went away, or the take was saved elsewhere. */
  onLost: (message: string) => void
  onSaved: (saved: RoomTakeSaved | null) => void
}

/** Five seconds of headroom: a take rolls over before it reaches the server's limit, never at it. */
const ROLL_HEADROOM = 5 * BYTES_PER_SECOND

export class RoomMic {
  private source: MicSource | null = null
  private tape: RoomTape | null = null
  // While the next take opens (the last one was full), blocks wait here.
  private rolling: Promise<void> | null = null
  private waiting: Float32Array[] = []
  private closing: Promise<void>[] = []

  private readonly meetingId: string
  private readonly purpose: () => string
  private readonly store: TapeStore
  private readonly events: RoomMicEvents

  /** `purpose` is read when a take opens, so the one the Secretary confirmed last is stored with it. */
  constructor(meetingId: string, purpose: () => string, store: TapeStore, events: RoomMicEvents) {
    this.meetingId = meetingId
    this.purpose = purpose
    this.store = store
    this.events = events
  }

  level() {
    return this.source?.level() ?? 0
  }

  /** Seconds of audio waiting behind a part the server has not taken: the network is slow, nothing is lost. */
  behindSeconds() {
    return (this.tape?.behind() ?? 0) / BYTES_PER_SECOND
  }

  /**
   * Opens the mic and a take — a new one, or the one this device kept from a session cut off. `consent`: the
   * participants were told just now, which the server asks for before the meeting's first take.
   */
  async start(consent: boolean, saved?: SavedTape) {
    const taping = saved ? Promise.resolve(this.resume(saved)) : this.fresh(consent)
    taping.catch(() => undefined)
    try {
      this.source = await openMic(
        (samples) => this.take(samples),
        () => this.lost('Micro đã bị ngắt khỏi máy. Lượt ghi đến lúc đó vẫn được lưu.'),
      )
    } catch (error) {
      // No mic, no take: one already opened is saved empty at once, so the meeting holds no take nobody feeds.
      if (!saved) void taping.then((tape) => saveTake(tape.recordingId)).catch(() => undefined)
      throw new Error(micError(error))
    }
    try {
      await this.into(taping)
    } catch (error) {
      this.source?.release()
      this.source = null
      throw error
    }
  }

  /** Releases the mic, sends the rest and saves the take. */
  async stop() {
    this.source?.release()
    this.source = null
    await this.rolling
    if (this.tape) this.closing.push(this.close(this.tape))
    this.tape = null
    await Promise.all(this.closing)
  }

  private lost(message: string) {
    void this.stop().catch(() => undefined)
    this.events.onLost(message)
  }

  private async fresh(consent: boolean) {
    const opened = await beginRoomTake(this.meetingId, consent, this.purpose())
    return new RoomTape({
      meetingId: this.meetingId,
      recordingId: opened.recordingId,
      partSize: opened.partSize,
      maxBytes: opened.maxBytes,
      startedAt: Date.now(),
      send: sendPart(opened.recordingId),
      store: this.store,
      onClosed: () => this.lost('Lượt ghi đã được lưu ở nơi khác. Bật mic để ghi tiếp.'),
    })
  }

  private resume(saved: SavedTape) {
    return RoomTape.resume(saved, {
      send: sendPart(saved.recordingId),
      store: this.store,
      onClosed: () => this.lost('Lượt ghi dở đã được lưu ở nơi khác. Bật mic để ghi tiếp.'),
    })
  }

  private async close(tape: RoomTape) {
    try {
      await tape.finish()
    } catch (error) {
      // Saved elsewhere meanwhile: nothing left to save here.
      if (error instanceof TapeClosed) return
      throw error
    }
    this.events.onSaved(await saveTake(tape.recordingId))
  }

  private take(samples: Float32Array) {
    if (this.rolling) {
      this.waiting.push(samples)
      return
    }
    const tape = this.tape
    if (!tape) return
    if (tape.full(ROLL_HEADROOM)) {
      this.waiting.push(samples)
      this.tape = null
      // The meeting has one open take: this one is saved before the next opens.
      void this.into(this.close(tape).then(() => this.fresh(false))).catch(() =>
        this.lost('Không mở được lượt ghi tiếp theo. Phần đã ghi vẫn được lưu.'),
      )
      return
    }
    tape.push(samples)
  }

  /** Records into the next take once it has opened; what was heard meanwhile goes into it, or nowhere if it never does. */
  private into(next: Promise<RoomTape>) {
    const rolling = next
      .then((tape) => {
        this.tape = tape
      })
      .finally(() => {
        this.rolling = null
        const blocks = this.waiting
        this.waiting = []
        for (const block of blocks) this.take(block)
      })
    this.rolling = rolling.catch(() => undefined)
    return rolling
  }
}
