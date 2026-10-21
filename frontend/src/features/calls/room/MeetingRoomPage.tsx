import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { DisconnectReason, type LocalVideoTrack, Room, RoomEvent, createLocalVideoTrack } from 'livekit-client'
import { getMeetingCall, joinMeetingCall } from '../../../api/calls'
import { Icon } from '../../../components/Icon'
import { NotFound } from '../../../components/NotFound'
import { Select } from '../../../components/Select'
import { useToast } from '../../../components/toastContext'
import { avatarColors, initials } from '../../../lib/avatar'
import { errorMessage, isApiError } from '../../../lib/errors'
import { routes } from '../../../lib/routes'
import type { CallRoomState } from '../../../types/call'
import type { Meeting } from '../../../types/meeting'
import { useAccessLost } from '../../auth/useAccessLost'
import { useAuth } from '../../auth/useAuth'
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
  cam: boolean
  camId: string
}

const PREFS_KEY = 'trustcabinet.call.prefs'

/** A per-browser convenience: remember whether the user joins muted and with which microphone. */
function loadPrefs(): Prefs {
  try {
    const saved = JSON.parse(localStorage.getItem(PREFS_KEY) ?? '{}') as Partial<Prefs>
    // Microphone on, camera off unless the user chose otherwise before (SCRUM-42).
    return { mic: saved.mic ?? true, micId: saved.micId ?? '', cam: saved.cam ?? false, camId: saved.camId ?? '' }
  } catch {
    return { mic: true, micId: '', cam: false, camId: '' }
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
  const [cameras, setCameras] = useState<MediaDeviceInfo[]>([])
  const [micError, setMicError] = useState<string | null>(null)
  const [joinError, setJoinError] = useState<string | null>(null)
  const [joining, setJoining] = useState(false)
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const { attachVideo, ready: previewReady, error: previewError, release: releasePreview } = useCameraPreview(
    prefs.cam,
    prefs.camId,
  )
  // The room has no identity before connecting; the pre-join avatar uses the signed-in name.
  const myName = useAuth().user?.fullName ?? ''

  useEffect(() => {
    let cancelled = false
    // Asking for the device list asks for permission too, so a refusal shows up here, before joining.
    Room.getLocalDevices('audioinput')
      .then((devices) => !cancelled && setMics(devices.filter((d) => d.deviceId)))
      .catch((err: unknown) => !cancelled && setMicError(deviceErrorMessage(err, 'micro')))
    // Cameras without asking: the camera is off by default and the preview asks when turned on.
    Room.getLocalDevices('videoinput', false)
      .then((devices) => !cancelled && setCameras(devices.filter((d) => d.deviceId)))
      .catch(() => undefined)
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
      // The preview holds the camera; release it so publishing can open it again.
      releasePreview()
      await room.connect(ticket.serverUrl, ticket.token)
      onJoined()
      if (prefs.mic && !micError) {
        try {
          await room.localParticipant.setMicrophoneEnabled(true, prefs.micId ? { deviceId: prefs.micId } : undefined)
        } catch (err) {
          toast.show(deviceErrorMessage(err, 'micro'), 'error')
        }
      }
      if (prefs.cam && !previewError) {
        try {
          await room.localParticipant.setCameraEnabled(true, prefs.camId ? { deviceId: prefs.camId } : undefined)
        } catch (err) {
          toast.show(deviceErrorMessage(err, 'camera'), 'error')
        }
      }
    } catch (err) {
      if (handleAccessLost(err)) return
      setJoinError(errorMessage(err))
    } finally {
      setJoining(false)
    }
  }

  const micOn = prefs.mic && !micError
  const camOn = prefs.cam && !previewError
  const colors = avatarColors(myName)

  return (
    <div className="room-lobby">
      <section className="room-lobby-card room-lobby-card-wide" aria-labelledby="prejoin-title">
        <div className="room-preview" aria-label="Xem trước hình của bạn">
          {camOn ? (
            <video ref={attachVideo} className="room-preview-video" muted playsInline autoPlay />
          ) : (
            <span className="room-tile-avatar" style={{ backgroundColor: colors.bg, color: colors.fg }} aria-hidden="true">
              {initials(myName)}
            </span>
          )}
          <span className="room-preview-caption">
            {!camOn ? 'Camera đang tắt' : previewReady ? 'Hình của bạn' : 'Đang mở camera… Nếu trình duyệt hỏi, hãy chọn Cho phép.'}
          </span>
        </div>

        <div className="room-lobby-side">
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
            <div className="room-prejoin-row">
              <button
                type="button"
                className={`room-toggle${micOn ? '' : ' room-toggle-off'}`}
                aria-pressed={micOn}
                disabled={Boolean(micError)}
                onClick={() => update({ ...prefs, mic: !prefs.mic })}
              >
                <Icon name={micOn ? 'mic' : 'micOff'} />
                {micOn ? 'Micro bật' : 'Micro tắt'}
              </button>
              <button
                type="button"
                className={`room-toggle${camOn ? '' : ' room-toggle-off'}`}
                aria-pressed={camOn}
                onClick={() => update({ ...prefs, cam: !prefs.cam })}
              >
                <Icon name={camOn ? 'video' : 'videoOff'} />
                {camOn ? 'Camera bật' : 'Camera tắt'}
              </button>
            </div>
            {mics.length > 1 && (
              <Select
                label="Chọn micro"
                value={prefs.micId}
                options={deviceOptions(mics, 'Micro')}
                onChange={(micId) => update({ ...prefs, micId })}
              />
            )}
            {camOn && cameras.length > 1 && (
              <Select
                label="Chọn camera"
                value={prefs.camId}
                options={deviceOptions(cameras, 'Camera')}
                onChange={(camId) => update({ ...prefs, camId })}
              />
            )}
          </div>

          {micError && (
            <div className="alert alert-warning" role="status">
              <p>{micError}</p>
            </div>
          )}
          {prefs.cam && previewError && (
            <div className="alert alert-warning" role="status">
              <p>{previewError}</p>
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
        </div>
      </section>
    </div>
  )
}

function deviceOptions(devices: MediaDeviceInfo[], noun: string) {
  return [
    { value: '', label: `${noun} mặc định của máy` },
    ...devices.map((d, i) => ({ value: d.deviceId, label: d.label || `${noun} ${i + 1}` })),
  ]
}

/**
 * A local camera preview (SCRUM-42): opens the camera while `enabled`, closes it when switched off,
 * when the device changes, and on unmount. `release` hands the camera over before publishing.
 * Results are keyed by device, so a stale track or error from a previous choice is never shown.
 */
function useCameraPreview(enabled: boolean, deviceId: string) {
  const [result, setResult] = useState<{ key: string; track?: LocalVideoTrack; error?: string }>({ key: '' })
  const [video, setVideo] = useState<HTMLVideoElement | null>(null)
  const key = enabled ? `on:${deviceId}` : 'off'
  const track = result.key === key ? result.track : undefined
  const error = result.key === key ? result.error : undefined

  useEffect(() => {
    if (!enabled) return
    let cancelled = false
    let opened: LocalVideoTrack | undefined
    createLocalVideoTrack(deviceId ? { deviceId } : undefined)
      .then((created) => {
        if (cancelled) return created.stop()
        opened = created
        setResult({ key: `on:${deviceId}`, track: created })
      })
      .catch((err: unknown) => {
        if (!cancelled) setResult({ key: `on:${deviceId}`, error: deviceErrorMessage(err, 'camera') })
      })
    return () => {
      cancelled = true
      opened?.stop()
    }
  }, [enabled, deviceId])

  useEffect(() => {
    if (!track || !video) return
    track.attach(video)
    return () => {
      track.detach(video)
    }
  }, [track, video])

  return { attachVideo: setVideo, ready: track !== undefined, error, release: () => track?.stop() }
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
