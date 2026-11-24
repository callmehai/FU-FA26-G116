import { memo, useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { matchPath, useLocation } from 'react-router-dom'
import { getMeeting } from '../../api/meetings'
import { routes } from '../../lib/routes'
import { useMeetingEvents } from '../meetings/useMeetingEvents'
import { RecorderCard } from './RecorderCard'
import { RoomMicContext, type RoomMicHold } from './roomMicContext'
import { useRoomRecorder } from './useRoomRecorder'
import { useRoomRecordings } from './useRoomRecordings'

/**
 * ADR-0072 — holds the room mic above the routes. While the tab shows a meeting's page or its call room, the mic of
 * that meeting lives here, so going from one to the other in the same tab (the installed app, a browser that blocks
 * tabs, or a tab that holds the mic) keeps it on. ADR-0083: a mic that is on stays on when the tab goes to any other
 * page, with its card floating there, so the Secretary always sees that the room is recorded and can switch it off. An
 * idle mic is let go as before.
 */
export function RoomMicProvider({ children }: { children: ReactNode }) {
  const { pathname } = useLocation()
  const page = meetingIn(pathname)
  const [hold, setHold] = useState<RoomMicHold | null>(null)
  // A mic opening, on or saving keeps its meeting held, whatever page the tab shows now.
  const busy = hold !== null && hold.recorder.phase !== 'off'
  const meetingId = busy ? hold.meetingId : page
  const shown = meetingId && hold?.meetingId === meetingId ? hold : null
  const away = busy && shown !== null && page?.toLowerCase() !== shown.meetingId.toLowerCase()
  return (
    <>
      {meetingId && <RoomMicHost key={meetingId.toLowerCase()} meetingId={meetingId} onHold={setHold} />}
      <RoomMicContext.Provider value={shown}>{children}</RoomMicContext.Provider>
      {away && <AwayRecorderCard hold={shown} />}
    </>
  )
}

/** ADR-0083 — the card of a mic still on while the tab shows another page, or another meeting's. */
function AwayRecorderCard({ hold }: { hold: RoomMicHold }) {
  const { meetingId, recorder, listen } = hold
  const [title, setTitle] = useState('')
  useEffect(() => {
    getMeeting(meetingId).then(
      (meeting) => setTitle(meeting.title),
      () => undefined,
    )
  }, [meetingId])
  const recordings = useRoomRecordings(meetingId, true)
  const { reload } = recordings
  useEffect(() => listen(reload), [listen, reload])
  return (
    <RecorderCard
      title={title || 'Cuộc họp đang ghi'}
      room={recorder}
      list={recordings.list}
      open={recordings.open}
      onChanged={reload}
    />
  )
}

const PAGES = [routes.meeting(':meetingId'), routes.meetingRoom(':meetingId')]

function meetingIn(pathname: string) {
  for (const path of PAGES) {
    const match = matchPath(path, pathname)
    if (match?.params.meetingId) return match.params.meetingId
  }
  return null
}

/** Memoised: it draws again only when the mic changes, not with every page change around it. */
const RoomMicHost = memo(function RoomMicHost({
  meetingId,
  onHold,
}: {
  meetingId: string
  onHold: (hold: RoomMicHold | null) => void
}) {
  const [running, setRunning] = useState(true)
  const listeners = useRef(new Set<() => Promise<void> | void>())
  const changed = useCallback(async () => {
    await Promise.all([...listeners.current].map((listener) => listener()))
  }, [])
  const listen = useCallback((listener: () => Promise<void> | void) => {
    listeners.current.add(listener)
    return () => {
      listeners.current.delete(listener)
    }
  }, [])
  const recorder = useRoomRecorder(meetingId, running, changed)
  // The call room's "left" screen shows no meeting: the mic still stops when the meeting ends. The hub also calls on
  // every (re)connection to have everything read again, so the meeting itself says whether it still runs.
  const on = recorder.phase === 'on'
  useMeetingEvents(
    meetingId,
    () => {
      if (!on) return
      getMeeting(meetingId).then(
        (meeting) => setRunning(meeting.status === 'InProgress'),
        () => undefined,
      )
    },
    ['meetingEnded'],
  )

  useEffect(() => {
    onHold({ meetingId, recorder, setRunning, listen })
  }, [onHold, meetingId, recorder, listen])
  useEffect(() => () => onHold(null), [onHold])
  return null
})
