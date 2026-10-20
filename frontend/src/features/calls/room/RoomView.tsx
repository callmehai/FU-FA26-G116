import { useEffect, useId, useMemo, useRef, useState, useSyncExternalStore, type KeyboardEvent, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
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
import { Icon, type IconName } from '../../../components/Icon'
import { useToast } from '../../../components/toastContext'
import { avatarColors, initials } from '../../../lib/avatar'
import { errorMessage, isApiError } from '../../../lib/errors'
import { routes } from '../../../lib/routes'
import { MEETING_ROLE_LABEL, type MeetingRole } from '../../../types/call'
import { type Meeting } from '../../../types/meeting'
import { SensitivityBadge, StatusBadge } from '../../meetings/StatusBadge'
import { useAccessLost } from '../../auth/useAccessLost'
import { usePipWindow } from './pipWindow'
import { deviceErrorMessage, meetingRoleOf } from './roomSupport'

const PANEL_OVERLAY_QUERY = '(max-width: 1023px)'

/** ⌘ on a Mac, Ctrl elsewhere — the modifier of the mic and camera shortcuts. */
const IS_MAC = /Mac|iPhone|iPad/.test(navigator.platform)
const MOD = IS_MAC ? '⌘' : 'Ctrl'

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
  const floating = usePipWindow()
  const tray = useDeviceTray()

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

  // Meet's shortcuts: ⌘/Ctrl + D for the microphone, ⌘/Ctrl + E for the camera. They override the
  // browser's own (bookmark, search bar) only while in the room.
  const shortcuts = useRef({ mic: () => {}, camera: () => {} })
  useEffect(() => {
    shortcuts.current = { mic: () => void toggleMic(), camera: () => void toggleCamera() }
  })
  useEffect(() => {
    const onKey = (event: globalThis.KeyboardEvent) => {
      if (!(IS_MAC ? event.metaKey : event.ctrlKey) || event.altKey || event.shiftKey) return
      const key = event.key.toLowerCase()
      if (key !== 'd' && key !== 'e') return
      event.preventDefault()
      if (key === 'd') shortcuts.current.mic()
      else shortcuts.current.camera()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

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
        // The room's own tab is offered too: presenting a TrustCabinet screen is common.
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
          <div className="room-title-facts">
            <StatusBadge status="InProgress" />
            <span className="room-count">
              <Icon name="users" size={13} />
              {participants.length} người trong phòng
            </span>
            {restricted && <SensitivityBadge sensitivity={meeting.sensitivity} />}
          </div>
        </div>
        <a className="room-header-link" href={routes.meeting(meeting.id)} target="_blank" rel="noopener">
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
          {floating.pip ? (
            // As in Meet: while the call lives in the floating window, the stage says so instead of
            // playing every video twice, and offers to bring it back.
            <section className="room-elsewhere" aria-label="Cuộc họp đang ở cửa sổ nổi">
              <Icon name="pictureInPicture" size={40} />
              <h2>Cuộc họp đang ở cửa sổ nổi</h2>
              <p>Bạn vẫn đang trong phòng và có thể làm việc khác ở tab hoặc ứng dụng khác.</p>
              <button type="button" className="btn btn-primary" onClick={floating.close}>
                Đưa cuộc họp về cửa sổ này
              </button>
            </section>
          ) : presenter ? (
            <div className="room-presenting">
              {/* Like Meet, the presenter sees their own share too, to know what everyone sees. */}
              <figure className="room-screen">
                <VideoTrack trackRef={presenter} className="room-screen-video" />
                <figcaption className="room-tile-name">
                  <Icon name="monitor" size={14} />
                  <span>
                    {presenter.participant.isLocal
                      ? 'Bạn đang trình bày cho mọi người'
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

      {/* Laid out like Google Meet: the call controls centred, the panels on the right, icons only
          with a tooltip — the label stays the button's accessible name. */}
      <footer className="room-controls">
        <div className="room-controls-spacer" aria-hidden="true" />
        <div className={`room-controls-main${tray.open ? ' is-open' : ''}`} ref={tray.ref}>
          {tray.open && (
            <div
              className="room-tray"
              id={tray.id}
              role="group"
              aria-label={tray.open === 'audio' ? 'Cài đặt âm thanh' : 'Cài đặt video'}
            >
              {tray.open === 'audio' ? (
                <>
                  <DeviceSelect kind="audioinput" icon="mic" title="Micro" />
                  <DeviceSelect kind="audiooutput" icon="volume" title="Loa" />
                </>
              ) : (
                <DeviceSelect kind="videoinput" icon="video" title="Camera" />
              )}
            </div>
          )}
          <div className="room-controls-row">
            <DeviceSplit
              label="Cài đặt âm thanh"
              open={tray.open === 'audio'}
              trayId={tray.id}
              onToggle={() => tray.toggle('audio')}
            >
              <ControlButton
                icon={isMicrophoneEnabled ? 'mic' : 'micOff'}
                label={isMicrophoneEnabled ? 'Tắt micro' : 'Bật micro'}
                shortcut={`${MOD} + D`}
                off={!isMicrophoneEnabled}
                onClick={() => void toggleMic()}
              />
            </DeviceSplit>
            <DeviceSplit
              label="Cài đặt video"
              open={tray.open === 'video'}
              trayId={tray.id}
              onToggle={() => tray.toggle('video')}
            >
              <ControlButton
                icon={isCameraEnabled ? 'video' : 'videoOff'}
                label={isCameraEnabled ? 'Tắt camera' : 'Bật camera'}
                shortcut={`${MOD} + E`}
                off={!isCameraEnabled}
                disabled={cameraBusy}
                onClick={() => void toggleCamera()}
              />
            </DeviceSplit>
            <ControlButton
              icon="monitor"
              label={isScreenShareEnabled ? 'Dừng chia sẻ' : 'Chia sẻ màn hình'}
              active={isScreenShareEnabled}
              onClick={toggleScreenShare}
            />
            <ControlButton icon="phoneOff" label="Rời phòng" leave onClick={() => setConfirmingLeave(true)} />
          </div>
        </div>
        <div className="room-controls-side">
          {floating.supported && (
            <ControlButton
              icon="pictureInPicture"
              label={floating.pip ? 'Đóng cửa sổ nổi' : 'Mở cửa sổ nổi'}
              className="room-control-pip"
              side
              active={floating.pip !== null}
              onClick={() => (floating.pip ? floating.close() : void floating.open())}
            />
          )}
          <ControlButton
            icon="users"
            label="Người dự"
            side
            active={panel === 'people'}
            onClick={() => togglePanel('people')}
          />
          <ControlButton
            icon="messageSquare"
            label="Chat"
            side
            active={panel === 'chat'}
            badge={unread > 0 ? unread : undefined}
            onClick={() => togglePanel('chat')}
          />
        </div>
      </footer>

      {floating.pip &&
        createPortal(
          <PipView
            presenter={presenter}
            mic={{ on: isMicrophoneEnabled, toggle: () => void toggleMic() }}
            camera={{ on: isCameraEnabled, busy: cameraBusy, toggle: () => void toggleCamera() }}
            share={{ on: isScreenShareEnabled, toggle: toggleScreenShare }}
            onLeave={() => void onLeave()}
          />,
          floating.pip.document.body,
        )}


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

/**
 * The floating window's content, like Meet's: whoever is presenting (or the person talking, or the
 * first camera), your own camera in a corner, and the four controls that matter while you are in
 * another tab. Leaving from here leaves at once — there is no main window in view to confirm in.
 */
function PipView({
  presenter,
  mic,
  camera,
  share,
  onLeave,
}: {
  presenter: TrackReferenceOrPlaceholder | undefined
  mic: { on: boolean; toggle: () => void }
  camera: { on: boolean; busy: boolean; toggle: () => void }
  share: { on: boolean; toggle: () => void }
  onLeave: () => void
}) {
  const cameras = useTracks([{ source: Track.Source.Camera, withPlaceholder: true }], { onlySubscribed: false })
  const speakers = useSpeakingParticipants()
  const self = cameras.find((t) => t.participant.isLocal)
  const others = cameras.filter((t) => !t.participant.isLocal)
  const speaking = others.find((t) => speakers.some((p) => p.identity === t.participant.identity))
  const main = speaking ?? others[0]

  return (
    <div className="room room-pip">
      {presenter && isTrackReference(presenter) && (
        <p className="room-pip-bar">
          <Icon name="monitor" size={14} />
          <span>
            {presenter.participant.isLocal
              ? `${presenter.participant.name || 'Bạn'} (Bạn, đang trình bày)`
              : `${presenter.participant.name || 'Người dự'} đang trình bày`}
          </span>
        </p>
      )}
      <div className="room-pip-stage">
        {presenter && isTrackReference(presenter) ? (
          <figure className="room-screen">
            <VideoTrack trackRef={presenter} className="room-screen-video" />
          </figure>
        ) : main ? (
          <ParticipantTile trackRef={main} />
        ) : (
          <p className="room-pip-alone">Chỉ có bạn trong phòng</p>
        )}
        {self && (presenter || main) && (
          <div className="room-pip-self">
            <ParticipantTile trackRef={self} />
          </div>
        )}
      </div>
      <div className="room-pip-controls">
        <ControlButton
          icon={mic.on ? 'mic' : 'micOff'}
          label={mic.on ? 'Tắt micro' : 'Bật micro'}
          off={!mic.on}
          onClick={mic.toggle}
        />
        <ControlButton
          icon={camera.on ? 'video' : 'videoOff'}
          label={camera.on ? 'Tắt camera' : 'Bật camera'}
          off={!camera.on}
          disabled={camera.busy}
          onClick={camera.toggle}
        />
        <ControlButton
          icon="monitor"
          label={share.on ? 'Dừng chia sẻ' : 'Chia sẻ màn hình'}
          active={share.on}
          onClick={share.toggle}
        />
        <ControlButton icon="phoneOff" label="Rời phòng" leave onClick={onLeave} />
      </div>
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
  side = false,
  leave = false,
  className = '',
  shortcut,
  badge,
}: {
  icon: IconName
  label: string
  onClick: () => void
  /** A device the user switched off: red, as in every call app. */
  off?: boolean
  /** A share or an open panel. */
  active?: boolean
  disabled?: boolean
  /** A panel toggle on the right: flat until hovered or open. */
  side?: boolean
  leave?: boolean
  className?: string
  /** A keyboard shortcut shown in the tooltip, as Meet does ("Tắt micro (⌘ + D)"). */
  shortcut?: string
  badge?: number
}) {
  const variant = leave ? ' room-control-leave' : off ? ' room-control-off' : active ? ' room-control-active' : ''
  return (
    <button
      type="button"
      disabled={disabled}
      className={`room-control${side ? ' room-control-side' : ''}${variant} ${className}`.trimEnd()}
      aria-label={label}
      aria-keyshortcuts={shortcut ? shortcut.replace(' + ', '+').replace('⌘', 'Meta') : undefined}
      aria-pressed={side || icon === 'monitor' ? active : undefined}
      onClick={onClick}
    >
      <Icon name={icon} size={20} />
      <span className="room-control-tip" aria-hidden="true">
        {shortcut ? `${label} (${shortcut})` : label}
      </span>
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
  const shares = useTracks([Track.Source.ScreenShare], { onlySubscribed: false })
  const speakers = useSpeakingParticipants()
  const wideOrNarrow = usePageSize()
  // Beside a shared screen the cameras shrink to a strip of four.
  const pageSize = compact ? NARROW_PAGE : wideOrNarrow
  const [order, setOrder] = useState<string[]>([])
  const [page, setPage] = useState(0)

  const localIdentity = tracks.find((t) => t.participant.isLocal)?.participant.identity
  const nextOrder = tileOrder(order, {
    present: tracks.map((t) => t.participant.identity),
    presenting: shares.filter((t) => !t.publication?.isMuted).map((t) => t.participant.identity),
    speaking: speakers.map((p) => p.identity),
    cameraOn: tracks.filter((t) => isTrackReference(t) && !t.publication.isMuted).map((t) => t.participant.identity),
    self: localIdentity,
    pageSize,
  })
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
 * Who gets a tile on the first page, the way Google Meet decides it: yourself always, then whoever
 * is presenting, whoever is talking, whoever has their camera on, then everyone else.
 *
 * The order is kept between renders so tiles do not reshuffle on every word. Newcomers go to the
 * end, and someone off the first page is pulled onto it only when they outrank the weakest tile
 * there — the one dropped is that weakest tile, everyone else stays put. You sit in the first
 * page's last slot.
 */
function tileOrder(
  previous: string[],
  now: {
    present: string[]
    presenting: string[]
    speaking: string[]
    cameraOn: string[]
    self: string | undefined
    pageSize: number
  },
) {
  const { present, presenting, speaking, cameraOn, self, pageSize } = now
  const rank = (id: string) =>
    presenting.includes(id) ? 0 : speaking.includes(id) ? 1 : cameraOn.includes(id) ? 2 : 3
  const kept = previous.filter((id) => present.includes(id) && id !== self)
  const order = [...kept, ...present.filter((id) => !kept.includes(id) && id !== self)]
  // One slot of the first page is yours.
  const slots = self ? pageSize - 1 : pageSize

  for (let at = slots; at < order.length; at++) {
    const candidate = order[at]
    const first = order.slice(0, slots)
    let weakest = -1
    for (let k = first.length - 1; k >= 0; k--) {
      if (weakest < 0 || rank(first[k]) > rank(first[weakest])) weakest = k
    }
    if (weakest >= 0 && rank(candidate) < rank(first[weakest])) {
      // Swap them: the promoted tile takes the weakest one's place, which goes where it was.
      order[at] = first[weakest]
      order[weakest] = candidate
    }
  }

  if (!self) return order
  return [...order.slice(0, slots), self, ...order.slice(slots)]
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

type TrayKind = 'audio' | 'video'

/**
 * Switch microphone, camera and speaker without leaving the room (SCRUM-42), as Google Meet does:
 * the arrow beside the microphone or camera grows the control panel upwards into a tray of device
 * pickers. A click outside the panel closes it; Escape closes an open picker first, then the tray.
 */
function useDeviceTray() {
  const [open, setOpen] = useState<TrayKind | null>(null)
  const ref = useRef<HTMLDivElement>(null)
  const id = useId()

  useEffect(() => {
    if (!open) return
    const onPointer = (event: PointerEvent) => {
      if (!ref.current?.contains(event.target as Node)) setOpen(null)
    }
    const onKey = (event: globalThis.KeyboardEvent) => {
      // A picker's own menu takes the first Escape.
      if (event.key === 'Escape' && !ref.current?.querySelector('[role="menu"]')) setOpen(null)
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('pointerdown', onPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  return { open, ref, id, toggle: (kind: TrayKind) => setOpen((v) => (v === kind ? null : kind)) }
}

/** The microphone or camera button with its tray arrow on the left, in one pill. */
function DeviceSplit({
  label,
  open,
  trayId,
  onToggle,
  children,
}: {
  label: string
  open: boolean
  trayId: string
  onToggle: () => void
  children: ReactNode
}) {
  return (
    <div className="room-split">
      <button
        type="button"
        className="room-split-more"
        aria-label={label}
        aria-expanded={open}
        aria-controls={open ? trayId : undefined}
        onClick={onToggle}
      >
        <Icon name={open ? 'chevronDown' : 'chevronUp'} size={16} />
      </button>
      {children}
    </div>
  )
}

type DeviceOption = { id: string; label: string; isDefault: boolean }

/**
 * Chrome lists the system default twice: once as "default" ("Mặc định - Micrô MacBook Air") and
 * once as the device itself. Like Meet, show the device once and mark it as the default. The
 * "communications" entry (Windows) is the same kind of alias.
 */
function deviceOptions(devices: MediaDeviceInfo[], title: string): DeviceOption[] {
  const alias = devices.find((d) => d.deviceId === 'default')
  const real = devices.filter((d) => d.deviceId && d.deviceId !== 'default' && d.deviceId !== 'communications')
  if (real.length === 0) {
    return alias ? [{ id: alias.deviceId, label: alias.label || title, isDefault: true }] : []
  }
  const aliasName = alias?.label.includes(' - ') ? alias.label.slice(alias.label.indexOf(' - ') + 3) : alias?.label
  const defaultDevice =
    alias && (real.find((d) => d.groupId === alias.groupId && d.label === aliasName) ?? real.find((d) => d.label === aliasName))
  const options = real.map((d, i) => ({ id: d.deviceId, label: d.label || `${title} ${i + 1}`, isDefault: d === defaultDevice }))
  // A default we cannot match to a device stays its own entry, so following the system still works.
  return alias && !defaultDevice ? [{ id: alias.deviceId, label: alias.label || title, isDefault: true }, ...options] : options
}

function DeviceSelect({ kind, icon, title }: { kind: MediaDeviceKind; icon: IconName; title: string }) {
  const toast = useToast()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const menuId = useId()
  const { devices, activeDeviceId, setActiveMediaDevice } = useMediaDeviceSelect({
    kind,
    requestPermissions: kind !== 'audiooutput',
  })
  const options = deviceOptions(devices, title)
  // The list arrives a moment after the tray opens; only call it empty once it has had time to.
  const [settled, setSettled] = useState(false)
  useEffect(() => {
    const timer = window.setTimeout(() => setSettled(true), 1000)
    return () => window.clearTimeout(timer)
  }, [])
  const active =
    options.find((o) => o.id === activeDeviceId) ??
    (activeDeviceId === '' || activeDeviceId === 'default' ? options.find((o) => o.isDefault) : undefined) ??
    options[0]

  useEffect(() => {
    if (!open) return
    ref.current?.querySelector<HTMLElement>('[role="menuitemradio"][aria-checked="true"]')?.focus()
    const onPointer = (event: PointerEvent) => {
      if (!ref.current?.contains(event.target as Node)) setOpen(false)
    }
    const onKey = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false)
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('pointerdown', onPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  function pick(id: string) {
    setOpen(false)
    setActiveMediaDevice(id).catch((err: unknown) =>
      toast.show(
        kind === 'audiooutput' ? errorMessage(err) : deviceErrorMessage(err, kind === 'audioinput' ? 'micro' : 'camera'),
        'error',
      ),
    )
  }

  function onMenuKey(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return
    event.preventDefault()
    const items = [...event.currentTarget.querySelectorAll<HTMLElement>('[role="menuitemradio"]')]
    const at = items.indexOf(document.activeElement as HTMLElement)
    items[(at + (event.key === 'ArrowDown' ? 1 : items.length - 1)) % items.length]?.focus()
  }

  if (options.length === 0) {
    const reason = !settled
      ? title
      : kind === 'audiooutput'
        ? 'Trình duyệt này không cho chọn loa'
        : `Chưa có quyền dùng ${title.toLowerCase()}, hoặc máy không có ${title.toLowerCase()}`
    return (
      <div className="room-select">
        <button type="button" className="room-select-trigger" disabled aria-label={`${title}: ${reason}`}>
          <Icon name={icon} size={18} />
          <span className="room-select-value">{reason}</span>
        </button>
      </div>
    )
  }

  return (
    <div className="room-select" ref={ref}>
      <button
        type="button"
        className="room-select-trigger"
        aria-label={`${title}: ${active?.label ?? ''}`}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen((v) => !v)}
      >
        <Icon name={icon} size={18} />
        <span className="room-select-value">{active?.label}</span>
        <Icon name={open ? 'chevronUp' : 'chevronDown'} size={16} />
      </button>
      {open && (
        <div className="room-select-menu" role="menu" id={menuId} aria-label={title} onKeyDown={onMenuKey}>
          {options.map((option) => {
            const checked = option.id === active?.id
            return (
              <button
                key={option.id}
                type="button"
                role="menuitemradio"
                aria-checked={checked}
                tabIndex={checked ? 0 : -1}
                className={`room-select-item${checked ? ' is-active' : ''}`}
                onClick={() => pick(option.id)}
              >
                <span className="room-select-check">{checked && <Icon name="check" size={18} />}</span>
                <span className="room-select-text">
                  <span className="room-select-name">{option.label}</span>
                  {option.isDefault && <span className="room-select-note">Theo mặc định của hệ thống</span>}
                </span>
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
