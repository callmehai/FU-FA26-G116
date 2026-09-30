import { useEffect, useMemo, useRef, useState, useSyncExternalStore, type KeyboardEvent } from 'react'
import { useBeforeUnload } from 'react-router-dom'
import {
  RoomAudioRenderer,
  RoomContext,
  VideoTrack,
  isTrackReference,
  useAudioPlayback,
  useChat,
  useIsSpeaking,
  useLocalParticipant,
  useMediaDeviceSelect,
  useParticipants,
  useSpeakingParticipants,
  useTracks,
  type ReceivedChatMessage,
  type TrackReferenceOrPlaceholder,
} from '@livekit/components-react'
import { type Participant, ParticipantEvent, type Room, RoomEvent, Track, type TrackPublication } from 'livekit-client'
import { muteCallParticipant } from '../../../api/calls'
import { ConfirmDialog } from '../../../components/ConfirmDialog'
import { Dialog } from '../../../components/Dialog'
import { Select } from '../../../components/Select'
import { Icon, type IconName } from '../../../components/Icon'
import { useToast } from '../../../components/toastContext'
import { avatarColors, initials } from '../../../lib/avatar'
import { errorMessage, isApiError } from '../../../lib/errors'
import { routes } from '../../../lib/routes'
import { MEETING_ROLE_LABEL, type MeetingRole } from '../../../types/call'
import { SENSITIVITY_LABEL, type Meeting } from '../../../types/meeting'
import { useAccessLost } from '../../auth/useAccessLost'
import { deviceErrorMessage, meetingRoleOf } from './roomSupport'

const PANEL_OVERLAY_QUERY = '(max-width: 1023px)'

type PanelId = 'people' | 'chat'

const CHAT_MAX = 1000

const ROLE_ORDER: Record<MeetingRole, number> = { Chair: 0, Secretary: 1, Member: 2, Guest: 3 }

/**
 * Inside the room (SCRUM-41). Everything reads the Room through RoomContext, so the LiveKit hooks
 * re-render on the events they care about. Nothing in here navigates the tab: the side panels are
 * where meeting features live while the call runs (ADR-0011).
 */
export function RoomView({
  room,
  meeting,
  myRole,
  onLeave,
}: {
  room: Room
  meeting: Meeting
  myRole: MeetingRole
  onLeave: () => Promise<void>
}) {
  return (
    <RoomContext.Provider value={room}>
      <RoomLayout room={room} meeting={meeting} myRole={myRole} onLeave={onLeave} />
      <RoomAudioRenderer />
    </RoomContext.Provider>
  )
}

function RoomLayout({
  room,
  meeting,
  myRole,
  onLeave,
}: {
  room: Room
  meeting: Meeting
  myRole: MeetingRole
  onLeave: () => Promise<void>
}) {
  // Below 1024px a panel covers the stage (see .room-panel in global.css), so it starts closed there.
  const [panel, setPanel] = useState<PanelId | null>(() =>
    window.matchMedia(PANEL_OVERLAY_QUERY).matches ? null : 'people',
  )
  const [confirmingLeave, setConfirmingLeave] = useState(false)
  const [choosingDevices, setChoosingDevices] = useState(false)
  const [reconnecting, setReconnecting] = useState(false)
  const participants = useParticipants()
  const chat = useChat()
  const [seenMessages, setSeenMessages] = useState(0)
  const unread = panel === 'chat' ? 0 : chat.chatMessages.length - seenMessages
  const toast = useToast()
  const selfMuting = useRef(false)
  const { isMicrophoneEnabled, isCameraEnabled, isScreenShareEnabled, localParticipant } = useLocalParticipant()
  const screenShares = useTracks([Track.Source.ScreenShare], { onlySubscribed: false })
  // One presenter at a time. A share the server muted to enforce that is still published for a
  // moment, so it is skipped; if two overlap briefly, the one who started last is shown.
  const presenter = screenShares.filter((t) => t.publication && !t.publication.isMuted).at(-1)
  const [cameraBusy, setCameraBusy] = useState(false)

  // Closing the tab while connected asks first; the browser shows its own wording.
  useBeforeUnload((event) => event.preventDefault())

  useEffect(() => {
    const onReconnecting = () => setReconnecting(true)
    const onReconnected = () => setReconnecting(false)
    // A microphone that goes muted without the user clicking was muted by the chair.
    const onMuted = (publication: TrackPublication, participant: Participant) => {
      if (participant.isLocal && publication.source === Track.Source.Microphone && !selfMuting.current) {
        toast.show('Chủ toạ đã tắt micro của bạn. Bạn có thể bật lại khi cần phát biểu.', 'info')
      }
    }
    // Someone else started presenting while I am: step aside (SCRUM-43). The server mutes the
    // older share as well, for a client that does not do this.
    const onPublished = (publication: TrackPublication, participant: Participant) => {
      if (publication.source !== Track.Source.ScreenShare || participant.isLocal) return
      if (!room.localParticipant.isScreenShareEnabled) return
      void room.localParticipant.setScreenShareEnabled(false)
      toast.show(`${participant.name || 'Người khác'} bắt đầu trình bày, phần chia sẻ của bạn đã dừng.`, 'info')
    }
    room.on(RoomEvent.TrackPublished, onPublished)
    room.on(RoomEvent.Reconnecting, onReconnecting)
    room.on(RoomEvent.Reconnected, onReconnected)
    room.on(RoomEvent.TrackMuted, onMuted)
    return () => {
      room.off(RoomEvent.TrackPublished, onPublished)
      room.off(RoomEvent.Reconnecting, onReconnecting)
      room.off(RoomEvent.Reconnected, onReconnected)
      room.off(RoomEvent.TrackMuted, onMuted)
    }
  }, [room, toast])

  function togglePanel(id: PanelId) {
    if (panel === 'chat') setSeenMessages(chat.chatMessages.length)
    setPanel((current) => (current === id ? null : id))
  }

  async function toggleMic() {
    selfMuting.current = true
    try {
      await localParticipant.setMicrophoneEnabled(!isMicrophoneEnabled)
    } catch (err) {
      toast.show(deviceErrorMessage(err, 'micro'), 'error')
    } finally {
      selfMuting.current = false
    }
  }

  /** Camera on or off without leaving the call (SCRUM-42): the track is muted, not the call redone. */
  async function toggleCamera() {
    setCameraBusy(true)
    try {
      await localParticipant.setCameraEnabled(!isCameraEnabled)
    } catch (err) {
      toast.show(deviceErrorMessage(err, 'camera'), 'error')
    } finally {
      setCameraBusy(false)
    }
  }

  /**
   * Straight to the browser's own picker, like Meet or Discord: it already shows what will be shared.
   * Cancelling it is not an error worth telling anyone about.
   */
  async function startScreenShare() {
    try {
      await localParticipant.setScreenShareEnabled(true, {
        audio: true,
        // The room's own tab is offered too (presenting a TrustCabinet screen is common); the
        // presenter sees a card instead of their own share, so it never mirrors itself.
        selfBrowserSurface: 'include',
        surfaceSwitching: 'include',
        contentHint: 'detail',
      })
    } catch (err) {
      if (err instanceof Error && err.name === 'NotAllowedError') return
      toast.show('Không chia sẻ được màn hình. Vui lòng thử lại.', 'error')
    }
  }

  function toggleScreenShare() {
    if (isScreenShareEnabled) void localParticipant.setScreenShareEnabled(false)
    else void startScreenShare()
  }

  const restricted = meeting.sensitivity === 'Secret' || meeting.sensitivity === 'Confidential'

  return (
    <div className={`room${panel ? ' room-with-panel' : ''}`}>
      <header className="room-header">
        <div className="room-title">
          <h1>{meeting.title}</h1>
          <p>
            Đang diễn ra · {participants.length} người trong phòng
            {restricted && (
              <span className="room-flag"> · Cuộc họp {SENSITIVITY_LABEL[meeting.sensitivity].toLowerCase()}</span>
            )}
          </p>
        </div>
        <a className="btn btn-outline btn-sm" href={routes.meeting(meeting.id)} target="_blank" rel="noopener">
          <span className="room-link-long">Mở chi tiết cuộc họp</span>
          <span className="room-link-short">Chi tiết</span>
        </a>
      </header>

      {reconnecting && (
        <div className="room-banner" role="status">
          Mất kết nối, đang kết nối lại…
        </div>
      )}
      <StartAudioBanner />

      <div className="room-body">
        <main className="room-stage" aria-label="Người trong phòng">
          {presenter ? (
            <div className="room-presenting">
              <figure className="room-screen">
                <VideoTrack trackRef={presenter} className="room-screen-video" />
                <figcaption className="room-tile-name">
                  <Icon name="monitor" size={14} />
                  <span>
                    {presenter.participant.isLocal
                      ? 'Bạn đang trình bày'
                      : `${presenter.participant.name || 'Người dự'} đang trình bày`}
                  </span>
                </figcaption>
              </figure>
              <div className="room-filmstrip">
                <ParticipantGrid compact />
              </div>
            </div>
          ) : (
            <ParticipantGrid />
          )}
        </main>

        {panel && (
          <aside className="room-panel" aria-label={panel === 'people' ? 'Người dự' : 'Chat'}>
            {panel === 'people' ? (
              <ParticipantsPanel meetingId={meeting.id} participants={participants} isChair={myRole === 'Chair'} />
            ) : (
              <ChatPanel messages={chat.chatMessages} send={chat.send} isSending={chat.isSending} />
            )}
          </aside>
        )}
      </div>

      <footer className="room-controls">
        <div className="room-controls-group">
          <ControlButton
            icon={isMicrophoneEnabled ? 'mic' : 'micOff'}
            label={isMicrophoneEnabled ? 'Tắt micro' : 'Bật micro'}
            off={!isMicrophoneEnabled}
            onClick={() => void toggleMic()}
          />
          <ControlButton
            icon={isCameraEnabled ? 'video' : 'videoOff'}
            label={isCameraEnabled ? 'Tắt camera' : 'Bật camera'}
            off={!isCameraEnabled}
            disabled={cameraBusy}
            onClick={() => void toggleCamera()}
          />
          <ControlButton
            icon="monitor"
            label={isScreenShareEnabled ? 'Dừng chia sẻ' : 'Chia sẻ màn hình'}
            active={isScreenShareEnabled}
            onClick={toggleScreenShare}
          />
          <ControlButton icon="sliders" label="Thiết bị" onClick={() => setChoosingDevices(true)} />
        </div>
        <div className="room-controls-group">
          <ControlButton
            icon="users"
            label="Người dự"
            active={panel === 'people'}
            onClick={() => togglePanel('people')}
          />
          <ControlButton
            icon="messageSquare"
            label="Chat"
            active={panel === 'chat'}
            badge={unread > 0 ? unread : undefined}
            onClick={() => togglePanel('chat')}
          />
        </div>
        <div className="room-controls-group">
          <button
            type="button"
            className="btn btn-danger room-leave"
            aria-label="Rời phòng"
            onClick={() => setConfirmingLeave(true)}
          >
            <Icon name="phoneOff" size={16} />
            <span className="room-leave-label">Rời phòng</span>
          </button>
        </div>
      </footer>

      {choosingDevices && <DevicesDialog onClose={() => setChoosingDevices(false)} />}

      {confirmingLeave && (
        <ConfirmDialog
          title="Rời phòng họp"
          confirmLabel="Rời phòng"
          pendingLabel="Đang rời phòng…"
          cancelLabel="Ở lại"
          tone="danger"
          onConfirm={onLeave}
          onClose={() => setConfirmingLeave(false)}
        >
          Cuộc họp vẫn tiếp tục với những người khác. Bạn có thể vào lại khi cuộc họp còn đang diễn ra.
        </ConfirmDialog>
      )}
    </div>
  )
}

function ControlButton({
  icon,
  label,
  onClick,
  off = false,
  active = false,
  disabled = false,
  badge,
}: {
  icon: IconName
  label: string
  onClick: () => void
  off?: boolean
  active?: boolean
  disabled?: boolean
  badge?: number
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      className={`room-control${off ? ' room-control-off' : ''}${active ? ' room-control-active' : ''}`}
      aria-pressed={active || undefined}
      title={label}
      onClick={onClick}
    >
      <Icon name={icon} />
      <span>{label}</span>
      {badge !== undefined && <span className="room-badge">{badge > 99 ? '99+' : badge}</span>}
    </button>
  )
}

/** Browsers block audio until the user interacts with the page; this is that interaction. */
function StartAudioBanner() {
  const { canPlayAudio, startAudio } = useAudioPlayback()
  if (canPlayAudio) return null
  return (
    <div className="room-banner room-banner-action" role="status">
      <span>Trình duyệt đang chặn phát âm thanh.</span>
      <button type="button" className="btn btn-primary btn-sm" onClick={() => void startAudio()}>
        <Icon name="volume" size={16} />
        Bấm để nghe mọi người
      </button>
    </div>
  )
}

const WIDE_PAGE = 9
const NARROW_PAGE = 4

function ParticipantGrid({ compact = false }: { compact?: boolean }) {
  // One tile per participant: their camera when on, a placeholder otherwise.
  const tracks = useTracks([{ source: Track.Source.Camera, withPlaceholder: true }], { onlySubscribed: false })
  const speakers = useSpeakingParticipants()
  const wideOrNarrow = usePageSize()
  // Beside a shared screen the cameras shrink to a strip of four.
  const pageSize = compact ? NARROW_PAGE : wideOrNarrow
  const [order, setOrder] = useState<string[]>([])
  const [page, setPage] = useState(0)

  const localIdentity = tracks.find((t) => t.participant.isLocal)?.participant.identity
  const nextOrder = tileOrder(
    order,
    tracks.map((t) => t.participant.identity),
    speakers.map((p) => p.identity),
    localIdentity,
    pageSize,
  )
  // Adjusting state while rendering, guarded so it settles: the order is kept between renders.
  if (nextOrder.join() !== order.join()) setOrder(nextOrder)

  const pages = Math.max(1, Math.ceil(nextOrder.length / pageSize))
  const current = Math.min(page, pages - 1)
  const byIdentity = new Map(tracks.map((t) => [t.participant.identity, t]))
  // Only the visible page is rendered, so adaptive stream stops downloading everyone else's video.
  const visible = nextOrder
    .slice(current * pageSize, (current + 1) * pageSize)
    .map((id) => byIdentity.get(id))
    .filter((t): t is TrackReferenceOrPlaceholder => t !== undefined)

  return (
    <div className="room-grid-wrap">
      <div className="room-grid" data-count={visible.length}>
        {visible.map((trackRef) => (
          <ParticipantTile key={trackRef.participant.identity} trackRef={trackRef} />
        ))}
      </div>
      {pages > 1 && (
        <nav className="room-pager" aria-label="Trang người dự">
          <button type="button" className="icon-button" aria-label="Trang trước" disabled={current === 0} onClick={() => setPage(current - 1)}>
            <Icon name="chevronLeft" />
          </button>
          <span>
            Trang {current + 1}/{pages}
          </span>
          <button type="button" className="icon-button" aria-label="Trang sau" disabled={current >= pages - 1} onClick={() => setPage(current + 1)}>
            <Icon name="chevronRight" />
          </button>
        </nav>
      )}
    </div>
  )
}

/**
 * A stable tile order so tiles do not jump around. Newcomers go to the end; someone who starts
 * speaking while off the first page moves to its front. Yourself always last, the way people
 * expect to find themselves.
 */
function tileOrder(previous: string[], present: string[], speaking: string[], self: string | undefined, pageSize: number) {
  const kept = previous.filter((id) => present.includes(id))
  let order = [...kept, ...present.filter((id) => !kept.includes(id))].filter((id) => id !== self)
  for (const id of speaking) {
    const at = order.indexOf(id)
    if (at >= pageSize) order = [id, ...order.slice(0, at), ...order.slice(at + 1)]
  }
  return self ? [...order, self] : order
}

/** 9 tiles on a wide window, 4 on a narrow one — matching the grid's breakpoint in global.css. */
function usePageSize() {
  return useSyncExternalStore(
    (onChange) => {
      const query = window.matchMedia('(max-width: 1023px)')
      query.addEventListener('change', onChange)
      return () => query.removeEventListener('change', onChange)
    },
    () => (window.matchMedia('(max-width: 1023px)').matches ? NARROW_PAGE : WIDE_PAGE),
  )
}

function ParticipantTile({ trackRef }: { trackRef: TrackReferenceOrPlaceholder }) {
  const participant = trackRef.participant
  const speaking = useIsSpeaking(participant)
  const micMuted = !useMicrophoneOn(participant)
  const name = participant.name || 'Người dự'
  const role = meetingRoleOf(participant)
  const showVideo = isTrackReference(trackRef) && !trackRef.publication.isMuted
  const colors = avatarColors(name)

  return (
    <figure className={`room-tile${speaking ? ' room-tile-speaking' : ''}`}>
      {showVideo ? (
        <VideoTrack
          trackRef={trackRef}
          className={`room-tile-video${participant.isLocal ? ' room-tile-video-self' : ''}`}
        />
      ) : (
        <span className="room-tile-avatar" style={{ backgroundColor: colors.bg, color: colors.fg }} aria-hidden="true">
          {initials(name)}
        </span>
      )}
      <figcaption className="room-tile-name">
        {micMuted && <Icon name="micOff" size={14} />}
        <span>{participant.isLocal ? `${name} (Bạn)` : name}</span>
        {role === 'Chair' && <span className="room-role">Chủ toạ</span>}
      </figcaption>
    </figure>
  )
}

function ParticipantsPanel({
  meetingId,
  participants,
  isChair,
}: {
  meetingId: string
  participants: Participant[]
  isChair: boolean
}) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [pending, setPending] = useState<string | null>(null)

  const sorted = useMemo(
    () =>
      [...participants].sort((a, b) => {
        const byRole = ROLE_ORDER[meetingRoleOf(a) ?? 'Member'] - ROLE_ORDER[meetingRoleOf(b) ?? 'Member']
        return byRole || (a.name ?? '').localeCompare(b.name ?? '', 'vi', { numeric: true })
      }),
    [participants],
  )

  async function mute(participant: Participant) {
    setPending(participant.identity)
    try {
      await muteCallParticipant(meetingId, participant.identity)
      toast.show(`Đã tắt micro của ${participant.name || 'người này'}.`, 'success')
    } catch (err) {
      // 403 MSG123 is "not the chair" — the caller can still see the meeting, so it is not access lost.
      if (!isApiError(err, 403, 'MSG123') && handleAccessLost(err)) return
      toast.show(errorMessage(err), 'error')
    } finally {
      setPending(null)
    }
  }

  return (
    <div className="room-panel-body">
      <h2 className="room-panel-title">Người dự ({participants.length})</h2>
      <ul className="room-people">
        {sorted.map((participant) => (
          <PersonRow
            key={participant.identity}
            participant={participant}
            canMute={isChair && !participant.isLocal}
            pending={pending === participant.identity}
            onMute={() => void mute(participant)}
          />
        ))}
      </ul>
    </div>
  )
}

function PersonRow({
  participant,
  canMute,
  pending,
  onMute,
}: {
  participant: Participant
  canMute: boolean
  pending: boolean
  onMute: () => void
}) {
  const speaking = useIsSpeaking(participant)
  const micMuted = !useMicrophoneOn(participant)
  const name = participant.name || 'Người dự'
  const role = meetingRoleOf(participant)
  const colors = avatarColors(name)

  return (
    <li className="room-person">
      <span className="avatar avatar-sm" style={{ backgroundColor: colors.bg, color: colors.fg }} aria-hidden="true">
        {initials(name)}
      </span>
      <span className="room-person-name">
        <strong>
          {name}
          {participant.isLocal && ' (Bạn)'}
        </strong>
        <small>{role ? MEETING_ROLE_LABEL[role] : 'Thành viên'}</small>
      </span>
      {speaking && <span className="room-speaking-dot" title="Đang nói" aria-label="Đang nói" />}
      <span className={`room-person-mic${micMuted ? ' is-muted' : ''}`} title={micMuted ? 'Micro tắt' : 'Micro bật'}>
        <Icon name={micMuted ? 'micOff' : 'mic'} size={16} />
      </span>
      {canMute && !micMuted && (
        <button type="button" className="btn btn-outline btn-sm" disabled={pending} onClick={onMute}>
          {pending ? 'Đang tắt…' : 'Tắt mic'}
        </button>
      )}
    </li>
  )
}

function ChatPanel({
  messages,
  send,
  isSending,
}: {
  messages: ReceivedChatMessage[]
  send: (message: string) => Promise<ReceivedChatMessage>
  isSending: boolean
}) {
  const [draft, setDraft] = useState('')
  const [error, setError] = useState<string | null>(null)
  const listRef = useRef<HTMLOListElement>(null)

  // Keep the newest message in view.
  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight })
  }, [messages.length])

  async function submit() {
    const text = draft.trim()
    if (!text || isSending) return
    setError(null)
    try {
      await send(text)
      setDraft('')
    } catch {
      setError('Không gửi được tin nhắn. Vui lòng thử lại.')
    }
  }

  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault()
      void submit()
    }
  }

  return (
    <div className="room-panel-body room-chat">
      <h2 className="room-panel-title">Chat</h2>
      <p className="room-chat-note">Tin nhắn chỉ hiện với người đang trong phòng và không được lưu lại sau cuộc họp.</p>
      <ol className="room-chat-list" ref={listRef}>
        {messages.length === 0 && <li className="room-chat-empty">Chưa có tin nhắn nào.</li>}
        {messages.map((m) => (
          <li key={m.id} className={`room-chat-message${m.from?.isLocal ? ' is-mine' : ''}`}>
            <span className="room-chat-meta">
              <strong>{m.from?.isLocal ? 'Bạn' : m.from?.name || 'Người dự'}</strong>
              <time dateTime={new Date(m.timestamp).toISOString()}>
                {new Date(m.timestamp).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
              </time>
            </span>
            <p>{m.message}</p>
          </li>
        ))}
      </ol>
      {error && (
        <div className="alert alert-error" role="alert">
          <p>{error}</p>
        </div>
      )}
      <form
        className="room-chat-form"
        onSubmit={(event) => {
          event.preventDefault()
          void submit()
        }}
      >
        <textarea
          rows={2}
          maxLength={CHAT_MAX}
          value={draft}
          placeholder="Nhập tin nhắn — Enter để gửi, Shift+Enter xuống dòng"
          aria-label="Tin nhắn"
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={onKeyDown}
        />
        <div className="room-chat-actions">
          <span className="field-hint">
            {draft.length}/{CHAT_MAX}
          </span>
          <button type="submit" className="btn btn-primary btn-sm" disabled={!draft.trim() || isSending}>
            Gửi
          </button>
        </div>
      </form>
    </div>
  )
}

const MIC_EVENTS = [
  ParticipantEvent.TrackPublished,
  ParticipantEvent.TrackUnpublished,
  ParticipantEvent.LocalTrackPublished,
  ParticipantEvent.LocalTrackUnpublished,
  ParticipantEvent.TrackMuted,
  ParticipantEvent.TrackUnmuted,
] as const

/**
 * Whether the participant has a live microphone. Not useIsMuted: that reports "not muted" for
 * someone who never published a microphone at all, which would offer the chair a mute button for
 * a microphone that does not exist.
 */
function useMicrophoneOn(participant: Participant) {
  return useSyncExternalStore(
    (onChange) => {
      for (const event of MIC_EVENTS) participant.on(event, onChange)
      return () => {
        for (const event of MIC_EVENTS) participant.off(event, onChange)
      }
    },
    () => participant.isMicrophoneEnabled,
  )
}

/** Switch microphone, camera and speaker without leaving the room (SCRUM-42). */
function DevicesDialog({ onClose }: { onClose: () => void }) {
  return (
    <Dialog
      title="Thiết bị"
      description="Đổi thiết bị có hiệu lực ngay, không phải vào lại phòng."
      onClose={onClose}
      footer={
        <button type="button" className="btn btn-primary" onClick={onClose}>
          Xong
        </button>
      }
    >
      <div className="room-devices">
        <DeviceField kind="audioinput" label="Micro" />
        <DeviceField kind="videoinput" label="Camera" />
        <DeviceField kind="audiooutput" label="Loa" />
      </div>
    </Dialog>
  )
}

function DeviceField({ kind, label }: { kind: MediaDeviceKind; label: string }) {
  const toast = useToast()
  const { devices, activeDeviceId, setActiveMediaDevice } = useMediaDeviceSelect({
    kind,
    requestPermissions: kind !== 'audiooutput',
  })
  const options = devices
    .filter((d) => d.deviceId)
    .map((d, i) => ({ value: d.deviceId, label: d.label || `${label} ${i + 1}` }))

  return (
    <label className="field">
      <span className="field-label">{label}</span>
      {options.length === 0 ? (
        <span className="field-hint">
          {kind === 'audiooutput'
            ? 'Trình duyệt này không cho chọn loa.'
            : `Chưa có quyền dùng ${label.toLowerCase()}, hoặc máy không có ${label.toLowerCase()}.`}
        </span>
      ) : (
        <Select
          label={label}
          value={
            options.find((o) => o.value === activeDeviceId)?.value ??
            options.find((o) => o.value === 'default')?.value ??
            options[0].value
          }
          options={options}
          onChange={(id) =>
            void setActiveMediaDevice(id).catch((err: unknown) =>
              toast.show(kind === 'audiooutput' ? errorMessage(err) : deviceErrorMessage(err, kind === 'audioinput' ? 'micro' : 'camera'), 'error'),
            )
          }
        />
      )}
    </label>
  )
}
