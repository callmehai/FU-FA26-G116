import { Icon } from '../../components/Icon'
import { SpeechTimeline } from './SpeechTimeline'
import type { Speech } from './useSpeech'

/**
 * SCRUM-109 — the per-mic record in the meeting's Bản ghi tab. Each line is a sentence someone who joined from afar
 * said into their own mic, named after them; they speak in the call room, where the call's mic is their one switch
 * (ADR-0040). Nobody can start it for them.
 */
export function SpeechPanel({
  speech,
  perMic,
  userId,
  startedAt,
}: {
  /** The meeting's speech, kept by the page so that it re-reads as sentences arrive in every tab. */
  speech: Speech
  /** The meeting is joined from afar (online or hybrid), so it is captured a sentence per mic. */
  perMic: boolean
  userId: string | undefined
  startedAt: string | null
}) {
  const { list, clips, error } = speech
  return (
    <div className="speech-panel">
      {error && (
        <div className="alert alert-error" role="alert">
          <p>{error}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={speech.reload}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}
      {!list && !error && <p className="field-hint">Đang tải…</p>}
      {list && (perMic || clips.length > 0) && <SpeechTimeline speech={speech} userId={userId} startedAt={startedAt} />}
    </div>
  )
}
