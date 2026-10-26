import { useEffect, useState } from 'react'
import { type Participant, type Room, RoomEvent } from 'livekit-client'
import { useRoleOf } from './roomRoles'

/**
 * SCRUM-44 — everyone in the call sees that the Secretary is recording it, including people who join later. The
 * recorder lives in the Secretary's tab, so the server does not know about it: while it records, that tab says so
 * on the call's data channel every few seconds, and the sign goes once the beats stop, so a Secretary whose tab
 * closed is not shown recording for ever. Only a beat from the meeting's Secretary counts.
 */

const TOPIC = 'recording'
const BEAT_MS = 4000
const STALE_MS = 10_000

export function useRecordingSign(room: Room, mine: boolean) {
  const roleOf = useRoleOf()
  const [heard, setHeard] = useState<ReadonlyMap<string, number>>(new Map())
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (!mine) return
    const say = (recording: boolean) =>
      room.localParticipant
        .publishData(new TextEncoder().encode(JSON.stringify({ recording })), { reliable: true, topic: TOPIC })
        .catch(() => undefined) // Reconnecting: the next beat goes through.
    void say(true)
    const timer = window.setInterval(() => void say(true), BEAT_MS)
    return () => {
      window.clearInterval(timer)
      void say(false)
    }
  }, [room, mine])

  useEffect(() => {
    const onData = (payload: Uint8Array, participant?: Participant, _kind?: unknown, topic?: string) => {
      if (topic !== TOPIC || !participant || roleOf(participant) !== 'Secretary') return
      let recording = false
      try {
        recording = JSON.parse(new TextDecoder().decode(payload)).recording === true
      } catch {
        return
      }
      setHeard((current) => {
        const next = new Map(current)
        if (recording) next.set(participant.identity, Date.now())
        else next.delete(participant.identity)
        return next
      })
    }
    const onLeft = (participant: Participant) =>
      setHeard((current) => {
        if (!current.has(participant.identity)) return current
        const next = new Map(current)
        next.delete(participant.identity)
        return next
      })
    room.on(RoomEvent.DataReceived, onData)
    room.on(RoomEvent.ParticipantDisconnected, onLeft)
    const timer = window.setInterval(() => setNow(Date.now()), 2000)
    return () => {
      room.off(RoomEvent.DataReceived, onData)
      room.off(RoomEvent.ParticipantDisconnected, onLeft)
      window.clearInterval(timer)
    }
  }, [room, roleOf])

  return mine || [...heard.values()].some((at) => now - at < STALE_MS)
}
