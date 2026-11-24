// PO 23/11/2026: a progress bar and the processing time of a transcript. transcribeTime.ts imports nothing, so
// node --test runs it as it is.
import assert from 'node:assert/strict'
import { test } from 'node:test'
import {
  elapsedSince,
  formatClock,
  formatRemaining,
  formatSpan,
  processingTime,
  progressParts,
  remainingMs,
} from '../../src/features/recordings/transcribeTime.ts'

const MIN = 60_000

test('the time left follows the pace so far', () => {
  assert.equal(remainingMs(25, 10 * MIN), 30 * MIN)
  assert.equal(remainingMs(50, 12 * MIN), 12 * MIN)
})

test('no time left while too early to tell or once done', () => {
  assert.equal(remainingMs(4, 10 * MIN), null)
  assert.equal(remainingMs(40, 29_000), null)
  assert.equal(remainingMs(40, null), null)
  assert.equal(remainingMs(100, 10 * MIN), null)
})

test('elapsed comes from the start the server gave, never below zero', () => {
  const now = Date.parse('2026-11-23T10:12:30Z')
  assert.equal(elapsedSince('2026-11-23T10:00:00Z', now), 12.5 * MIN)
  assert.equal(elapsedSince('2026-11-23T10:12:31Z', now), 0)
  assert.equal(elapsedSince(null, now), null)
  assert.equal(elapsedSince('not a date', now), null)
})

test('clock and spans read as people say them', () => {
  assert.equal(formatClock(12.5 * MIN), '12:30')
  assert.equal(formatClock(65 * MIN + 5000), '1:05:05')
  assert.equal(formatSpan(45_000), '45 giây')
  assert.equal(formatSpan(24 * MIN + 10_000), '24 phút')
  assert.equal(formatSpan(65 * MIN), '1 giờ 5 phút')
  assert.equal(formatSpan(120 * MIN), '2 giờ')
  assert.equal(formatRemaining(30_000), 'sắp xong')
  assert.equal(formatRemaining(15 * MIN), 'còn ~15 phút')
})

test('the line under the bar', () => {
  assert.deepEqual(progressParts(42, 12.5 * MIN), { label: 'Đang chuyển chữ 42%', clock: '12:30', left: 'còn ~17 phút' })
  assert.deepEqual(progressParts(3, 2 * MIN), { label: 'Đang chuyển chữ 3%', clock: '2:00', left: null })
  assert.deepEqual(progressParts(0, 20_000), { label: 'Đang chuyển chữ', clock: '0:20', left: null })
  assert.deepEqual(progressParts(60, 6 * MIN, true), { label: 'Đang chuyển chữ lại 60%', clock: '6:00', left: 'còn ~4 phút' })
  assert.deepEqual(progressParts(0, null), { label: 'Đang chuyển chữ', clock: null, left: null })
})

test('how long a finished transcript took', () => {
  assert.equal(processingTime('2026-11-23T10:00:00Z', '2026-11-23T10:24:00Z'), 24 * MIN)
  assert.equal(processingTime(null, '2026-11-23T10:24:00Z'), null)
  assert.equal(processingTime('2026-11-23T10:30:00Z', '2026-11-23T10:24:00Z'), null)
})
