import { useEffect, useState } from 'react'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { formatDuration } from './format'
import { elapsedSince, progressParts } from './transcribeTime'

/**
 * A transcript being made, under the player (PO 23/11/2026: "hiện được process bar ko ? và hiện thời gian xử lí được
 * ko"): waiting its turn, or a bar with how far the speech worker got, how long it has been at it and about how long
 * is left. The per cent comes with the transcript the view already polls; the clock ticks here, every second.
 */
export function TranscribeProgress({
  status,
  percent,
  startedAt,
  durationMs,
  again,
}: {
  status: 'Pending' | 'Processing'
  percent: number
  startedAt: string | null
  durationMs: number
  /** The text below is the previous run's: a take was appended, or the Secretary asked for it again. */
  again: boolean
}) {
  const now = useNow(status === 'Processing' && startedAt !== null)
  const previous = again && <span className="field-hint">Chữ dưới đây là bản trước.</span>
  if (status === 'Pending')
    return (
      <p className="transcript-status transcript-running" role="status">
        <span className="transcript-spinner" aria-hidden="true" />
        {again ? 'Đang chờ lượt chuyển chữ lại' : 'Đang chờ lượt chuyển chữ'}
        {previous}
        {!again && durationMs > 0 && (
          <InfoTip
            text={`Bản ghi dài ${formatDuration(durationMs)}, thường chuyển xong trong khoảng ${Math.max(1, Math.ceil(durationMs / 60000 / 4))} phút.`}
          />
        )}
      </p>
    )

  const { label, clock, left } = progressParts(percent, elapsedSince(startedAt, now), again)
  // Until the worker has said how far it got (the first look is a few seconds in, an older worker never says), the
  // spinner stays: an empty bar would read as stuck.
  return (
    <div className="transcribe-progress">
      {percent > 0 && (
        <progress className="transcribe-bar" max={100} value={percent} aria-label={`Chuyển chữ ${percent}%`} />
      )}
      <p className="transcript-status transcript-running">
        {percent === 0 && <span className="transcript-spinner" aria-hidden="true" />}
        <span>{label}</span>
        {clock && (
          <span className="transcribe-clock">
            <Icon name="clock" size={14} />
            <span className="visually-hidden">Đã chạy</span>
            {clock}
          </span>
        )}
        {left && <span className="transcribe-left">{left}</span>}
        {previous}
      </p>
    </div>
  )
}

/** The time now, again every second while `ticking`. */
function useNow(ticking: boolean) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!ticking) return
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [ticking])
  return now
}
