import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { DisconnectReason, Room, RoomEvent } from 'livekit-client'
import { getMeetingCall, joinMeetingCall } from '../../../api/calls'
import { Icon } from '../../../components/Icon'
import { NotFound } from '../../../components/NotFound'
import { Select } from '../../../components/Select'
import { useToast } from '../../../components/toastContext'
import { errorMessage, isApiError } from '../../../lib/errors'
import { routes } from '../../../lib/routes'
import type { CallRoomState } from '../../../types/call'
import type { Meeting } from '../../../types/meeting'
import { useAccessLost } from '../../auth/useAccessLost'
import { useMeeting } from '../../meetings/useMeeting'
import { RoomView } from './RoomView'
import { ROOM_OPTIONS, deviceErrorMessage, disconnectMessage } from './roomSupport'

type Phase =
  | { kind: 'lobby' }
  | { kind: 'inRoom' }
  | { kind: 'left'; reason: DisconnectReason | undefined }

interface Prefs {
  mic: boolean
  micId: string
}

const PREFS_KEY = 'trustcabinet.call.prefs'

/** A per-browser convenience: remember whether the user joins muted and with which microphone. */
function loadPrefs(): Prefs {
  try {
    const saved = JSON.parse(localStorage.getItem(PREFS_KEY) ?? '{}') as Partial<Prefs>
    return { mic: saved.mic ?? true, micId: saved.micId ?? '' }
  } catch {
    return { mic: true, micId: '' }
  }
}

function savePrefs(prefs: Prefs) {
  try {
    localStorage.setItem(PREFS_KEY, JSON.stringify(prefs))
  } catch {
    // Private mode or blocked storage: the choice just is not remembered.
  }
}

/**
 * The meeting call room (SCRUM-41, ADR-0010). Full-screen, outside the app shell: navigating this
 * tab elsewhere drops the call, so nothing here links away in the same tab.
 *
 * One Room object lives as long as the page. The token is fetched when the user clicks "join" —
 * not in an effect, which StrictMode would run twice, and not on page load, since it expires in
 * minutes.
 */
export function MeetingRoomPage() {
  const { meetingId } = useParams()
  const { state: meetingState } = useMeeting(meetingId)
  const [call, setCall] = useState<CallRoomState | 'loading' | 'notFound' | { error: string }>('loading')
  const handleAccessLost = useAccessLost()

  useEffect(() => {
    if (!meetingId) return
    let cancelled = false
    getMeetingCall(meetingId)
      .then((state) => !cancelled && setCall(state))
      .catch((err: unknown) => {
        if (cancelled || handleAccessLost(err)) return
        setCall(isApiError(err, 404) ? 'notFound' : { error: errorMessage(err) })
      })
    return () => {
      cancelled = true
    }
  }, [meetingId, handleAccessLost])

  if (meetingState.kind === 'notFound' || call === 'notFound') return <NotFound />

  if (meetingState.kind === 'error' || (typeof call === 'object' && 'error' in call)) {
    const message = meetingState.kind === 'error' ? meetingState.message : (call as { error: string }).error
    return <LobbyMessage meetingId={meetingId!} title="Không tải được phòng họp" text={message} />
  }

  if (meetingState.kind !== 'loaded' || call === 'loading') {
    return (
      <div className="room-lobby">
        <p className="page-loading">Đang tải phòng họp…</p>
      </div>
    )
  }

  return <RoomLifecycle meeting={meetingState.meeting} call={call} />
}

function RoomLifecycle({ meeting, call }: { meeting: Meeting; call: CallRoomState }) {
  const [room] = useState(() => new Room(ROOM_OPTIONS))
  const [phase, setPhase] = useState<Phase>({ kind: 'lobby' })
  const leavingRef = useRef(false)
  const navigate = useNavigate()

  useEffect(() => {
    function onDisconnected(reason?: DisconnectReason) {
      if (leavingRef.current) return
      setPhase({ kind: 'left', reason })
    }
    room.on(RoomEvent.Disconnected, onDisconnected)
    return () => {
      room.off(RoomEvent.Disconnected, onDisconnected)
    }
  }, [room])

  // Leaving the page (Back, closing the route) leaves the room.
  useEffect(() => () => void room.disconnect(), [room])

  const leave = useCallback(async () => {
    leavingRef.current = true
    await room.disconnect()
    navigate(routes.meeting(meeting.id))
  }, [room, navigate, meeting.id])

  if (call.status !== 'Open') {
    const text = {
      NotStarted: 'Phòng họp chỉ mở khi cuộc họp đang diễn ra.',
      Ended: 'Cuộc họp đã kết thúc, phòng họp đã đóng.',
      InPersonOnly: 'Cuộc họp này họp trực tiếp nên không có phòng họp trực tuyến.',
      Open: '',
    }[call.status]
    return <LobbyMessage meetingId={meeting.id} title={meeting.title} text={text} />
  }

  if (!call.available) {
    return (
      <LobbyMessage
        meetingId={meeting.id}
        title={meeting.title}
        text="Phòng họp trực tuyến chưa sẵn sàng. Vui lòng liên hệ quản trị viên."
      />
    )
  }

  if (phase.kind === 'inRoom') {
    return <RoomView room={room} meeting={meeting} myRole={call.myRole} onLeave={leave} />
  }

  if (phase.kind === 'left') {
    const { text, canRejoin } = disconnectMessage(phase.reason)
    return (
      <LobbyMessage meetingId={meeting.id} title={meeting.title} text={text}>
        {canRejoin && (
          <button type="button" className="btn btn-primary" onClick={() => setPhase({ kind: 'lobby' })}>
            Vào lại
          </button>
        )}
      </LobbyMessage>
    )
  }

  return <PreJoin room={room} meeting={meeting} call={call} onJoined={() => setPhase({ kind: 'inRoom' })} />
}

function PreJoin({
  room,
  meeting,
  call,
  onJoined,
}: {
  room: Room
  meeting: Meeting
  call: CallRoomState
  onJoined: () => void
}) {
  const [prefs, setPrefs] = useState(loadPrefs)
  const [mics, setMics] = useState<MediaDeviceInfo[]>([])
  const [deviceError, setDeviceError] = useState<string | null>(null)
  const [joinError, setJoinError] = useState<string | null>(null)
  const [joining, setJoining] = useState(false)
  const toast = useToast()
  const handleAccessLost = useAccessLost()

  useEffect(() => {
    let cancelled = false
    // Asking for the device list asks for permission too, so a refusal shows up here, before joining.
    Room.getLocalDevices('audioinput')
      .then((devices) => !cancelled && setMics(devices.filter((d) => d.deviceId)))
      .catch((err: unknown) => !cancelled && setDeviceError(deviceErrorMessage(err, 'micro')))
    return () => {
      cancelled = true
    }
  }, [])

  function update(next: Prefs) {
    setPrefs(next)
    savePrefs(next)
  }

  async function join() {
    setJoining(true)
    setJoinError(null)
    try {
      const ticket = await joinMeetingCall(meeting.id)
      await room.connect(ticket.serverUrl, ticket.token)
      onJoined()
      if (prefs.mic && !deviceError) {
        try {
          await room.localParticipant.setMicrophoneEnabled(true, prefs.micId ? { deviceId: prefs.micId } : undefined)
        } catch (err) {
          toast.show(deviceErrorMessage(err, 'micro'), 'error')
        }
      }
    } catch (err) {
      if (handleAccessLost(err)) return
      setJoinError(errorMessage(err))
    } finally {
      setJoining(false)
    }
  }

  const micOptions = [
    { value: '', label: 'Micro mặc định của máy' },
    ...mics.map((d, i) => ({ value: d.deviceId, label: d.label || `Micro ${i + 1}` })),
  ]

  return (
    <div className="room-lobby">
      <section className="room-lobby-card" aria-labelledby="prejoin-title">
        <p className="room-lobby-eyebrow">Phòng họp trực tuyến</p>
        <h1 id="prejoin-title">{meeting.title}</h1>
        <p className="room-lobby-meta">
          {call.participantCount === null
            ? 'Cuộc họp đang diễn ra.'
            : call.participantCount === 0
              ? 'Chưa có ai trong phòng.'
              : `Đang có ${call.participantCount} người trong phòng.`}
        </p>

        <div className="room-prejoin-controls">
          <button
            type="button"
            className={`room-toggle${prefs.mic && !deviceError ? '' : ' room-toggle-off'}`}
            aria-pressed={prefs.mic && !deviceError}
            disabled={Boolean(deviceError)}
            onClick={() => update({ ...prefs, mic: !prefs.mic })}
          >
            <Icon name={prefs.mic && !deviceError ? 'mic' : 'micOff'} />
            {prefs.mic && !deviceError ? 'Micro bật khi vào phòng' : 'Vào phòng với micro tắt'}
          </button>
          {mics.length > 1 && (
            <Select
              label="Chọn micro"
              value={prefs.micId}
              options={micOptions}
              onChange={(micId) => update({ ...prefs, micId })}
            />
          )}
        </div>

        {deviceError && (
          <div className="alert alert-warning" role="status">
            <p>{deviceError}</p>
          </div>
        )}
        {joinError && (
          <div className="alert alert-error" role="alert">
            <p>{joinError}</p>
          </div>
        )}

        <div className="room-lobby-actions">
          <Link className="btn btn-outline" to={routes.meeting(meeting.id)}>
            Quay lại cuộc họp
          </Link>
          <button type="button" className="btn btn-primary" disabled={joining} onClick={() => void join()}>
            {joining ? 'Đang vào phòng…' : 'Vào phòng'}
          </button>
        </div>
      </section>
    </div>
  )
}

function LobbyMessage({
  meetingId,
  title,
  text,
  children,
}: {
  meetingId: string
  title: string
  text: string
  children?: React.ReactNode
}) {
  return (
    <div className="room-lobby">
      <section className="room-lobby-card">
        <p className="room-lobby-eyebrow">Phòng họp trực tuyến</p>
        <h1>{title}</h1>
        <p className="room-lobby-meta">{text}</p>
        <div className="room-lobby-actions">
          <Link className="btn btn-outline" to={routes.meeting(meetingId)}>
            Về trang cuộc họp
          </Link>
          {children}
        </div>
      </section>
    </div>
  )
}
