// ADR-0085 — a hybrid meeting's Secretary has one mic: the call room's mic is the room mic, shown and switched as one,
// and no floating card repeats it while this browser's call room holds it (Khánh's bug, 24/11).
// Run with `npm test` (node --test, which strips the types itself); hybridMic.ts imports nothing for that reason.
import assert from 'node:assert/strict'
import { test } from 'node:test'
import {
  lobbyMicOn,
  lobbyMicPress,
  roomMicStopsOnMute,
  roomMicStopsWhen,
  showsRoomMicCard,
  type CardFacts,
} from '../../src/features/recordings/hybridMic.ts'

test('the lobby shows the mic on while the room mic records, whatever was chosen there last time', () => {
  assert.equal(lobbyMicOn(false, 'on'), true)
  assert.equal(lobbyMicOn(false, 'starting'), true)
  assert.equal(lobbyMicOn(true, 'on'), true)
})

test('with the room mic off, the lobby shows its own choice', () => {
  assert.equal(lobbyMicOn(false, 'off'), false)
  assert.equal(lobbyMicOn(true, 'off'), true)
  assert.equal(lobbyMicOn(false, 'saving'), false)
  // Not a hybrid meeting's Secretary: no room mic at all.
  assert.equal(lobbyMicOn(true, null), true)
  assert.equal(lobbyMicOn(false, null), false)
})

test("pressing the lobby's switch while the room mic records switches it off; otherwise it changes the choice", () => {
  assert.equal(lobbyMicPress('on'), 'stopRoomMic')
  assert.equal(lobbyMicPress('starting'), 'toggleChoice')
  assert.equal(lobbyMicPress('off'), 'toggleChoice')
  assert.equal(lobbyMicPress('saving'), 'toggleChoice')
  assert.equal(lobbyMicPress(null), 'toggleChoice')
})

test('the room mic goes with the call to another tab or device, and stops when the Secretary is taken off the roster', () => {
  assert.equal(roomMicStopsWhen('movedAway'), true)
  assert.equal(roomMicStopsWhen('removed'), true)
})

test('leaving or losing the call keeps the room recorded; the meeting ending stops it on its own', () => {
  assert.equal(roomMicStopsWhen('left'), false)
  assert.equal(roomMicStopsWhen('lost'), false)
  assert.equal(roomMicStopsWhen('closed'), false)
})

test("the chair muting the Secretary's mic in the call switches the room mic off", () => {
  assert.equal(roomMicStopsOnMute(false), true)
  assert.equal(roomMicStopsOnMute(true), false)
})

const idle: CardFacts = {
  mode: 'Hybrid',
  switchHere: false,
  phase: 'off',
  unfinished: false,
  openTake: false,
  otherTab: false,
  callUnusable: false,
}

test("a hybrid meeting's page shows no card for a take this browser's call room feeds", () => {
  assert.equal(showsRoomMicCard({ ...idle, openTake: true, otherTab: true }), false)
})

test("a hybrid meeting's page still shows a take open on another device, with the way to stop it", () => {
  assert.equal(showsRoomMicCard({ ...idle, openTake: true, otherTab: false }), true)
})

test('the card stays when the call room is unusable here: the page is where the room is recorded then', () => {
  assert.equal(showsRoomMicCard({ ...idle, callUnusable: true, switchHere: true }), true)
  assert.equal(showsRoomMicCard({ ...idle, callUnusable: true, openTake: true, otherTab: true }), true)
})

test("this tab's own mic on, or a take kept on this laptop, always shows the card (ADR-0083)", () => {
  assert.equal(showsRoomMicCard({ ...idle, phase: 'on' }), true)
  assert.equal(showsRoomMicCard({ ...idle, phase: 'saving' }), true)
  assert.equal(showsRoomMicCard({ ...idle, unfinished: true }), true)
})

test('nothing to show: no card', () => {
  assert.equal(showsRoomMicCard(idle), false)
})

test('an in-person meeting keeps its card, including for a take fed by another tab', () => {
  assert.equal(showsRoomMicCard({ ...idle, mode: 'InPerson', switchHere: true }), true)
  assert.equal(showsRoomMicCard({ ...idle, mode: 'InPerson', openTake: true, otherTab: true }), true)
})
