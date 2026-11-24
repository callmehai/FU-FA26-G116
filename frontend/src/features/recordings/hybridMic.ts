/**
 * ADR-0085 — a hybrid meeting's Secretary has one mic: the call's mic is the room mic. Whatever the call room shows
 * (the lobby's switch, the call's mic button, the picture-in-picture window) is the room mic's own state, and switching
 * it there switches the room mic. These are the decisions that keep the two one, apart from React and LiveKit so they
 * can be tested on their own.
 *
 * Imports nothing, so `npm test` (node --test) runs it as it is.
 */

/** The room mic's phase, as useRoomRecorder has it. */
export type RoomMicPhase = 'off' | 'starting' | 'on' | 'saving'

/** The room mic is recording, or about to: the call shows the mic on. */
export const roomMicLive = (phase: RoomMicPhase | null | undefined) => phase === 'on' || phase === 'starting'

/**
 * The lobby's mic switch. For a hybrid meeting's Secretary whose room mic is on in this tab it shows on, whatever was
 * left in the lobby last time; otherwise it is the lobby's own choice (on: the room mic opens on joining).
 */
export function lobbyMicOn(choice: boolean, roomMic: RoomMicPhase | null) {
  return roomMicLive(roomMic) || choice
}

/** What pressing the lobby's mic switch does: switch the room mic off (its take is saved), or change the choice. */
export function lobbyMicPress(roomMic: RoomMicPhase | null): 'stopRoomMic' | 'toggleChoice' {
  return roomMic === 'on' ? 'stopRoomMic' : 'toggleChoice'
}

/**
 * Why this tab is out of the call. `movedAway`: the same person joined from another tab or device (LiveKit's duplicate
 * identity); `removed`: taken off the roster; `closed`: the meeting ended; `lost`: the connection dropped; `left`: the
 * person left.
 */
export type CallEnd = 'movedAway' | 'removed' | 'closed' | 'lost' | 'left'

/**
 * Whether the room mic stops when the call ends this way. It goes with the call when the call moves to another tab or
 * device (where it opens again), and when the Secretary is no longer on the roster; the meeting ending stops it on its
 * own. Leaving, or losing the connection, keeps the room recorded (ADR-0083): the meeting goes on in the room.
 */
export const roomMicStopsWhen = (end: CallEnd) => end === 'movedAway' || end === 'removed'

/**
 * The chair muted the Secretary's mic in the call. With one mic, muted in the call is off: the room mic stops and its
 * take is saved, and the Secretary's own button says so, as Google Meet's does.
 */
export const roomMicStopsOnMute = (mutedByMe: boolean) => !mutedByMe

/** What the meeting page knows of the room mic, to decide whether it shows the floating card. */
export interface CardFacts {
  mode: 'InPerson' | 'Hybrid' | 'Online'
  /** The page is where this Secretary switches the room mic: in person, or hybrid with the call room unusable. */
  switchHere: boolean
  /** The room mic of this tab. */
  phase: RoomMicPhase
  /** A take this laptop kept from a tab cut off, to carry on or save. */
  unfinished: boolean
  /** A take is open, for a Secretary: on another device, or in another tab of this browser. */
  openTake: boolean
  /** The open take is fed by another tab of this browser. */
  otherTab: boolean
  /** The call room cannot be used here (no LiveKit, unreachable, or joining failed lately). */
  callUnusable: boolean
}

/**
 * Whether the meeting page shows the floating room-mic card. As before (ADR-0072, ADR-0083), with one exception: in a
 * hybrid meeting whose call room works, a take fed by another tab of this browser is that call room's. The call shows
 * its mic there, and in its picture-in-picture window when the Secretary is on another tab, so no card repeats it here.
 */
export function showsRoomMicCard(facts: CardFacts) {
  const { switchHere, phase, unfinished, openTake } = facts
  if (switchHere || phase !== 'off' || unfinished) return true
  if (!openTake) return false
  return !(facts.mode === 'Hybrid' && !facts.callUnusable && facts.otherTab)
}
