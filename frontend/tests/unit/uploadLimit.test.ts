// BR-107–BR-115, MSG293: a recording past the server's limit is refused before it is sent, and a connection dropped
// while sending one reads as that refusal, not as "no network" (round 1 QA, REC_FT27).
// Run with `npm test` (node --test, which strips the types itself); uploadLimit.ts imports nothing for that reason.
import assert from 'node:assert/strict'
import { test } from 'node:test'
import {
  explainUploadFailure,
  isTooLarge,
  limitHint,
  tooLargeMessage,
} from '../../src/features/recordings/uploadLimit.ts'

const MB = 1024 * 1024
const LIMIT = 300 * MB

test('a file one byte past the limit is too large, one at the limit is not', () => {
  assert.equal(isTooLarge(LIMIT + 1, LIMIT), true)
  assert.equal(isTooLarge(LIMIT, LIMIT), false)
})

test('an unknown limit refuses nothing', () => {
  assert.equal(isTooLarge(10_000 * MB, undefined), false)
  assert.equal(isTooLarge(10_000 * MB, 0), false)
  assert.equal(limitHint(undefined), null)
})

test("the message is MSG293's, with the server's own limit", () => {
  assert.equal(tooLargeMessage(LIMIT), 'Bản ghi vượt quá 300 MB.')
  assert.equal(tooLargeMessage(1 * MB), 'Bản ghi vượt quá 1 MB.')
  assert.equal(limitHint(LIMIT), 'tối đa 300 MB')
})

test('a dropped connection while sending a file over the limit reads as MSG293', () => {
  const explained = explainUploadFailure(new TypeError('Network request failed'), LIMIT + 1, LIMIT)
  assert.ok(explained instanceof Error)
  assert.ok(!(explained instanceof TypeError))
  assert.equal((explained as Error).message, 'Bản ghi vượt quá 300 MB.')
})

test('any other failure is left as it is', () => {
  const network = new TypeError('Network request failed')
  assert.equal(explainUploadFailure(network, 5 * MB, LIMIT), network)
  assert.equal(explainUploadFailure(network, LIMIT + 1, undefined), network)
  const refused = new Error('Chỉ Thư ký cuộc họp được quản lý bản ghi.')
  assert.equal(explainUploadFailure(refused, LIMIT + 1, LIMIT), refused)
})
