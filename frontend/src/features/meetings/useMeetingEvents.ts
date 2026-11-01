import { HubConnectionState, type HubConnection } from '@microsoft/signalr'
import { useEffect, useRef } from 'react'
import { createHubConnection, retryDelay } from '../../api/hubs'
import type { AttendanceChangedEvent } from '../../types/attendance'
import type { DocumentsChangedEvent } from '../../types/document'
import type { AgendaChangedEvent } from '../../types/meeting'
import type { ParticipantsChangedEvent } from '../../types/participant'
import type { SpeechChangedEvent } from '../../types/speech'
import type { VoteChangedEvent } from '../../types/vote'

/**
 * What the meeting's hub announces; each only says "re-read". `agendaChanged` is any change to the
 * agenda (SCRUM-25), the item in progress included; `meetingEnded` is the one that ending the meeting
 * sends (SCRUM-69), the only change of status anyone else hears about, so it is told to both.
 * `documentsChanged` follows an upload (SCRUM-21). The per-mic transcript (SCRUM-109) says `speechChanged` for a
 * sentence added or corrected, which a read of what changed since the last one catches, and `speechRemoved` for a
 * sentence taken back or the capture mode changed, which only a full read shows. The room recordings (ADR-0041) say
 * `recordingsChanged` when a take opens or is saved, a file is kept or erased, or a transcript is done. Votes
 * (SCRUM-28) say `voteChanged` for any change, and `voteOpened` as well when the Chair opens one, so the call room
 * can tell everyone.
 */
export type MeetingEvent =
  | 'participantsChanged'
  | 'attendanceChanged'
  | 'agendaChanged'
  | 'meetingEnded'
  | 'documentsChanged'
  | 'speechChanged'
  | 'speechRemoved'
  | 'recordingsChanged'
  | 'voteChanged'
  | 'voteOpened'

interface Listener {
  events: readonly MeetingEvent[]
  notify: () => void
}

interface Channel {
  listeners: Set<Listener>
  close: () => void
  /** Set while the last listener has left, so a remount in the same tick keeps the connection. */
  closing?: number
}

/** One `/hubs/meetings` connection per meeting on screen, shared by the page and its panels. */
const channels = new Map<string, Channel>()

/**
 * Opens the connection and joins the meeting's group. Events only say "re-read": a listener is told
 * on the events it asked for (each event once, by id — delivery is at least once), and every
 * listener again once joined and whenever the connection comes back after a gap, because events
 * pushed before the join are not replayed.
 */
function open(meetingId: string): Channel {
  const connection: HubConnection = createHubConnection('/hubs/meetings')
  const listeners = new Set<Listener>()
  const seen = new Set<string>()
  let closed = false
  let attempt = 0
  let retryTimer: number | undefined
  const notify = (event?: MeetingEvent) =>
    listeners.forEach((listener) => (!event || listener.events.includes(event)) && listener.notify())

  // A meeting the caller can no longer read refuses the join; the next re-read shows why.
  const join = () =>
    connection.invoke('JoinMeeting', meetingId).then(
      () => true,
      () => false,
    )

  for (const name of ['participantsChanged', 'attendanceChanged', 'documentsChanged'] as const) {
    connection.on(name, (event: ParticipantsChangedEvent | AttendanceChangedEvent | DocumentsChangedEvent) => {
      if (event.meetingId.toLowerCase() !== meetingId.toLowerCase() || seen.has(event.eventId)) return
      seen.add(event.eventId)
      notify(name)
    })
  }
  connection.on('agendaChanged', (event: AgendaChangedEvent) => {
    if (event.meetingId.toLowerCase() !== meetingId.toLowerCase() || seen.has(event.eventId)) return
    seen.add(event.eventId)
    notify('agendaChanged')
    if (event.changeType === 'MeetingEnded') notify('meetingEnded')
  })
  connection.on('speechChanged', (event: SpeechChangedEvent) => {
    if (event.meetingId.toLowerCase() !== meetingId.toLowerCase() || seen.has(event.eventId)) return
    seen.add(event.eventId)
    notify(
      event.changeType === 'RecordingsChanged'
        ? 'recordingsChanged'
        : event.changeType === 'ClipDeleted'
          ? 'speechRemoved'
          : 'speechChanged',
    )
  })
  connection.on('voteChanged', (event: VoteChangedEvent) => {
    if (event.meetingId.toLowerCase() !== meetingId.toLowerCase() || seen.has(event.eventId)) return
    seen.add(event.eventId)
    notify('voteChanged')
    if (event.changeType === 'Opened') notify('voteOpened')
  })
  connection.onreconnected(() => void join().then(() => notify()))
  connection.onclose(() => {
    if (!closed) schedule()
  })

  function schedule() {
    window.clearTimeout(retryTimer)
    retryTimer = window.setTimeout(start, retryDelay(attempt++))
  }

  async function start() {
    if (closed || connection.state !== HubConnectionState.Disconnected) return
    try {
      await connection.start()
    } catch {
      if (!closed) schedule()
      return
    }
    await join()
    // The first join is a gap too: the page read the roster before it was in the group, and a change
    // in between (a second or so to a far server) was sent without it. Re-read once now.
    notify()
    attempt = 0
  }

  // A tab coming back to the foreground catches up at once instead of waiting for the next retry.
  function catchUp() {
    if (document.visibilityState !== 'visible' || connection.state !== HubConnectionState.Disconnected) return
    window.clearTimeout(retryTimer)
    void start()
  }

  document.addEventListener('visibilitychange', catchUp)
  window.addEventListener('online', catchUp)
  void start()

  return {
    listeners,
    close: () => {
      closed = true
      window.clearTimeout(retryTimer)
      document.removeEventListener('visibilitychange', catchUp)
      window.removeEventListener('online', catchUp)
      void connection.stop()
    },
  }
}

function subscribe(meetingId: string, listener: Listener) {
  const key = meetingId.toLowerCase()
  let channel = channels.get(key)
  if (!channel) {
    channel = open(meetingId)
    channels.set(key, channel)
  }
  window.clearTimeout(channel.closing)
  channel.listeners.add(listener)
  const current = channel
  return () => {
    current.listeners.delete(listener)
    if (current.listeners.size > 0) return
    // StrictMode unmounts and remounts at once in development: keep the connection for that.
    current.closing = window.setTimeout(() => {
      if (current.listeners.size > 0) return
      current.close()
      channels.delete(key)
    })
  }
}

const ROSTER_EVENTS: readonly MeetingEvent[] = ['participantsChanged']

/**
 * Calls `onChange` whenever one of `events` arrives — by default the roster changing (SCRUM-20) —
 * or the connection recovers from a gap. The caller re-reads what it shows; it never trusts an
 * event's payload as data.
 */
export function useMeetingEvents(
  meetingId: string,
  onChange: () => void,
  events: readonly MeetingEvent[] = ROSTER_EVENTS,
) {
  const onChangeRef = useRef(onChange)
  useEffect(() => {
    onChangeRef.current = onChange
  })
  const key = events.join()
  useEffect(
    () => subscribe(meetingId, { events: key.split(',') as MeetingEvent[], notify: () => onChangeRef.current() }),
    [meetingId, key],
  )
}
