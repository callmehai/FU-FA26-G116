/**
 * ADR-0072 — whether this tab holds a room mic right now (on, or still saving its take). While it does, the meeting
 * page and the call room share this tab: going from one to the other stays here rather than opening or closing a
 * tab, so the mic goes with the Secretary instead of stopping.
 */
const held = new Set<string>()

export function holdRoomMic(meetingId: string, holding: boolean) {
  if (holding) held.add(meetingId)
  else held.delete(meetingId)
}

export function holdsRoomMic() {
  return held.size > 0
}
