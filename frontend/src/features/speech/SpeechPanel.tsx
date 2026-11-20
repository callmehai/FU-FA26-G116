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
      {/* First load: the card's outline where the record will be, as the other tabs load (Khánh, 19/11/2026). An
          in-person meeting has no per-mic record unless one is found, so it shows nothing until then. */}
      {speech.loading && perMic && <TimelineSkeleton />}
      {list && (perMic || clips.length > 0) && <SpeechTimeline speech={speech} userId={userId} startedAt={startedAt} />}
    </div>
  )
}

/** The per-mic record's card while it loads: its heading, the player and a few lines, sized like the real ones. */
function TimelineSkeleton() {
  return (
    <section className="tl-card tl-skeleton" aria-busy="true" aria-label="Đang tải bản ghi">
      <div className="tl-head">
        <span className="skeleton tl-skeleton-title" />
      </div>
      <div className="tl-player">
        <span className="tl-skeleton-player" />
      </div>
      <ol className="tl-list">
        {[0, 1, 2].map((i) => (
          <li key={i} className="tl-group">
            <div className="tl-row tl-row-head">
              <span className="tl-time">
                <span className="skeleton tl-skeleton-time" />
              </span>
              <span className="tl-rail" aria-hidden="true">
                <span className="tl-dot" />
              </span>
              <div className="tl-head-row">
                <span className="skeleton tl-skeleton-name" />
              </div>
            </div>
            <div className="tl-row">
              <span className="tl-time" />
              <span className="tl-rail" aria-hidden="true" />
              <div className="tl-content">
                <span className="skeleton tl-skeleton-text" />
              </div>
            </div>
          </li>
        ))}
      </ol>
    </section>
  )
}
