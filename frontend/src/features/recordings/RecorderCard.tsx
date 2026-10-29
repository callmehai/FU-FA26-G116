import { useEffect, useLayoutEffect, useRef, useState, type PointerEvent } from 'react'
import { createPortal } from 'react-dom'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { Recording, RecordingList } from '../../types/recording'
import { ConsentDialog } from './ConsentDialog'
import { formatDuration } from './format'
import { saveTake } from './roomMic'
import type { RoomRecorder } from './useRoomRecorder'
import { useFloatingBox, type Edge } from './useFloatingBox'

/**
 * SCRUM-29, ADR-0041 — the Secretary's room mic, as AI-eCabinet's floating mic card: in the corner of every tab while
 * the meeting runs, so moving between tabs never stops it. It is dragged anywhere and widened from its sides, and
 * stays where it was left; nothing else in the corner pushes it. One switch: on records the room, off saves the take into
 * the meeting's one recording and has it transcribed again. The participants are told before the first take only. The
 * card also carries on or saves a take this laptop kept from a session cut off, and saves one left open on another.
 */

const MIN_KEY = 'trustcabinet.recorder.min'
const BOX_KEY = 'trustcabinet.recorder.box'
const MIN_WIDTH = 320
const MAX_WIDTH = 480
const EDGES: Edge[] = ['w', 'e']
/** Past this much audio waiting on the device, the network is said to be slow; nothing is lost meanwhile. */
const BEHIND_NOTICE_SECONDS = 30

/** Open on a laptop, so the Secretary sees it is there; a bubble on a phone, where the card would cover the page. */
function storedMin() {
  const phone = window.matchMedia('(max-width: 560px)').matches
  try {
    const stored = localStorage.getItem(MIN_KEY)
    return stored === null ? phone : stored === '1'
  } catch {
    return phone
  }
}

function useNow(on: boolean) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!on) return
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [on])
  return now
}

export function RecorderCard({
  title,
  room,
  list,
  open,
  onChanged,
}: {
  title: string
  room: RoomRecorder
  /** The meeting's recordings: whether its first take is still to come. */
  list: RecordingList | null
  /** The take open right now, on this laptop or another. */
  open: Recording | null
  onChanged: () => Promise<void> | void
}) {
  const toast = useToast()
  const [min, setMin] = useState(storedMin)
  const [asking, setAsking] = useState(false)
  const [closing, setClosing] = useState(false)
  const box = useFloatingBox(BOX_KEY, { right: 16, bottom: 16, width: null }, MIN_WIDTH, MAX_WIDTH)
  const { ref: dock, refit } = box
  const { phase, unfinished } = room
  const on = phase === 'on'
  const now = useNow(on)

  // The page scrolls past the card (it reads the height here), and a card that grew is pulled back inside the window.
  useEffect(() => {
    const el = dock.current
    if (!el) return
    const root = document.documentElement
    const observer = new ResizeObserver(() => {
      root.style.setProperty('--mic-dock-h', `${el.offsetHeight + 8}px`)
      refit()
    })
    observer.observe(el)
    return () => {
      observer.disconnect()
      root.style.removeProperty('--mic-dock-h')
    }
  }, [dock, refit])

  const kept = !on && phase !== 'starting' && unfinished !== null
  // A take open that is not this laptop's: on another device, or this one's own that a closed tab left behind.
  const elsewhere = !on && phase === 'off' && !kept && open !== null ? open : null
  const firstTake = !list?.items.some((r) => r.source === 'BrowserCapture' && r.status === 'Available' && r.takes)
  // A problem must be seen, whatever the Secretary chose.
  const minimised = min && !room.error && !kept && !elsewhere
  useLayoutEffect(() => {
    if (!minimised) refit()
  }, [minimised, refit])

  // The bubble opens on a press that did not move it. The drag captures the pointer, so the release lands on the
  // box rather than the button and is read there; the button's own click is the keyboard's.
  const pressedAt = useRef<{ x: number; y: number } | null>(null)
  const still = (event: { clientX: number; clientY: number }) => {
    const p = pressedAt.current
    return !p || Math.hypot(event.clientX - p.x, event.clientY - p.y) <= 4
  }

  const remember = (value: boolean) => {
    setMin(value)
    try {
      localStorage.setItem(MIN_KEY, value ? '1' : '0')
    } catch {
      // A private window: the choice lasts for this page.
    }
  }

  const status =
    phase === 'starting'
      ? 'Đang mở micro…'
      : on
        ? 'Đang ghi cả phòng'
        : phase === 'saving'
          ? 'Đang lưu lượt ghi…'
          : kept
            ? 'Còn lượt ghi dở trên máy này'
            : elsewhere
              ? `Đang ghi ở nơi khác · ${elsewhere.createdByName}`
              : room.error
                ? 'Chưa ghi được'
                : 'Mic phòng đang tắt'

  const closeElsewhere = async (take: Recording) => {
    setClosing(true)
    try {
      const saved = await saveTake(take.id)
      if (saved) toast.show(saved.message, saved.recordingId ? 'success' : 'info')
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      await onChanged()
      setClosing(false)
    }
  }

  const toggle = () => {
    if (on) room.stop()
    else if (firstTake) setAsking(true)
    else room.start(false)
  }
  const toggleLabel = on ? 'Tắt mic, lưu lượt ghi' : 'Bật mic phòng'

  const card = minimised ? (
    <button
      type="button"
      data-drag
      className={`mic-bubble${on ? ' is-live' : ''}`}
      onPointerDown={(event) => {
        pressedAt.current = { x: event.clientX, y: event.clientY }
      }}
      onClick={(event) => {
        if (event.detail === 0 || still(event)) remember(false)
      }}
      aria-label={`Mở ô ghi âm: ${status} · ${title}`}
      data-tooltip={`${title} · ${status}`}
    >
      <Icon name={on ? 'mic' : 'micOff'} size={24} />
    </button>
  ) : (
    <section
      className="mic-card"
      aria-labelledby="recorder-card-title"
      style={box.rect.width ? { width: Math.max(MIN_WIDTH, box.rect.width) } : undefined}
    >
      {EDGES.map((edge) => (
        <span key={edge} aria-hidden="true" className={`mic-edge mic-edge-${edge}`} {...box.edge(edge)} />
      ))}
      <header className="mic-card-head">
        <h2 id="recorder-card-title">{title}</h2>
        <button
          type="button"
          className="mic-card-min"
          onClick={() => remember(true)}
          aria-label="Thu nhỏ ô ghi âm"
          data-tooltip="Thu nhỏ ô ghi âm"
        >
          <Icon name="minus" size={16} />
        </button>
      </header>
      <div className="mic-card-body">
        <div className="mic-status" role="status">
          <span className={`mic-status-dot${on ? ' is-live' : room.error ? ' is-error' : ''}`} />
          <span className="mic-status-text" title={status}>
            {status}
          </span>
          {on && room.startedAt !== null && (
            <span className="mic-clock" data-tooltip="Thời lượng lượt ghi này">
              {formatDuration(now - room.startedAt)}
            </span>
          )}
        </div>
        {kept ? (
          <div className="mic-choices">
            <button type="button" className="btn btn-primary btn-sm" onClick={room.resumeUnfinished}>
              <Icon name="mic" size={16} />
              Ghi tiếp
            </button>
            <button type="button" className="btn btn-outline btn-sm" onClick={room.finishUnfinished}>
              <Icon name="check" size={16} />
              Lưu phần đã ghi
            </button>
          </div>
        ) : elsewhere ? (
          elsewhere.canClose && (
            <div className="mic-choices">
              <button
                type="button"
                className="btn btn-outline btn-sm"
                disabled={closing}
                onClick={() => void closeElsewhere(elsewhere)}
                data-tooltip="Máy kia không quay lại thì lưu lượt đó để chuyển chữ"
              >
                <Icon name="check" size={16} />
                {closing ? 'Đang lưu…' : 'Lưu lượt đó'}
              </button>
            </div>
          )
        ) : (
          <div className="mic-controls">
            <button
              type="button"
              className={`mic-toggle${on ? ' is-on' : ''}`}
              onClick={toggle}
              disabled={phase === 'starting' || phase === 'saving'}
              aria-label={toggleLabel}
              aria-pressed={on}
              data-tooltip={toggleLabel}
            >
              <Icon name={on ? 'mic' : 'micOff'} size={22} />
            </button>
            <Meter level={room.level} live={on} />
          </div>
        )}
        {room.error ? (
          <p className="mic-notice mic-notice-error" role="alert">
            <Icon name="alertCircle" size={16} />
            {room.error}
          </p>
        ) : room.notice && phase === 'off' ? (
          <p className="mic-notice" role="status">
            <Icon name="alertCircle" size={16} />
            {room.notice}
          </p>
        ) : on && room.behindSeconds > BEHIND_NOTICE_SECONDS ? (
          <p className="mic-notice" role="status">
            <Icon name="alertCircle" size={16} />
            Mạng chậm · {formatDuration(room.behindSeconds * 1000)} chưa gửi, vẫn giữ trên máy
          </p>
        ) : (
          phase === 'off' &&
          !kept &&
          !elsewhere && (
            <p className="mic-hint">
              {firstTake ? 'Báo mọi người trước khi bật mic.' : 'Tắt mic là lưu lượt ghi và chuyển chữ.'}
            </p>
          )
        )}
      </div>
    </section>
  )

  return (
    <>
      {createPortal(
        <div
          className={`mic-dock${box.moving ? ' is-moving' : ''}`}
          ref={dock}
          style={{ right: box.rect.right, bottom: box.rect.bottom }}
          {...box.move}
          onPointerUp={(event: PointerEvent<HTMLDivElement>) => {
            box.move.onPointerUp()
            if (minimised && pressedAt.current && still(event)) remember(false)
          }}
        >
          {card}
        </div>,
        document.body,
      )}
      {asking && (
        <ConsentDialog
          consent={{ kind: 'record' }}
          purpose={room.purpose}
          onPurpose={room.setPurpose}
          onClose={() => setAsking(false)}
          onConfirm={() => {
            setAsking(false)
            room.start(true)
          }}
        />
      )}
    </>
  )
}

/** Bars that light from the left with loudness, like a call app's mic test; none in silence. */
function Meter({ level, live }: { level: number; live: boolean }) {
  const ref = useRef<HTMLSpanElement>(null)
  const [count, setCount] = useState(18)
  useLayoutEffect(() => {
    const el = ref.current
    if (!el) return
    const measure = () => setCount(Math.max(8, Math.floor(el.clientWidth / 8)))
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(el)
    return () => observer.disconnect()
  }, [])
  const lit = Math.round(level * count)
  return (
    <span ref={ref} className={live ? 'mic-meter is-live' : 'mic-meter'} aria-hidden="true">
      {Array.from({ length: count }, (_, i) => (
        <i key={i} className={i < lit ? 'is-lit' : undefined} />
      ))}
    </span>
  )
}
