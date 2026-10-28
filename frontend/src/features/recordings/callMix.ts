import { RoomEvent, type Room } from 'livekit-client'
import type { RecordingInput } from './useRecorder'

/**
 * SCRUM-44 — what the call hears, as one stream for the Secretary's recorder: their own call mic and every other
 * participant's audio, mixed in the browser. The tracks come straight from the call, not from the loudspeaker, so
 * nobody is recorded twice and the room's echo is not in it. A muted mic is silent here as it is in the call.
 *
 * People join, leave and switch microphones during a meeting: the mix follows the call's tracks as they change.
 */
export function mixCall(room: Room): RecordingInput {
  const context = new AudioContext()
  const out = context.createMediaStreamDestination()
  const sources = new Map<string, MediaStreamAudioSourceNode>()

  const sync = () => {
    const tracks = new Map<string, MediaStreamTrack>()
    const add = (track: MediaStreamTrack | undefined) => {
      if (track && track.readyState === 'live') tracks.set(track.id, track)
    }
    for (const publication of room.localParticipant.audioTrackPublications.values())
      add(publication.track?.mediaStreamTrack)
    for (const participant of room.remoteParticipants.values())
      for (const publication of participant.audioTrackPublications.values()) add(publication.track?.mediaStreamTrack)

    for (const [id, node] of sources)
      if (!tracks.has(id)) {
        node.disconnect()
        sources.delete(id)
      }
    for (const [id, track] of tracks)
      if (!sources.has(id)) {
        const node = context.createMediaStreamSource(new MediaStream([track]))
        node.connect(out)
        sources.set(id, node)
      }
  }

  const events = [
    RoomEvent.TrackSubscribed,
    RoomEvent.TrackUnsubscribed,
    RoomEvent.LocalTrackPublished,
    RoomEvent.LocalTrackUnpublished,
    RoomEvent.ParticipantDisconnected,
  ] as const
  for (const event of events) room.on(event, sync)
  // A device switch replaces the mic's track without a room event; a slow sweep catches it.
  const timer = window.setInterval(sync, 2000)
  sync()
  void context.resume()

  return {
    stream: out.stream,
    release: () => {
      window.clearInterval(timer)
      for (const event of events) room.off(event, sync)
      for (const node of sources.values()) node.disconnect()
      sources.clear()
      out.stream.getTracks().forEach((track) => track.stop())
      void context.close()
    },
  }
}
