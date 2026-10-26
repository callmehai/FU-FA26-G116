// SCRUM-109 — copies the mic's samples off the audio thread, a block at a time, so the page can cut them into
// sentences. Plain JS on purpose: the browser loads it as a module of its own, untouched by the bundler.
const BLOCK = 2048

class SpeechTap extends AudioWorkletProcessor {
  constructor() {
    super()
    this.buffer = new Float32Array(BLOCK)
    this.filled = 0
  }

  process(inputs) {
    const channel = inputs[0] && inputs[0][0]
    if (!channel) return true
    let read = 0
    while (read < channel.length) {
      const take = Math.min(channel.length - read, BLOCK - this.filled)
      this.buffer.set(channel.subarray(read, read + take), this.filled)
      this.filled += take
      read += take
      if (this.filled === BLOCK) {
        this.port.postMessage(this.buffer, [this.buffer.buffer])
        this.buffer = new Float32Array(BLOCK)
        this.filled = 0
      }
    }
    return true
  }
}

registerProcessor('speech-tap', SpeechTap)
