import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { deleteSpeechClip, editSpeechClip, setCaptureMode } from '../../api/speech'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { SegmentedControl } from '../../components/SegmentedControl'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { CAPTURE_MODE_LABEL, type CaptureMode, type SpeechClip } from '../../types/speech'
import { RecordingsPanel } from '../recordings/RecordingsPanel'
import type { Recorder, RecordingInput } from '../recordings/useRecorder'
import { useSpeech, type Speech } from './useSpeech'

/**
 * SCRUM-109 — the per-mic transcript, written once for the meeting page and the call room (ADR-0011). Each line is a
 * sentence someone said into their own mic, named after them. Above it, the person's own switch: they see that what
 * they say is being written down and can stop it; nobody can start it for them.
 */

const CAPTURE_OPTIONS = (['PerMic', 'Room'] as const).map((value) => ({ value, label: CAPTURE_MODE_LABEL[value] }))
const time = new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit', second: '2-digit' })

export function SpeechPanel({
  meetingId,
  variant,
  speech,
  onCaptureMode,
  recorder,
  recordSource,
}: {
  meetingId: string
  variant: 'page' | 'room'
  speech?: Speech
  /** Told the capture mode on every read, so the page can show the room recordings beside it or not. */
  onCaptureMode?: (mode: CaptureMode) => void
  /** The call room's recorder and the whole call as its sound (SCRUM-44), for a meeting captured as one room. */
  recorder?: Recorder
  recordSource?: () => RecordingInput
}) {
  // A meeting captured as one room has no per-mic lines: in the call, the panel is where the Secretary records it.
  if (speech && variant === 'room' && speech.list?.captureMode === 'Room')
    return <RecordingsPanel meetingId={meetingId} variant="room" recorder={recorder} source={recordSource} />
  return speech ? (
    <SpeechView speech={speech} variant={variant} />
  ) : (
    <PageSpeech meetingId={meetingId} onCaptureMode={onCaptureMode} />
  )
}

/** On the meeting page — an in-person meeting, each person at their own laptop — the switch starts off. */
function PageSpeech({ meetingId, onCaptureMode }: { meetingId: string; onCaptureMode?: (mode: CaptureMode) => void }) {
  const speech = useSpeech(meetingId, { defaultOn: false })
  const mode = speech.list?.captureMode
  useEffect(() => {
    if (mode) onCaptureMode?.(mode)
  }, [mode, onCaptureMode])
  return <SpeechView speech={speech} variant="page" />
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
      {list?.canCapture && <MyMic speech={speech} room={room} />}
      {list && (perMic || clips.length > 0) && <Lines speech={speech} room={room} />}
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
  const status =
    capture.state === 'error'
      ? capture.error
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
      <button
        type="button"
        className={mine ? 'btn btn-outline btn-sm' : 'btn btn-primary btn-sm'}
        onClick={() => speech.setMine(!mine)}
        data-tooltip={room && mine ? 'Chỉ dừng ghi chữ; micro trong phòng vẫn bật' : undefined}
      >
        <Icon name={mine ? 'micOff' : 'mic'} size={16} />
        {mine ? 'Dừng ghi' : 'Ghi lời tôi'}
      </button>
      {/* Its own line, so it wraps instead of being cut off beside the button in the room's narrow panel. */}
      {stuck && <p className="speech-mine-note field-error">{stuck}</p>}
      {capture.refused > 0 && (
        <p className="speech-mine-note field-error">{capture.refused}&nbsp;câu không gửi được.</p>
      )}
    </div>
  )
}

function Lines({ speech, room }: { speech: Speech; room: boolean }) {
  const { clips, list } = speech
  const box = useRef<HTMLOListElement>(null)
  const stuck = useRef(true)
  const [removing, setRemoving] = useState<SpeechClip | null>(null)
  const toast = useToast()

  // Like a chat: a new line scrolls into view while the reader is at the bottom, and leaves them alone when not.
  useLayoutEffect(() => {
    const el = box.current
    if (el && stuck.current) el.scrollTop = el.scrollHeight
  }, [clips.length])

  if (clips.length === 0)
    return (
      <p className="speech-empty field-hint">
        {list?.canCapture ? 'Chưa có câu nào. Chữ hiện ở đây sau mỗi câu nói.' : 'Chưa có câu nào.'}
      </p>
    )

  return (
    <>
      <ol
        className={`speech-lines${room ? ' speech-lines-room' : ''}`}
        ref={box}
        aria-label="Lời nói theo từng câu"
        onScroll={(e) => {
          const el = e.currentTarget
          stuck.current = el.scrollHeight - el.scrollTop - el.clientHeight < 40
        }}
      >
        {clips.map((clip, i) => (
          <Line
            key={clip.id}
            clip={clip}
            first={i === 0 || clips[i - 1].speakerId !== clip.speakerId}
            canEdit={!!list?.canEdit}
            onRemove={() => setRemoving(clip)}
          />
        ))}
      </ol>
      {removing && (
        <ConfirmDialog
          title="Xoá câu này?"
          confirmLabel="Xoá câu"
          pendingLabel="Đang xoá…"
          cancelLabel="Giữ lại"
          tone="danger"
          onConfirm={async () => {
            toast.show((await deleteSpeechClip(removing.id)).message, 'success')
            speech.reload()
          }}
          onClose={() => setRemoving(null)}
        >
          <p>“{removing.text}”</p>
          <p className="field-hint">Câu bị xoá khỏi bản gỡ băng của cuộc họp.</p>
        </ConfirmDialog>
      )}
    </>
  )
}

function Line({
  clip,
  first,
  canEdit,
  onRemove,
}: {
  clip: SpeechClip
  first: boolean
  canEdit: boolean
  onRemove: () => void
}) {
  const toast = useToast()
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(clip.text)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      toast.show((await editSpeechClip(clip.id, draft)).message, 'success')
      setEditing(false)
    } catch (failure) {
      setError(errorMessage(failure))
    } finally {
      setSaving(false)
    }
  }

  return (
    <li className={`speech-line${first ? ' speech-line-first' : ''}`}>
      {first && (
        <span className="speech-line-head">
          <strong>{clip.speakerName}</strong>
          <time dateTime={clip.startedAt}>{time.format(new Date(clip.startedAt))}</time>
        </span>
      )}
      {editing ? (
        <div className="transcript-editor">
          <textarea
            value={draft}
            maxLength={8000}
            rows={Math.min(6, Math.max(2, Math.ceil(draft.length / 60)))}
            onChange={(e) => setDraft(e.target.value)}
            aria-label="Nội dung câu"
            aria-invalid={error ? true : undefined}
            autoFocus
          />
          {error && (
            <p className="field-error" role="alert">
              {error}
            </p>
          )}
          <div className="row-actions transcript-editor-actions">
            <button
              type="button"
              className="btn btn-ghost btn-sm"
              disabled={saving}
              onClick={() => {
                setDraft(clip.text)
                setEditing(false)
              }}
            >
              Huỷ
            </button>
            <button
              type="button"
              className="btn btn-primary btn-sm"
              disabled={saving || !draft.trim()}
              onClick={() => void save()}
            >
              {saving ? 'Đang lưu…' : 'Lưu'}
            </button>
          </div>
        </div>
      ) : (
        <p className="speech-line-text" title={first ? undefined : time.format(new Date(clip.startedAt))}>
          {clip.text}
          {clip.editedAt && (
            <span
              className="transcript-edited"
              tabIndex={0}
              data-tooltip={`Sửa bởi ${clip.editedByName ?? 'Thư ký'} lúc ${formatDateTime(clip.editedAt)}`}
            >
              {' '}
              đã sửa
            </span>
          )}
        </p>
      )}
      {!editing && (canEdit || clip.canDelete) && (
        <span className="speech-line-actions">
          {canEdit && (
            <button
              type="button"
              className="btn btn-ghost btn-icon btn-sm"
              aria-label="Sửa câu này"
              data-tooltip="Sửa câu này"
              onClick={() => {
                setDraft(clip.text)
                setEditing(true)
              }}
            >
              <Icon name="pencil" size={16} />
            </button>
          )}
          {clip.canDelete && (
            <button
              type="button"
              className="btn btn-ghost btn-icon btn-sm"
              aria-label="Xoá câu này"
              data-tooltip="Xoá câu này"
              onClick={onRemove}
            >
              <Icon name="trash" size={16} />
            </button>
          )}
        </span>
      )}
    </li>
  )
}
