// ADR-0072: the room mic saves its take on its own every few minutes and opens the next, losing nothing.
// ADR-0078: past the split (ADR-0080: thirty minutes) it waits for a pause to cut at, and cuts anyway a minute later.
// Run with `npm test` (node --test, which strips the types itself); takeRoller.ts imports nothing for that reason.
import assert from 'node:assert/strict'
import { test } from 'node:test'
import {
  QUIET_MIN_MS,
  QuietMeter,
  TAKE_MAX_EXTRA_SECONDS,
  TAKE_SPLIT_SECONDS,
  TakeRoller,
  quietThresholdDb,
  shouldRollNow,
  type RollingTape,
} from '../../src/features/recordings/takeRoller.ts'

const RATE = 16_000
const BYTES_PER_SECOND = RATE * 2
const HEADER = 44

class FakeTape implements RollingTape {
  samples = 0
  readonly name: string
  readonly maxBytes: number
  constructor(name: string, maxBytes = 0) {
    this.name = name
    this.maxBytes = maxBytes
  }
  push(samples: Float32Array) {
    this.samples += samples.length
  }
  bytes() {
    return HEADER + this.samples * 2
  }
  full(headroom: number) {
    return this.maxBytes > 0 && this.bytes() + headroom > this.maxBytes
  }
}

/** A take that opens or saves when its test says so, as the network would. */
function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((r) => (resolve = r))
  return { promise, resolve }
}

const flush = () => new Promise<void>((resolve) => setImmediate(resolve))
/** One tenth of a second, the size of block the mic hands over (a little more than the real 128-frame blocks). */
const block = () => new Float32Array(RATE / 10)
/** A tenth of a second at `db` below full scale: a constant signal, whose RMS is its value. */
const loud = (db: number) => new Float32Array(RATE / 10).fill(10 ** (db / 20))
const SPEECH = -20

function roller(log: string[], opens: Array<ReturnType<typeof deferred<FakeTape>>>, closes: Array<() => void>) {
  return new TakeRoller<FakeTape>({
    bytesPerSecond: BYTES_PER_SECOND,
    bytesPerSample: 2,
    headroomBytes: 5 * BYTES_PER_SECOND,
    close: (tape) => {
      log.push(`close ${tape.name}`)
      return new Promise<void>((resolve) => closes.push(resolve))
    },
    open: () => {
      log.push('open')
      const next = deferred<FakeTape>()
      opens.push(next)
      return next.promise
    },
    onFailed: () => log.push('failed'),
  })
}

test('splits at thirty minutes when the room is quiet, opening the next take only after the last is saved, and loses no audio', async () => {
  assert.equal(TAKE_SPLIT_SECONDS, 1800)
  const log: string[] = []
  const opens: Array<ReturnType<typeof deferred<FakeTape>>> = []
  const closes: Array<() => void> = []
  const r = roller(log, opens, closes)
  const first = new FakeTape('1')
  await r.into(Promise.resolve(first))

  // Thirty minutes and two seconds of audio, while the next take takes a while to open.
  const blocks = (TAKE_SPLIT_SECONDS + 2) * 10
  for (let i = 0; i < blocks; i++) r.push(block())
  assert.deepEqual(log, ['close 1'], 'the first take is saved when it reaches thirty minutes; nothing opens yet')
  assert.ok(first.bytes() <= TAKE_SPLIT_SECONDS * BYTES_PER_SECOND, 'the first take holds at most thirty minutes')
  assert.equal(r.current, null)

  closes[0]()
  await flush()
  assert.deepEqual(log, ['close 1', 'open'], 'the next take opens once the first is saved')
  // More audio arrives while it opens: it waits too.
  for (let i = 0; i < 5; i++) r.push(block())
  const second = new FakeTape('2')
  opens[0].resolve(second)
  await flush()

  assert.equal(r.current, second)
  assert.equal(first.samples + second.samples, (blocks + 5) * (RATE / 10), 'every sample is in one take or the other')
  assert.ok(second.samples > 0)
})

test('a take that reaches the size limit first rolls over then, as before', async () => {
  const log: string[] = []
  const opens: Array<ReturnType<typeof deferred<FakeTape>>> = []
  const closes: Array<() => void> = []
  const r = roller(log, opens, closes)
  // A limit of one minute's worth: five seconds of headroom roll it over at 55 seconds.
  const small = new FakeTape('small', 60 * BYTES_PER_SECOND)
  await r.into(Promise.resolve(small))
  for (let i = 0; i < 60 * 10; i++) r.push(block())
  assert.deepEqual(log, ['close small'])
  assert.ok(small.bytes() < 56 * BYTES_PER_SECOND)
})

test('stopping while the next take opens hands that take over to be saved, with what was heard meanwhile', async () => {
  const log: string[] = []
  const opens: Array<ReturnType<typeof deferred<FakeTape>>> = []
  const closes: Array<() => void> = []
  const r = roller(log, opens, closes)
  await r.into(Promise.resolve(new FakeTape('1')))
  for (let i = 0; i < (TAKE_SPLIT_SECONDS + 1) * 10; i++) r.push(block())
  closes[0]()
  await flush()
  const detached = r.detach()
  const second = new FakeTape('2')
  opens[0].resolve(second)
  assert.equal(await detached, second)
  assert.ok(second.samples > 0, 'what was heard while it opened went into the next take')
  const before = second.samples
  r.push(block())
  assert.equal(second.samples, before, 'nothing records after stopping')
})

test('the next take failing to open stops the mic', async () => {
  const log: string[] = []
  const r = new TakeRoller<FakeTape>({
    bytesPerSecond: BYTES_PER_SECOND,
    bytesPerSample: 2,
    headroomBytes: 0,
    splitSeconds: 1,
    close: async () => undefined,
    open: () => Promise.reject(new Error('offline')),
    onFailed: () => log.push('failed'),
  })
  await r.into(Promise.resolve(new FakeTape('1')))
  for (let i = 0; i < 11; i++) r.push(block())
  await flush()
  await flush()
  assert.deepEqual(log, ['failed'])
  assert.equal(r.current, null)
})

test('shouldRollNow: never before the split, at a long enough pause after it, always at the cap', () => {
  const at = { splitMs: 300_000, capMs: 360_000 }
  assert.equal(shouldRollNow(299_999, 5_000, at), false, 'a pause before the split does not cut')
  assert.equal(shouldRollNow(300_000, QUIET_MIN_MS, at), true, 'a pause right at the split cuts')
  assert.equal(shouldRollNow(330_000, QUIET_MIN_MS - 1, at), false, 'a gap between words does not')
  assert.equal(shouldRollNow(330_000, 0, at), false, 'speech carries on past the split')
  assert.equal(shouldRollNow(360_000, 0, at), true, 'the cap cuts even mid-sentence')
  assert.equal(shouldRollNow(301_000, 250, { ...at, minQuietMs: 200 }), true)
})

test('the quiet threshold follows the room: its quietest tenth plus a margin, within fixed bounds', () => {
  assert.equal(quietThresholdDb([]), -55, 'nothing heard yet: an absolute level')
  assert.equal(quietThresholdDb(Array(100).fill(-100)), -55, 'digital silence does not make the bar unreachable')
  // A room with an air conditioner at -50 dB, people speaking at -20 most of the time.
  const room = [...Array(20).fill(-50), ...Array(80).fill(-20)]
  assert.equal(quietThresholdDb(room), -41)
  assert.equal(quietThresholdDb(Array(100).fill(-25)), -35, 'never so high that speech reads as quiet')
})

test('the meter counts a pause only once it is quiet against the room, and resets on any sound', () => {
  const meter = new QuietMeter()
  // A noisy room: its floor is -45 dB, so -45 reads quiet although it is far above the absolute -55.
  for (let i = 0; i < 100; i++) meter.push(loud(i % 4 === 0 ? -45 : SPEECH), 100)
  for (let i = 0; i < 6; i++) meter.push(loud(-45), 100)
  assert.equal(meter.quietMs, 600)
  meter.push(loud(SPEECH), 100)
  assert.equal(meter.quietMs, 0)
})

function quietRoller(log: string[]) {
  return new TakeRoller<FakeTape>({
    bytesPerSecond: BYTES_PER_SECOND,
    bytesPerSample: 2,
    headroomBytes: 0,
    close: async (tape) => {
      log.push(`close ${tape.name}`)
    },
    open: async () => new FakeTape('next'),
    onFailed: () => log.push('failed'),
  })
}

test('past the split mid-sentence, the take rolls at the next pause, not at the split', async () => {
  const log: string[] = []
  const r = quietRoller(log)
  const first = new FakeTape('1')
  await r.into(Promise.resolve(first))
  // Speech up to 5:20, with gaps between words (0.3 s: too short to cut at).
  for (let i = 0; i < (TAKE_SPLIT_SECONDS + 20) * 10; i++) r.push(loud(i % 10 < 3 ? -60 : SPEECH))
  assert.deepEqual(log, [], 'no cut while the sentence goes on, past the split')
  // A one-second pause between sentences.
  for (let i = 0; i < 10; i++) r.push(loud(-60))
  await flush()
  assert.deepEqual(log, ['close 1'])
  const intoPause = (first.bytes() - HEADER) / BYTES_PER_SECOND - (TAKE_SPLIT_SECONDS + 20)
  // The block that completes 600 ms of quiet opens the next take: the first ends 0.5 s into the pause.
  assert.equal(Math.round(intoPause * 10) / 10, (QUIET_MIN_MS - 100) / 1000, 'the cut is inside the pause')
})

test('a room that never pauses is cut at the cap, so a take is never over the split plus a minute', async () => {
  const log: string[] = []
  const r = quietRoller(log)
  const first = new FakeTape('1')
  await r.into(Promise.resolve(first))
  const cap = TAKE_SPLIT_SECONDS + TAKE_MAX_EXTRA_SECONDS
  for (let i = 0; i < (cap + 5) * 10; i++) r.push(loud(i % 10 < 8 ? SPEECH : -60))
  await flush()
  assert.deepEqual(log, ['close 1'])
  const seconds = (first.bytes() - HEADER) / BYTES_PER_SECOND
  assert.ok(seconds <= cap && seconds > cap - 0.2, `${seconds}`)
})

test('a pause before the split does not cut', async () => {
  const log: string[] = []
  const r = quietRoller(log)
  await r.into(Promise.resolve(new FakeTape('1')))
  for (let i = 0; i < 60 * 10; i++) r.push(loud(SPEECH))
  for (let i = 0; i < 30 * 10; i++) r.push(loud(-80))
  for (let i = 0; i < 60 * 10; i++) r.push(loud(SPEECH))
  await flush()
  assert.deepEqual(log, [])
})
