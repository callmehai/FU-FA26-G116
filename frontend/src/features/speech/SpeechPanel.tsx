import { useState } from 'react'
import { setCaptureMode } from '../../api/speech'
import { Icon } from '../../components/Icon'
import { SegmentedControl } from '../../components/SegmentedControl'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { CAPTURE_MODE_LABEL, type CaptureMode } from '../../types/speech'
import { RecordingsPanel } from '../recordings/RecordingsPanel'
import type { Recorder, RecordingInput } from '../recordings/useRecorder'
import { SpeechTimeline } from './SpeechTimeline'
import type { Speech } from './useSpeech'

/**
 * SCRUM-109 — the per-mic transcript, written once for the meeting page and the call room (ADR-0011). Each line is a
 * sentence someone said into their own mic, named after them. The person's own switch sits above it in the call
 * room; on the meeting page it is the floating mic card (MicCard), so that it stays on in every tab. Nobody can start
 * it for them.
 */

const CAPTURE_OPTIONS = (['PerMic', 'Room'] as const).map((value) => ({ value, label: CAPTURE_MODE_LABEL[value] }))

export function SpeechPanel({
  meetingId,
  variant,
  speech,
  recorder,
  recordSource,
}: {
  meetingId: string
  variant: 'page' | 'room'
  /** The meeting's speech, kept by the page or the call so that the mic outlives this panel. */
  speech: Speech
  /** The call room's recorder and the whole call as its sound (SCRUM-44), for a meeting captured as one room. */
  recorder?: Recorder
  recordSource?: () => RecordingInput
}) {
  // A meeting captured as one room has no per-mic lines: in the call, the panel is where the Secretary records it.
  if (variant === 'room' && speech.list?.captureMode === 'Room')
    return <RecordingsPanel meetingId={meetingId} variant="room" recorder={recorder} source={recordSource} />
  return <SpeechView speech={speech} variant={variant} />
}

function SpeechView({ speech, variant }: { speech: Speech; variant: 'page' | 'room' }) {
  const { list, clips, error } = speech
  const room = variant === 'room'
  const perMic = list?.captureMode === 'PerMic'

  return (
    <div className={room ? 'room-panel-body speech-panel' : 'speech-panel'}>
      {room && <h2 className="room-panel-title">Lời nói</h2>}
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
      {list && !room && list.canChangeMode && (
        <ModePicker meetingId={speech.meetingId} mode={list.captureMode} onChanged={speech.reload} />
      )}
      {list?.canCapture && room && <MyMic speech={speech} room={room} />}
      {list && (perMic || clips.length > 0) && <SpeechTimeline speech={speech} room={room} />}
    </div>
  )
}

function ModePicker({ meetingId, mode, onChanged }: { meetingId: string; mode: CaptureMode; onChanged: () => void }) {
  const toast = useToast()
  const [saving, setSaving] = useState(false)
  // The pick shows at once and goes back if the server refuses it; a newer value from the server replaces it.
  const [picked, setPicked] = useState<CaptureMode | null>(null)
  const [seen, setSeen] = useState(mode)
  if (mode !== seen) {
    setSeen(mode)
    setPicked(null)
  }
  const change = async (next: CaptureMode) => {
    setPicked(next)
    setSaving(true)
    try {
      toast.show((await setCaptureMode(meetingId, next)).message, 'success')
      onChanged()
    } catch (failure) {
      setPicked(null)
      toast.show(errorMessage(failure), 'error')
    } finally {
      setSaving(false)
    }
  }
  const shown = picked ?? mode
  return (
    <div className="field speech-mode">
      <span>Cách ghi âm</span>
      <SegmentedControl
        value={shown}
        options={CAPTURE_OPTIONS}
        onChange={(v) => void change(v)}
        label="Cách ghi âm"
        disabled={saving}
      />
      <small className="field-hint">
        {shown === 'PerMic'
          ? 'Mỗi người tự thu micro của mình, chữ hiện theo từng câu.'
          : 'Thư ký ghi hoặc tải lên một bản ghi chung.'}{' '}
        Khoá khi bắt đầu họp.
      </small>
    </div>
  )
}

/** The person's own switch and what their mic is doing — the "being recorded" sign the ticket asks for. */
function MyMic({ speech, room }: { speech: Speech; room: boolean }) {
  const { capture, mine, capturing } = speech
  const elsewhere = capture.state === 'elsewhere'
  const status =
    capture.state === 'error'
      ? capture.error
      : elsewhere
        ? 'Đang ghi ở tab khác'
        : !mine
          ? 'Không ghi'
          : !capturing
            ? 'Micro tắt'
            : capture.state === 'starting'
              ? 'Đang mở micro…'
              : 'Đang ghi'
  const live = capturing && (capture.state === 'listening' || capture.state === 'speaking')
  // Sending as usual says nothing: the lines appearing say it. Only a sentence that is stuck gets a line here.
  // A no-break space keeps the count with "câu" when the line wraps.
  const stuck = capture.engineDown
    ? `Máy chuyển chữ chưa chạy: ${capture.waiting}\u00a0câu đang chờ, tự gửi khi máy chạy lại.`
    : capture.retrying > 0
      ? `Chưa gửi được ${capture.waiting}\u00a0câu, đang thử lại.`
      : null
  return (
    <div className={`speech-mine${live ? ' is-live' : ''}`} role="status">
      {/* A mic, struck through when it is not feeding the transcript: the shape says it, not a coloured dot. */}
      <span className="speech-mine-icon">
        <Icon name={live ? 'mic' : 'micOff'} size={18} />
      </span>
      {/* Short: the room's narrow panel has the meter and the button beside it, and the header already says whose. */}
      <span className="speech-mine-text" title={status ?? undefined}>
        {status}
      </span>
      {live && (
        <span className="speech-meter" aria-hidden="true">
          <span style={{ transform: `scaleX(${capture.level})` }} />
        </span>
      )}
      {elsewhere && capturing ? (
        // Two tabs of one person would write every sentence twice: one records, this one can take over.
        <button type="button" className="btn btn-primary btn-sm" onClick={capture.takeOver}>
          <Icon name="mic" size={16} />
          Ghi ở tab này
        </button>
      ) : (
        <button
          type="button"
          className={mine ? 'btn btn-outline btn-sm' : 'btn btn-primary btn-sm'}
          onClick={() => speech.setMine(!mine)}
          data-tooltip={room && mine ? 'Chỉ dừng ghi chữ; micro trong phòng vẫn bật' : undefined}
        >
          <Icon name={mine ? 'micOff' : 'mic'} size={16} />
          {mine ? 'Dừng ghi' : 'Ghi lời tôi'}
        </button>
      )}
      {/* Its own line, so it wraps instead of being cut off beside the button in the room's narrow panel. */}
      {stuck && <p className="speech-mine-note field-error">{stuck}</p>}
      {capture.refused > 0 && (
        <p className="speech-mine-note field-error">{capture.refused}&nbsp;câu không gửi được.</p>
      )}
    </div>
  )
}
