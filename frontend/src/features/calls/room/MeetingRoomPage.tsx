import { type ReactNode, useCallback, useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import {
  DisconnectReason,
  type LocalAudioTrack,
  type LocalVideoTrack,
  type MediaDeviceFailure,
  Room,
  RoomEvent,
  createLocalAudioTrack,
  createLocalVideoTrack,
} from 'livekit-client'
import { getMeetingCall, joinMeetingCall } from '../../../api/calls'
import { listSpeechClips } from '../../../api/speech'
import { Icon } from '../../../components/Icon'
import { InfoTip } from '../../../components/InfoTip'
import { NotFound } from '../../../components/NotFound'
import { BrandMark } from '../../../components/BrandMark'
import { useToast } from '../../../components/toastContext'
import { avatarColors, initials } from '../../../lib/avatar'
import { errorMessage, isApiError } from '../../../lib/errors'
import { routes } from '../../../lib/routes'
import type { CallRoomState } from '../../../types/call'
import type { GuestMeeting } from '../../../types/guest'
import type { Meeting, Sensitivity } from '../../../types/meeting'
import { useAccessLost } from '../../auth/useAccessLost'
import { useAuth } from '../../auth/useAuth'
import { guestName, useGuest, useMeetingHome } from '../../guest/guestContext'
import { SensitivityBadge } from '../../meetings/StatusBadge'
import { useMeeting } from '../../meetings/useMeeting'
import { markCallFailed } from '../callFailed'
import { returnToMeetingTab, useTabName } from '../meetingTabs'
import { MeetingPageLink } from '../TabLinks'
import { VoiceShortcut } from '../../voice/VoiceShortcut'
import { useNotifications } from '../../notifications/notificationsContext'
import { lobbyMicOn, lobbyMicPress, roomMicStopsWhen } from '../../recordings/hybridMic'
import { useHeldRoomMic } from '../../recordings/roomMicContext'
import type { RoomRecorder } from '../../recordings/useRoomRecorder'
import { DevicePicker, MicLevel } from './devices'
import { RoomView, type LobbyMic } from './RoomView'
import {
  ROOM_OPTIONS,
  callEnd,
  deviceErrorMessage,
  deviceFailure,
  devicesNotice,
  deviceOptions,
  disconnectMessage,
  noDeviceReason,
  roomMessage,
  sendsRoomMic,
} from './roomSupport'

/** What being recorded means for the person about to join, behind the lobby notice's (i). */
const RECORDING_DETAIL =
  'Khi micro bật, lời bạn được lưu chữ và tiếng từng câu. Người trong cuộc họp đọc và nghe được; bạn xoá được câu của mình.'

/**
 * ADR-0056: where the words go when the AI drafts the minutes. Only a Normal meeting's transcript (text, never audio) may
 * go to an outside AI service; Internal and Secret ones stay on the team's own machine. A guest is not told the
 * meeting's sensitivity, and the PO chose to leave the AI out of their notice (2026-11-14).
 */
function recordingDetail(sensitivity: Sensitivity | null) {
  if (!sensitivity) return RECORDING_DETAIL
  const ai =
    sensitivity === 'Normal'
      ? 'Để viết dự thảo biên bản, lời thoại (chữ, không có tiếng) có thể được gửi tới dịch vụ AI bên ngoài.'
      : 'Dự thảo biên bản do máy AI của nhóm viết; lời thoại không ra khỏi hệ thống.'
  return `${RECORDING_DETAIL} ${ai}`
}

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

/**
 * The lobby, the room and what is left after it, for a member or a guest (SCRUM-59). A guest's room is the members'
 * with less: a guest coordinates nothing. Their own mic is captured per microphone like a member's (SCRUM-129).
 */
export function RoomLifecycle({ meeting, call }: { meeting: Meeting | GuestMeeting; call: CallRoomState }) {
  const [room] = useState(() => new Room(ROOM_OPTIONS))
  const [phase, setPhase] = useState<Phase>({ kind: 'lobby' })
  const leavingRef = useRef(false)
  const connectedRef = useRef(false)
  const navigate = useNavigate()
  const home = useMeetingHome(meeting.id)
  // The room has a tab of its own (PO, 2026-11-19): named, so "Vào phòng họp" brings it forward instead of joining twice.
  const { pathname } = useLocation()
  useTabName(pathname)
  const recheckGuest = useGuest()?.recheck
  // A guest's meeting page closes with the meeting (UC-04 EX-1), so a room that is over has nowhere to send them back.
  const asGuest = recheckGuest !== undefined
  const [lobbyMic, setLobbyMic] = useState<LobbyMic>()
  // ADR-0072: a hybrid meeting's room mic outlives the call screen; a room that is over stops it. ADR-0085: the call room
  // shows it with no floating card. The lobby's mic switch and the call's mic button are the room mic; out of the call
  // with it still on (the connection dropped), the page says so in a line with its switch.
  const over = call.status === 'Ended' || call.status === 'Cancelled'
  const roomMic = useHeldRoomMic(meeting.id, over ? false : undefined)
  const roomMicRef = useRef(roomMic)
  useEffect(() => {
    roomMicRef.current = roomMic
  })
  const roomMicLine = roomMic !== null && roomMic.phase !== 'off' && <RoomMicLine mic={roomMic} />

  useEffect(() => {
    function onDisconnected(reason?: DisconnectReason) {
      // A connection that never opened is the lobby's to report (MSG480), not a lost one (MSG332).
      if (leavingRef.current || !connectedRef.current) return
      setPhase({ kind: 'left', reason })
      // ADR-0085: the room mic goes with the call. Joined from another tab or device, it is switched off here (the take
      // saved) so that it opens there; taken off the roster, it stops.
      if (roomMicStopsWhen(callEnd(reason))) roomMicRef.current?.stop()
      // Put out by the server — for a guest a revoked invitation or a lapsed session — or the meeting ended: the guest
      // page then replaces the room with its closed screen, taking the meeting off the screen.
      if (
        reason === DisconnectReason.PARTICIPANT_REMOVED ||
        reason === DisconnectReason.ROOM_DELETED ||
        reason === DisconnectReason.ROOM_CLOSED
      )
        recheckGuest?.()
    }
    room.on(RoomEvent.Disconnected, onDisconnected)
    return () => {
      room.off(RoomEvent.Disconnected, onDisconnected)
    }
  }, [room, recheckGuest])

  // Leaving the page (Back, closing the route) leaves the room.
  useEffect(() => () => void room.disconnect(), [room])

  const leave = useCallback(async () => {
    leavingRef.current = true
    await room.disconnect()
    // Back to the meeting page's tab, closing this one; here when this tab was not opened by the app.
    if (!returnToMeetingTab(home)) navigate(home)
  }, [room, navigate, home])

  if (call.status !== 'Open') {
    const text = {
      NotStarted: roomMessage.notStarted,
      Ended: roomMessage.closed,
      InPersonOnly: roomMessage.inPersonOnly,
      Cancelled: roomMessage.cancelled,
      Open: '',
    }[call.status]
    return (
      <LobbyMessage
        meetingId={meeting.id}
        title={meeting.title}
        text={text}
        showHome={!(asGuest && over)}
        line={roomMicLine}
      />
    )
  }

  if (!call.available) {
    return (
      <LobbyMessage meetingId={meeting.id} title={meeting.title} text={roomMessage.notConfigured} line={roomMicLine} />
    )
  }

  if (phase.kind === 'inRoom') {
    return <RoomView room={room} meeting={meeting} myRole={call.myRole} lobbyMic={lobbyMic} onLeave={leave} />
  }

  if (phase.kind === 'left') {
    const { text, canRejoin } = disconnectMessage(phase.reason)
    // Taken off the roster (MSG330): the meeting page is a 404 to them now, so the way out is the meeting list.
    const removed = phase.reason === DisconnectReason.PARTICIPANT_REMOVED
    return (
      <LobbyMessage
        meetingId={meeting.id}
        title={meeting.title}
        text={text}
        showHome={!asGuest || canRejoin}
        toList={removed}
        line={roomMicLine}
      >
        {canRejoin && (
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              connectedRef.current = false
              setPhase({ kind: 'lobby' })
            }}
          >
            Vào lại
          </button>
        )}
      </LobbyMessage>
    )
  }

  return (
    <PreJoin
      room={room}
      meeting={meeting}
      call={call}
      roomMic={sendsRoomMic(meeting, call.myRole) ? roomMic : null}
      onJoined={(mic) => {
        connectedRef.current = true
        setLobbyMic(mic)
        setPhase({ kind: 'inRoom' })
      }}
    />
  )
}

/**
 * How many days a sentence is kept, when the meeting records what is said in the call; null when it does not.
 * A guest's comes from GET …/call: they are always told, before joining, since the room records them either way.
 */
function useRecordingNotice(meetingId: string, guestDays: number | null) {
  const asGuest = guestDays !== null
  const [days, setDays] = useState<number | null>(guestDays)
  useEffect(() => {
    // A guest is always told: the room records their mic either way (SCRUM-129).
    if (asGuest) return
    let cancelled = false
    // Asking for what changed from now on brings the meeting's terms without its sentences.
    listSpeechClips(meetingId, new Date().toISOString()).then(
      (list) => !cancelled && setDays(list.canCapture ? list.retentionDays : null),
      // Without it the room still works; the notice waits for the next visit.
      () => undefined,
    )
    return () => {
      cancelled = true
    }
  }, [meetingId, asGuest])
  return days
}

function PreJoin({
  room,
  meeting,
  call,
  roomMic,
  onJoined,
}: {
  room: Room
  meeting: Meeting | GuestMeeting
  call: CallRoomState
  /** A hybrid meeting's Secretary: the room mic, which the lobby's mic switch shows and switches (ADR-0085). */
  roomMic: RoomRecorder | null
  /** `mic`: the lobby's mic switch and the mic picked there. */
  onJoined: (mic: LobbyMic) => void
}) {
  const guest = useGuest()
  // A guest confirms the recording notice before the join button works (SCRUM-59); the server keeps that as evidence.
  const [acknowledged, setAcknowledged] = useState(false)
  const [prefs, setPrefs] = useState(loadPrefs)
  const [micAllowed, setMicAllowed] = useState(false)
  const [micFailure, setMicFailure] = useState<MediaDeviceFailure | null>(null)
  const [joinError, setJoinError] = useState<string | null>(null)
  const [joining, setJoining] = useState(false)
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  // ADR-0085: a hybrid meeting's room mic still on in this tab is the lobby's mic, whatever was chosen here last time.
  const roomPhase = roomMic?.phase ?? null
  const micOn = lobbyMicOn(prefs.mic, roomPhase) && !micFailure
  // Labels only come once a permission is granted, so the lists are read again then.
  const { devices, reload: reloadDevices } = useDeviceList(micAllowed)
  const ofKind = (kind: MediaDeviceKind) => devices.filter((d) => d.kind === kind)
  const micId = savedDevice(ofKind('audioinput'), prefs.micId, prefs.micLabel)
  const speakerId = savedDevice(ofKind('audiooutput'), prefs.speakerId, prefs.speakerLabel)
  const camId = savedDevice(ofKind('videoinput'), prefs.camId, prefs.camLabel)
  const {
    attachVideo,
    ready: previewReady,
    failure: previewFailure,
    release: releasePreview,
  } = useCameraPreview(prefs.cam, camId, reloadDevices)
  const { track: micTrack, release: releaseMic } = useMicPreview(micOn && micAllowed, micId)
  const user = useAuth().user
  // The room has no identity before connecting; the preview uses the signed-in name, or the name a guest gave.
  const myName = guest ? guestName(guest.session) : (user?.fullName ?? '')
  const retentionDays = useRecordingNotice(meeting.id, guest ? call.retentionDays : null)
  const home = useMeetingHome(meeting.id)

  useEffect(() => {
    let cancelled = false
    // Asking for the device list asks for permission too, so a refusal shows up here, before joining.
    Room.getLocalDevices('audioinput')
      .then(() => !cancelled && setMicAllowed(true))
      .catch((err: unknown) => !cancelled && setMicFailure(deviceFailure(err)))
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
      const ticket = await joinMeetingCall(meeting.id, guest ? { recordingNoticeAcknowledged: true } : undefined)
      // The previews hold the camera and microphone; release them so publishing can open them again.
      releasePreview()
      releaseMic()
      try {
        await room.connect(ticket.serverUrl, ticket.token)
      } catch {
        // Never in, so nothing was lost: the lobby stays, says so, and "Vào phòng" tries again.
        setJoinError(roomMessage.unreachable)
        // ADR-0072: a hybrid meeting's Secretary then records the room from the meeting page's card.
        markCallFailed(meeting.id, true)
        return
      }
      markCallFailed(meeting.id, false)
      onJoined({ on: micOn, deviceId: micId || undefined })
      if (speakerId && CAN_PICK_SPEAKER) {
        await room.switchActiveDevice('audiooutput', speakerId).catch(() => undefined)
      }
      // A hybrid meeting's Secretary has no call mic of their own: the room opens its room mic instead (ADR-0072).
      if (micOn && !sendsRoomMic(meeting, call.myRole)) {
        try {
          await room.localParticipant.setMicrophoneEnabled(true, micId ? { deviceId: micId } : undefined)
        } catch (err) {
          toast.show(deviceErrorMessage(err, 'micro'), 'error')
        }
      }
      if (prefs.cam && !previewFailure) {
        try {
          await room.localParticipant.setCameraEnabled(true, camId ? { deviceId: camId } : undefined)
        } catch (err) {
          toast.show(deviceErrorMessage(err, 'camera'), 'error')
        }
      }
    } catch (err) {
      if (handleAccessLost(err)) return
      setJoinError(errorMessage(err))
      markCallFailed(meeting.id, true)
    } finally {
      setJoining(false)
    }
  }

  const camOn = prefs.cam && !previewFailure
  // The Secretary's switch is the room mic's, as the call's button is (ADR-0072).
  const micLabel = roomMic ? (micOn ? 'Tắt mic phòng' : 'Bật mic phòng') : micOn ? 'Tắt micro' : 'Bật micro'
  // Micro and camera refused alike (a blocked site) are said once, not in two near-identical notices.
  const deviceNotice = devicesNotice(micFailure, prefs.cam ? (previewFailure ?? null) : null)
  const sensitivity = 'sensitivity' in meeting ? meeting.sensitivity : null
  const restricted = sensitivity === 'Secret'
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
                aria-label={micLabel}
                title={micLabel}
                disabled={Boolean(micFailure) || roomPhase === 'starting' || roomPhase === 'saving'}
                onClick={() => {
                  // Off while the room mic records: the room mic goes off, its take saved, as in the call.
                  if (lobbyMicPress(roomPhase) === 'stopRoomMic') {
                    roomMic?.stop()
                    update({ ...prefs, mic: false })
                  } else update({ ...prefs, mic: !prefs.mic })
                }}
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
          {restricted && sensitivity && <SensitivityBadge sensitivity={sensitivity} />}
          <p className="prejoin-presence">
            {call.participantCount === null
              ? 'Cuộc họp đang diễn ra'
              : call.participantCount === 0
                ? 'Không có người nào khác ở đây'
                : `Đang có ${call.participantCount} người trong phòng`}
          </p>
          {/* Law 91/2025, art. 32: the people of a recorded meeting are told before it records them (ADR-0040). */}
          {guest ? (
            // Someone from outside is told, and confirms, before the room can record them (Law 91/2025, art. 32). The
            // server records nothing from a guest's mic until this was confirmed (SCRUM-129).
            <label className="prejoin-recording prejoin-consent">
              <input
                type="checkbox"
                checked={acknowledged}
                onChange={(event) => setAcknowledged(event.target.checked)}
              />
              <span>
                Tôi biết cuộc họp được ghi âm để lập biên bản, tự xoá sau {retentionDays} ngày
                <InfoTip text={recordingDetail(sensitivity)} />
              </span>
            </label>
          ) : (
            retentionDays !== null && (
              <p className="prejoin-recording">
                <Icon name="warning" size={16} />
                Cuộc họp được ghi âm để lập biên bản · tự xoá sau {retentionDays} ngày
                <InfoTip text={recordingDetail(sensitivity)} />
              </p>
            )
          )}
          {/* A member about to be recorded can leave a voice sample here; a guest has no Voice ID. */}
          {!guest && <VoiceShortcut />}

          {deviceNotice && (
            <div className="alert alert-warning" role="status">
              <p>{deviceNotice}</p>
            </div>
          )}
          {joinError && (
            <div className="alert alert-error" role="alert">
              <p>{joinError}</p>
            </div>
          )}

          <button
            type="button"
            className="prejoin-join"
            disabled={joining || (guest !== null && !acknowledged)}
            onClick={() => void join()}
          >
            {joining ? 'Đang vào phòng…' : 'Vào phòng'}
          </button>
          <MeetingPageLink className="prejoin-text-button" to={home}>
            <Icon name="chevronLeft" size={18} />
            Quay lại trang cuộc họp
          </MeetingPageLink>
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
  const [result, setResult] = useState<{ key: string; track?: LocalVideoTrack; failure?: MediaDeviceFailure }>({
    key: '',
  })
  const [video, setVideo] = useState<HTMLVideoElement | null>(null)
  const key = enabled ? `on:${deviceId}` : 'off'
  const track = result.key === key ? result.track : undefined
  const failure = result.key === key ? result.failure : undefined

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
        if (!cancelled) setResult({ key: `on:${deviceId}`, failure: deviceFailure(err) })
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

  return { attachVideo: setVideo, ready: track !== undefined, failure, release: () => track?.stop() }
}

/**
 * The room's own page outside the call, as Meet's: its mark on the left, who is signed in on the right — a guest as
 * the name they gave, never an internal session the browser may also hold.
 */
export function RoomPage({ children }: { children: ReactNode }) {
  const user = useAuth().user
  const guest = useGuest()
  const me = guest
    ? { name: guestName(guest.session), detail: 'Khách' }
    : user && { name: user.fullName, detail: user.email }
  const colors = avatarColors(me?.name ?? '')
  return (
    <div className="prejoin">
      <header className="prejoin-top">
        <span className="prejoin-brand">
          <BrandMark size={32} />
          TrustCabinet
        </span>
        {me && (
          <span className="prejoin-account">
            <span className="prejoin-account-text">
              <span className="prejoin-account-name">{me.name}</span>
              <span className="prejoin-account-mail">{me.detail}</span>
            </span>
            <span
              className="prejoin-avatar"
              style={{ backgroundColor: colors.bg, color: colors.fg }}
              aria-hidden="true"
            >
              {initials(me.name)}
            </span>
          </span>
        )}
      </header>
      {children}
    </div>
  )
}

/**
 * ADR-0085 — out of the call (the connection dropped, or the room cannot be joined) while a hybrid meeting's room mic
 * still records: said in the page, with its switch, rather than in a floating card. The room goes on recording, as the
 * meeting goes on in the room.
 */
function RoomMicLine({ mic }: { mic: RoomRecorder }) {
  const saving = mic.phase === 'saving'
  return (
    <p className="room-mic-line" role="status">
      <span className={`mic-status-dot${saving ? '' : ' is-live'}`} aria-hidden="true" />
      {saving ? 'Đang lưu lượt ghi…' : 'Mic phòng vẫn đang ghi'}
      {!saving && (
        <button type="button" className="btn btn-outline btn-sm" onClick={mic.stop} disabled={mic.phase !== 'on'}>
          <Icon name="micOff" size={16} />
          Tắt mic phòng
        </button>
      )}
    </p>
  )
}

export function LobbyMessage({
  meetingId,
  title,
  text,
  showHome = true,
  toList = false,
  line,
  children,
}: {
  meetingId: string
  title: string
  text: string
  /** False when the meeting page is gone too: a guest's, once the meeting ended or the invitation was revoked. */
  showHome?: boolean
  /** The way back leads to the meeting list instead: a member who may no longer open the meeting page. */
  toList?: boolean
  /** A line under the title: the room mic still on out of the call (ADR-0085). */
  line?: ReactNode
  children?: ReactNode
}) {
  const meetingHome = useMeetingHome(meetingId)
  return (
    <RoomPage>
      <main className="prejoin-message">
        <h1>{text}</h1>
        <p className="prejoin-meeting">{title}</p>
        {line}
        <div className="prejoin-message-actions">
          {showHome && (
            <MeetingPageLink className="btn btn-outline" to={toList ? routes.meetings : meetingHome}>
              {toList ? 'Về danh sách cuộc họp' : 'Về trang cuộc họp'}
            </MeetingPageLink>
          )}
          {children}
        </div>
      </main>
    </RoomPage>
  )
}
