import { useId, useState } from 'react'
import { Icon } from '../../components/Icon'
import { SPEEDS } from './playerSpeed'
import { formatClock } from './format'

/**
 * The player above a record, after AI-eCabinet's: time, a seek bar (the waveform when there is one), back and ahead
 * five seconds, play, speed and volume. It only draws; whoever owns the audio passes its state in.
 */

const speedLabel = (speed: number) => `${String(speed).replace('.', ',')}×`

export function PlayerBar({
  positionMs,
  totalMs,
  playing,
  onToggle,
  onSeek,
  playLabel,
  waveform,
  speed,
  onSpeed,
  volume,
  onVolume,
  muted,
  onMute,
  disabled,
}: {
  positionMs: number
  totalMs: number
  playing: boolean
  onToggle: () => void
  onSeek: (ms: number) => void
  /** What play does: "Nghe cả cuộc họp", "Nghe cả tệp". */
  playLabel: string
  /** 0–1 per bar; without it the seek bar is a plain slider. */
  waveform?: number[]
  speed: number
  onSpeed: (speed: number) => void
  volume: number
  onVolume: (volume: number) => void
  muted: boolean
  onMute: (muted: boolean) => void
  disabled?: boolean
}) {
  const [hover, setHover] = useState<{ x: number; ms: number } | null>(null)
  const [speedOpen, setSpeedOpen] = useState(false)
  const speedId = useId()
  const ratio = totalMs > 0 ? Math.min(1, positionMs / totalMs) : 0
  const total = formatClock(totalMs)

  return (
    <fieldset className="player-bar" disabled={disabled}>
      <legend className="visually-hidden">Nghe lại</legend>
      {/* As wide as the longest the time can get, so the bar beside it does not jump while it plays. */}
      <span className="player-time" style={{ ['--time-width' as string]: `${total.length * 2 + 3}ch` }}>
        <strong>{formatClock(positionMs)}</strong> / {total}
      </span>
      <div
        className={waveform ? 'player-seek player-seek-wave' : 'player-seek'}
        style={{ ['--p' as string]: ratio }}
        onPointerMove={(e) => {
          const box = e.currentTarget.getBoundingClientRect()
          const x = Math.min(box.width, Math.max(0, e.clientX - box.left))
          setHover({ x, ms: (x / box.width) * totalMs })
        }}
        onPointerLeave={() => setHover(null)}
      >
        {waveform && (
          <svg viewBox={`0 0 ${waveform.length} 100`} preserveAspectRatio="none" aria-hidden="true">
            {waveform.map((v, i) => {
              const height = Math.max(4, v * 92)
              return (
                <rect
                  key={i}
                  x={i + 0.15}
                  width={0.7}
                  y={(100 - height) / 2}
                  height={height}
                  className={i / waveform.length < ratio ? 'player-bar-heard' : undefined}
                />
              )
            })}
          </svg>
        )}
        {waveform && <span className="player-head" aria-hidden="true" />}
        <input
          type="range"
          min={0}
          max={Math.max(1, Math.round(totalMs))}
          step={100}
          value={Math.round(positionMs)}
          onChange={(e) => onSeek(Number(e.target.value))}
          onKeyDown={(e) => {
            // Five seconds a key, as the buttons, rather than the slider's own 100 ms step.
            const step = { ArrowLeft: -5000, ArrowDown: -5000, ArrowRight: 5000, ArrowUp: 5000 }[e.key]
            if (step === undefined) return
            e.preventDefault()
            onSeek(positionMs + step)
          }}
          aria-label="Vị trí phát"
          aria-valuetext={formatClock(positionMs)}
        />
        {hover && (
          <span className="player-hover" style={{ left: hover.x }} aria-hidden="true">
            {formatClock(hover.ms)}
          </span>
        )}
      </div>
      <div className="player-controls">
        <button
          type="button"
          className="player-round"
          onClick={() => onSeek(positionMs - 5000)}
          aria-label="Lùi 5 giây"
          data-tooltip="Lùi 5 giây"
        >
          <Icon name="back" size={18} />
          <span className="player-five">5</span>
        </button>
        <button
          type="button"
          className="player-round player-play"
          onClick={onToggle}
          aria-label={playing ? 'Tạm dừng' : playLabel}
          data-tooltip={playing ? 'Tạm dừng' : playLabel}
        >
          <Icon name={playing ? 'pause' : 'play'} size={16} />
        </button>
        <button
          type="button"
          className="player-round"
          onClick={() => onSeek(positionMs + 5000)}
          aria-label="Tới 5 giây"
          data-tooltip="Tới 5 giây"
        >
          <Icon name="ahead" size={18} />
          <span className="player-five">5</span>
        </button>
        <span className="player-speed-wrap">
          <button
            type="button"
            className={speed === 1 ? 'player-speed' : 'player-speed player-speed-on'}
            onClick={() => setSpeedOpen((open) => !open)}
            aria-expanded={speedOpen}
            aria-controls={speedId}
            aria-label={`Tốc độ nghe: ${speedLabel(speed)}`}
            data-tooltip={speedOpen ? undefined : `Tốc độ nghe: ${speedLabel(speed)}`}
          >
            {speedLabel(speed)}
          </button>
          {speedOpen && (
            <span className="player-speeds" id={speedId} role="radiogroup" aria-label="Tốc độ nghe">
              {SPEEDS.map((value) => (
                <button
                  key={value}
                  type="button"
                  role="radio"
                  aria-checked={value === speed}
                  className={value === speed ? 'player-speed-option is-on' : 'player-speed-option'}
                  onClick={() => {
                    onSpeed(value)
                    setSpeedOpen(false)
                  }}
                >
                  {speedLabel(value)}
                </button>
              ))}
            </span>
          )}
        </span>
      </div>
      <div className="player-volume">
        <button
          type="button"
          className="player-round"
          onClick={() => onMute(!muted)}
          aria-label={muted ? 'Bật tiếng' : 'Tắt tiếng'}
          data-tooltip={muted ? 'Bật tiếng' : 'Tắt tiếng'}
        >
          <Icon name={muted || volume === 0 ? 'volumeOff' : 'volume'} size={18} />
        </button>
        <input
          type="range"
          min={0}
          max={1}
          step={0.05}
          value={muted ? 0 : volume}
          onChange={(e) => onVolume(Number(e.target.value))}
          aria-label="Âm lượng"
          style={{ ['--p' as string]: muted ? 0 : volume }}
        />
      </div>
    </fieldset>
  )
}
