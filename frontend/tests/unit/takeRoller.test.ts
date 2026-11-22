// ADR-0072: the room mic saves its take on its own every few minutes and opens the next, losing nothing.
// Run with `npm test` (node --test, which strips the types itself); takeRoller.ts imports nothing for that reason.
import assert from 'node:assert/strict'
import { test } from 'node:test'
import { TAKE_SPLIT_SECONDS, TakeRoller, type RollingTape } from '../../src/features/recordings/takeRoller.ts'

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

test('splits every five minutes, opening the next take only after the last is saved, and loses no audio', async () => {
  assert.equal(TAKE_SPLIT_SECONDS, 300)
  const log: string[] = []
  const opens: Array<ReturnType<typeof deferred<FakeTape>>> = []
  const closes: Array<() => void> = []
  const r = roller(log, opens, closes)
  const first = new FakeTape('1')
  await r.into(Promise.resolve(first))

  // Five minutes and two seconds of audio, while the next take takes a while to open.
  const blocks = (TAKE_SPLIT_SECONDS + 2) * 10
  for (let i = 0; i < blocks; i++) r.push(block())
  assert.deepEqual(log, ['close 1'], 'the first take is saved when it reaches five minutes; nothing opens yet')
  assert.ok(first.bytes() <= TAKE_SPLIT_SECONDS * BYTES_PER_SECOND, 'the first take holds at most five minutes')
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
