/** 16-bit PCM WAV of mono samples, the one format every speech engine reads without guessing. */
export function encodeWav(samples: Float32Array, sampleRate: number): Blob {
  const view = new DataView(new ArrayBuffer(44 + samples.length * 2))
  const text = (at: number, value: string) => [...value].forEach((c, i) => view.setUint8(at + i, c.charCodeAt(0)))
  text(0, 'RIFF')
  view.setUint32(4, 36 + samples.length * 2, true)
  text(8, 'WAVE')
  text(12, 'fmt ')
  view.setUint32(16, 16, true)
  view.setUint16(20, 1, true)
  view.setUint16(22, 1, true)
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * 2, true)
  view.setUint16(32, 2, true)
  view.setUint16(34, 16, true)
  text(36, 'data')
  view.setUint32(40, samples.length * 2, true)
  samples.forEach((sample, i) => {
    const clamped = Math.max(-1, Math.min(1, sample))
    view.setInt16(44 + i * 2, clamped < 0 ? clamped * 0x8000 : clamped * 0x7fff, true)
  })
  return new Blob([view], { type: 'audio/wav' })
}

/**
 * Streams samples from the device's rate down to `to` Hz by linear interpolation. Good enough for speech, and it
 * keeps the AudioContext at the device's own rate: Firefox refuses a mic in a context of another rate.
 */
export class Resampler {
  private position = 0
  private last = 0
  private readonly step: number

  constructor(from: number, to: number) {
    this.step = from / to
  }

  push(input: Float32Array): Float32Array {
    const out: number[] = []
    while (this.position < input.length) {
      const index = Math.floor(this.position)
      const fraction = this.position - index
      const before = index === 0 ? this.last : input[index - 1]
      const after = input[index]
      out.push(before + (after - before) * fraction)
      this.position += this.step
    }
    this.position -= input.length
    this.last = input[input.length - 1] ?? this.last
    return Float32Array.from(out)
  }
}
