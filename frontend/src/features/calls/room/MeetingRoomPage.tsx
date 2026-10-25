import { type ReactNode, useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  DisconnectReason,
  type LocalAudioTrack,
  type LocalVideoTrack,
  Room,
  RoomEvent,
  createLocalAudioTrack,
  createLocalVideoTrack,
} from 'livekit-client'
import { getMeetingCall, joinMeetingCall } from '../../../api/calls'
import { Icon } from '../../../components/Icon'
import { NotFound } from '../../../components/NotFound'
import { BrandMark } from '../../../components/BrandMark'
import { useToast } from '../../../components/toastContext'
import { avatarColors, initials } from '../../../lib/avatar'
import { errorMessage, isApiError } from '../../../lib/errors'
import { routes } from '../../../lib/routes'
import type { CallRoomState } from '../../../types/call'
import type { Meeting } from '../../../types/meeting'
import { useAccessLost } from '../../auth/useAccessLost'
import { useAuth } from '../../auth/useAuth'
import { SensitivityBadge } from '../../meetings/StatusBadge'
import { useMeeting } from '../../meetings/useMeeting'
import { useNotifications } from '../../notifications/notificationsContext'
import { DevicePicker, MicLevel } from './devices'
import { RoomView } from './RoomView'
import { ROOM_OPTIONS, deviceErrorMessage, deviceOptions, disconnectMessage, noDeviceReason } from './roomSupport'

type Phase = { kind: 'lobby' } | { kind: 'inRoom' } | { kind: 'left'; reason: DisconnectReason | undefined }

interface Prefs {
  mic: boolean
  micId: string
  micLabel: string
  speakerId: string
  speakerLabel: string
  cam: boolean
  camId: string
  camLabel: string
}

const PREFS_KEY = 'trustcabinet.call.prefs'

/** A per-browser convenience: remember whether the user joins muted, and with which devices. */
function loadPrefs(): Prefs {
  try {
    const saved = JSON.parse(localStorage.getItem(PREFS_KEY) ?? '{}') as Partial<Prefs>
    // Microphone on, camera off unless the user chose otherwise before (SCRUM-42).
    return {
      mic: saved.mic ?? true,
      micId: saved.micId ?? '',
      micLabel: saved.micLabel ?? '',
      speakerId: saved.speakerId ?? '',
      speakerLabel: saved.speakerLabel ?? '',
      cam: saved.cam ?? false,
      camId: saved.camId ?? '',
      camLabel: saved.camLabel ?? '',
    }
  } catch {
    return { mic: true, micId: '', micLabel: '', speakerId: '', speakerLabel: '', cam: false, camId: '', camLabel: '' }
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
  const { state: meetingState, reload: reloadMeeting } = useMeeting(meetingId)
  const [call, setCall] = useState<CallRoomState | 'loading' | 'notFound' | { error: string }>('loading')
  const handleAccessLost = useAccessLost()

  const readCall = useCallback(
    (isCancelled: () => boolean) => {
      if (!meetingId) return
      getMeetingCall(meetingId)
        .then((state) => !isCancelled() && setCall(state))
        .catch((err: unknown) => {
          if (isCancelled() || handleAccessLost(err)) return
          setCall(isApiError(err, 404) ? 'notFound' : { error: errorMessage(err) })
        })
    },
    [meetingId, handleAccessLost],
  )

  useEffect(() => {
    let cancelled = false
    readCall(() => cancelled)
    return () => {
      cancelled = true
    }
  }, [readCall])

  // Someone waiting here before the start learns that the Chair started only from the notification
  // it sends them (SCRUM-69): re-read then, so "chỉ mở khi cuộc họp đang diễn ra" becomes the lobby.
  const { lastArrival } = useNotifications()
  const seenArrival = useRef(lastArrival)
  useEffect(() => {
    if (!lastArrival || lastArrival === seenArrival.current) return
    seenArrival.current = lastArrival
    if (lastArrival.type !== 'MeetingStarted' || lastArrival.meetingId?.toLowerCase() !== meetingId?.toLowerCase())
      return
    void reloadMeeting()
    readCall(() => false)
  }, [lastArrival, meetingId, reloadMeeting, readCall])

  if (meetingState.kind === 'notFound' || call === 'notFound') return <NotFound />

  if (meetingState.kind === 'error' || (typeof call === 'object' && 'error' in call)) {
    const message = meetingState.kind === 'error' ? meetingState.message : (call as { error: string }).error
    return <LobbyMessage meetingId={meetingId!} title="Không tải được phòng họp" text={message} />
  }

  if (meetingState.kind !== 'loaded' || call === 'loading') {
    return (
      <RoomPage>
        <p className="page-loading prejoin-loading">Đang tải phòng họp…</p>
      </RoomPage>
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
  const [micAllowed, setMicAllowed] = useState(false)
  const [micError, setMicError] = useState<string | null>(null)
  const [joinError, setJoinError] = useState<string | null>(null)
  const [joining, setJoining] = useState(false)
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const micOn = prefs.mic && !micError
  // Labels only come once a permission is granted, so the lists are read again then.
  const { devices, reload: reloadDevices } = useDeviceList(micAllowed)
  const ofKind = (kind: MediaDeviceKind) => devices.filter((d) => d.kind === kind)
  const micId = savedDevice(ofKind('audioinput'), prefs.micId, prefs.micLabel)
  const speakerId = savedDevice(ofKind('audiooutput'), prefs.speakerId, prefs.speakerLabel)
  const camId = savedDevice(ofKind('videoinput'), prefs.camId, prefs.camLabel)
  const {
    attachVideo,
    ready: previewReady,
    error: previewError,
    release: releasePreview,
  } = useCameraPreview(prefs.cam, camId, reloadDevices)
  const { track: micTrack, release: releaseMic } = useMicPreview(micOn && micAllowed, micId)
  const user = useAuth().user
  // The room has no identity before connecting; the preview uses the signed-in name.
  const myName = user?.fullName ?? ''

  useEffect(() => {
    let cancelled = false
    // Asking for the device list asks for permission too, so a refusal shows up here, before joining.
    Room.getLocalDevices('audioinput')
      .then(() => !cancelled && setMicAllowed(true))
      .catch((err: unknown) => !cancelled && setMicError(deviceErrorMessage(err, 'micro')))
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
      // The previews hold the camera and microphone; release them so publishing can open them again.
      releasePreview()
      releaseMic()
      await room.connect(ticket.serverUrl, ticket.token)
      onJoined()
      if (speakerId && CAN_PICK_SPEAKER) {
        await room.switchActiveDevice('audiooutput', speakerId).catch(() => undefined)
      }
      if (micOn) {
        try {
          await room.localParticipant.setMicrophoneEnabled(true, micId ? { deviceId: micId } : undefined)
        } catch (err) {
          toast.show(deviceErrorMessage(err, 'micro'), 'error')
        }
      }
      if (prefs.cam && !previewError) {
        try {
          await room.localParticipant.setCameraEnabled(true, camId ? { deviceId: camId } : undefined)
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

  const camOn = prefs.cam && !previewError
  const restricted = meeting.sensitivity === 'Secret' || meeting.sensitivity === 'Confidential'
  const labelOf = (id: string) => devices.find((d) => d.deviceId === id)?.label ?? ''

  return (
    <RoomPage>
      <main className="prejoin-main">
        <div className="prejoin-media">
          {/* Laid out as Meet's: your picture, its two switches over its bottom edge, the pickers under it. */}
          <div className="prejoin-preview" role="group" aria-label="Xem trước hình của bạn">
            {camOn && <video ref={attachVideo} className="prejoin-preview-video" muted playsInline autoPlay />}
            {(!camOn || !previewReady) && (
              <p className="prejoin-preview-text">{camOn ? 'Đang mở camera…' : 'Camera đang tắt'}</p>
            )}
            {camOn && previewReady && <span className="prejoin-preview-name">{myName}</span>}
            {micTrack && (
              <span className="prejoin-voice" title="Micro đang nhận tiếng">
                <MicLevel track={micTrack} />
              </span>
            )}
            <div className="prejoin-toggles">
              <button
                type="button"
                className={`prejoin-toggle${micOn ? '' : ' is-off'}`}
                aria-label={micOn ? 'Tắt micro' : 'Bật micro'}
                title={micOn ? 'Tắt micro' : 'Bật micro'}
                disabled={Boolean(micError)}
                onClick={() => update({ ...prefs, mic: !prefs.mic })}
              >
                <Icon name={micOn ? 'mic' : 'micOff'} />
              </button>
              <button
                type="button"
                className={`prejoin-toggle${camOn ? '' : ' is-off'}`}
                aria-label={camOn ? 'Tắt camera' : 'Bật camera'}
                title={camOn ? 'Tắt camera' : 'Bật camera'}
                onClick={() => update({ ...prefs, cam: !prefs.cam })}
              >
                <Icon name={camOn ? 'video' : 'videoOff'} />
              </button>
            </div>
          </div>
          <div className="prejoin-devices">
            <DevicePicker
              title="Micro"
              icon="mic"
              options={deviceOptions(ofKind('audioinput'), 'Micro')}
              activeId={micId}
              emptyReason={noDeviceReason('audioinput', 'Micro')}
              onPick={(id) => update({ ...prefs, micId: id, micLabel: labelOf(id) })}
            />
            {CAN_PICK_SPEAKER && (
              <DevicePicker
                title="Loa"
                icon="volume"
                options={deviceOptions(ofKind('audiooutput'), 'Loa')}
                activeId={speakerId}
                emptyReason={noDeviceReason('audiooutput', 'Loa')}
                onPick={(id) => update({ ...prefs, speakerId: id, speakerLabel: labelOf(id) })}
              />
            )}
            <DevicePicker
              title="Camera"
              icon="video"
              options={deviceOptions(ofKind('videoinput'), 'Camera')}
              activeId={camId}
              emptyReason={noDeviceReason('videoinput', 'Camera')}
              onPick={(id) => update({ ...prefs, camId: id, camLabel: labelOf(id) })}
            />
          </div>
        </div>

        <section className="prejoin-side" aria-labelledby="prejoin-title">
          <h1 id="prejoin-title">Sẵn sàng tham gia?</h1>
          <p className="prejoin-meeting">{meeting.title}</p>
          {restricted && <SensitivityBadge sensitivity={meeting.sensitivity} />}
          <p className="prejoin-presence">
            {call.participantCount === null
              ? 'Cuộc họp đang diễn ra'
              : call.participantCount === 0
                ? 'Không có người nào khác ở đây'
                : `Đang có ${call.participantCount} người trong phòng`}
          </p>

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

          <button type="button" className="prejoin-join" disabled={joining} onClick={() => void join()}>
            {joining ? 'Đang vào phòng…' : 'Vào phòng'}
          </button>
          <Link className="prejoin-text-button" to={routes.meeting(meeting.id)}>
            <Icon name="chevronLeft" size={18} />
            Quay lại trang cuộc họp
          </Link>
        </section>
      </main>
    </RoomPage>
  )
}

/**
 * The device a saved choice points at. Some browsers give devices new ids between visits (Safari,
 * Firefox, Chrome after "allow this time"), so a choice whose id is gone is found again by its name,
 * and one that matches nothing falls back to the system default.
 */
function savedDevice(devices: MediaDeviceInfo[], id: string, label: string) {
  // Before any permission the list has one entry per kind with no id: nothing to compare yet.
  const known = devices.filter((d) => d.deviceId)
  if (!id || known.length === 0 || known.some((d) => d.deviceId === id)) return id
  return known.find((d) => label !== '' && d.label === label)?.deviceId ?? ''
}

/** Choosing a speaker needs setSinkId, which Safari on a phone and older Firefox lack. */
const CAN_PICK_SPEAKER = typeof HTMLMediaElement !== 'undefined' && 'setSinkId' in HTMLMediaElement.prototype

/**
 * Every media device, read again when `refresh` changes, on `reload`, and whenever one is plugged
 * in or out.
 */
function useDeviceList(refresh: unknown) {
  const [devices, setDevices] = useState<MediaDeviceInfo[]>([])
  const reload = useCallback(() => {
    void navigator.mediaDevices?.enumerateDevices().then(setDevices, () => undefined)
  }, [])
  useEffect(() => {
    const media = navigator.mediaDevices
    if (!media?.enumerateDevices) return
    reload()
    media.addEventListener('devicechange', reload)
    return () => media.removeEventListener('devicechange', reload)
  }, [refresh, reload])
  return { devices, reload }
}

/**
 * The microphone while it is on in the pre-join screen, for its voice indicator — as Meet shows one.
 * Closed when switched off, when the device changes, on unmount, and by `release` before joining.
 */
function useMicPreview(enabled: boolean, deviceId: string) {
  const [result, setResult] = useState<{ key: string; track?: LocalAudioTrack }>({ key: '' })
  const key = enabled ? `on:${deviceId}` : 'off'
  const track = result.key === key ? result.track : undefined

  useEffect(() => {
    if (!enabled) return
    let cancelled = false
    let opened: LocalAudioTrack | undefined
    createLocalAudioTrack(deviceId ? { deviceId } : undefined)
      .then((created) => {
        if (cancelled) return created.stop()
        opened = created
        setResult({ key: `on:${deviceId}`, track: created })
      })
      // Without the indicator the screen still works; joining reports a real microphone problem.
      .catch(() => undefined)
    return () => {
      cancelled = true
      opened?.stop()
    }
  }, [enabled, deviceId])

  return { track, release: () => track?.stop() }
}

/**
 * A local camera preview (SCRUM-42): opens the camera while `enabled`, closes it when switched off,
 * when the device changes, and on unmount. `release` hands the camera over before publishing.
 * Results are keyed by device, so a stale track or error from a previous choice is never shown.
 */
function useCameraPreview(enabled: boolean, deviceId: string, onOpened?: () => void) {
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
        // A first camera permission brings the cameras' names with it.
        onOpened?.()
      })
      .catch((err: unknown) => {
        if (!cancelled) setResult({ key: `on:${deviceId}`, error: deviceErrorMessage(err, 'camera') })
      })
    return () => {
      cancelled = true
      opened?.stop()
    }
  }, [enabled, deviceId, onOpened])

  useEffect(() => {
    if (!track || !video) return
    track.attach(video)
    return () => {
      track.detach(video)
    }
  }, [track, video])

  return { attachVideo: setVideo, ready: track !== undefined, error, release: () => track?.stop() }
}

/** The room's own page outside the call, as Meet's: its mark on the left, who is signed in on the right. */
function RoomPage({ children }: { children: ReactNode }) {
  const user = useAuth().user
  const colors = avatarColors(user?.fullName ?? '')
  return (
    <div className="prejoin">
      <header className="prejoin-top">
        <span className="prejoin-brand">
          <BrandMark size={32} />
          TrustCabinet
        </span>
        {user && (
          <span className="prejoin-account">
            <span className="prejoin-account-text">
              <span className="prejoin-account-name">{user.fullName}</span>
              <span className="prejoin-account-mail">{user.email}</span>
            </span>
            <span
              className="prejoin-avatar"
              style={{ backgroundColor: colors.bg, color: colors.fg }}
              aria-hidden="true"
            >
              {initials(user.fullName)}
            </span>
          </span>
        )}
      </header>
      {children}
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
  children?: ReactNode
}) {
  return (
    <RoomPage>
      <main className="prejoin-message">
        <h1>{text}</h1>
        <p className="prejoin-meeting">{title}</p>
        <div className="prejoin-message-actions">
          <Link className="btn btn-outline" to={routes.meeting(meetingId)}>
            Về trang cuộc họp
          </Link>
          {children}
        </div>
      </main>
    </RoomPage>
  )
}
