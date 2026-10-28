import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Icon } from '../../components/Icon'
import { ConsentDialog } from './ConsentDialog'
import { formatDuration } from './format'
import type { RoomRecorder } from './useRoomRecorder'

/**
 * SCRUM-29, ADR-0040 — the Secretary's recorder for a meeting held in a room, as AI-eCabinet's floating mic card: in
 * the corner of every tab while the meeting runs, so moving between tabs never stops it. The room's one mic records
 * everyone; nobody else has a switch. The page holds the recorder, so the card can fold into a bubble while it runs.
 */

const MIN_KEY = 'trustcabinet.recorder.min'

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

export function RecorderCard({ title, room }: { title: string; room: RoomRecorder }) {
  const { recorder } = room
  const [min, setMin] = useState(storedMin)
  const [asking, setAsking] = useState(false)
  const dock = useRef<HTMLDivElement>(null)
  const { state } = recorder

  // The corner is shared with the upload tray (under the card) and the toasts (above it): they read its height here.
  useEffect(() => {
    const el = dock.current
    if (!el) return
    const root = document.documentElement
    const observer = new ResizeObserver(() => root.style.setProperty('--mic-dock-h', `${el.offsetHeight + 8}px`))
    observer.observe(el)
    return () => {
      observer.disconnect()
      root.style.removeProperty('--mic-dock-h')
    }
  }, [])
  const recording = state.kind === 'recording'
  const taking = recording || state.kind === 'paused'
  // A microphone that would not open must be seen, whatever the Secretary chose.
  const minimised = min && state.kind !== 'error'

  const remember = (value: boolean) => {
    setMin(value)
    try {
      localStorage.setItem(MIN_KEY, value ? '1' : '0')
    } catch {
      // A private window: the choice lasts for this page.
    }
  }

  const status =
    state.kind === 'error'
      ? 'Chưa ghi được'
      : state.kind === 'starting'
        ? 'Đang mở micro…'
        : recording
          ? 'Đang ghi cả phòng'
          : state.kind === 'paused'
            ? 'Đã tạm dừng'
            : 'Chưa ghi âm'

  const card = minimised ? (
    <button
      type="button"
      className={`mic-bubble${recording ? ' is-live' : ''}`}
      onClick={() => remember(false)}
      aria-label={`Mở ô ghi âm: ${status} · ${title}`}
      data-tooltip={`${title} · ${status}`}
    >
      <Icon name={taking ? 'mic' : 'micOff'} size={24} />
    </button>
  ) : (
    <section className="mic-card" aria-labelledby="recorder-card-title">
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
          <span className={`mic-status-dot${recording ? ' is-live' : state.kind === 'error' ? ' is-error' : ''}`} />
          <span className="mic-status-text" title={status}>
            {status}
          </span>
          {taking && (
            <span className="mic-clock" data-tooltip="Thời lượng bản ghi">
              {formatDuration(state.elapsedMs)}
            </span>
          )}
        </div>
        <div className="mic-controls">
          {taking ? (
            <button
              type="button"
              className={`mic-toggle${recording ? ' is-on' : ''}`}
              onClick={recording ? recorder.pause : recorder.resume}
              aria-label={recording ? 'Tạm dừng' : 'Ghi tiếp'}
              data-tooltip={recording ? 'Tạm dừng' : 'Ghi tiếp'}
            >
              <Icon name={recording ? 'pause' : 'mic'} size={22} />
            </button>
          ) : (
            <button
              type="button"
              className="mic-toggle"
              onClick={() => setAsking(true)}
              disabled={state.kind === 'starting'}
              aria-label="Bắt đầu ghi âm"
              data-tooltip="Bắt đầu ghi âm"
            >
              <Icon name="mic" size={22} />
            </button>
          )}
          <Meter level={recorder.level} live={recording} />
          {taking && (
            <button
              type="button"
              className="mic-side"
              onClick={() => void room.stop()}
              aria-label="Dừng và lưu"
              data-tooltip="Dừng và lưu"
            >
              <Icon name="recordStop" size={20} />
            </button>
          )}
        </div>
        {state.kind === 'error' ? (
          <p className="mic-notice mic-notice-error" role="alert">
            <Icon name="alertCircle" size={16} />
            {state.message}
          </p>
        ) : (
          !taking && <p className="mic-hint">Báo mọi người trước khi bấm ghi.</p>
        )}
      </div>
    </section>
  )

  return (
    <>
      {createPortal(
        <div className="mic-dock" ref={dock}>
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
            void recorder.start()
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
