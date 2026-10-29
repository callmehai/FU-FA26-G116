// Cuts one mic's audio into sentences with the web app's own ClipCutter (frontend/src/features/speech/clipCutter.ts),
// so bleed.py measures what the browser would send rather than a re-implementation of it.
// Node 22.18+ reads the TypeScript file directly. stdin: float32 little-endian samples at 16 kHz;
// stdout: JSON [[startSample, sampleCount], ...].
import { ClipCutter } from '../../../frontend/src/features/speech/clipCutter.ts'

const chunks = []
for await (const chunk of process.stdin) chunks.push(chunk)
const bytes = Buffer.concat(chunks)
const samples = new Float32Array(bytes.buffer, bytes.byteOffset, bytes.length / 4)
const cutter = new ClipCutter()
const clips = []
// The browser's worklet hands over 2048-sample blocks.
for (let at = 0; at < samples.length; at += 2048) clips.push(...cutter.push(samples.slice(at, at + 2048)))
clips.push(...cutter.flush())
process.stdout.write(JSON.stringify(clips.map((clip) => [clip.startSample, clip.samples.length])))
