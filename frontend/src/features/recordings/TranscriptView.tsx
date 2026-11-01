import { useCallback, useEffect, useRef, useState } from 'react'
import { deleteRoomTake } from '../../api/recordings'
import { editTranscriptSegment, getTranscript, getTranscriptText, retryTranscript } from '../../api/transcripts'
import { Badge } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import type { RecordingTake } from '../../types/recording'
import type { Transcript, TranscriptSegment, TranscriptStatus } from '../../types/transcript'
import { saveBlob } from '../documents/documentFile'
import { formatDuration } from './format'
import { SpeakerList, SpeakerPicker } from './SpeakerList'
import { speakerName, speakerSlotOf, voiceName, voicesOf } from './speakers'

/**
 * SCRUM-31 — the machine transcript of one recording, under its player. Clicking a time jumps the
 * audio there and the segment being heard is highlighted, so the Secretary can check the text
 * against the voice and correct it in place.
 *
 * The text is machine output and says so: the minutes the Secretary writes from it stay theirs.
 */

const POLL_MS = 4000

const wallClock = new Intl.DateTimeFormat('vi-VN', {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
})

/** The take a moment of the audio belongs to: the last one starting at or before it. */
function takeAt(takes: RecordingTake[], ms: number) {
  let index = 0
  for (let i = 0; i < takes.length; i++) if (takes[i].offsetMs <= ms) index = i
  return index
}

/**
 * How a moment of the recording reads: the room mic's takes (ADR-0041) carry when each was said, so a moment reads on
 * the meeting's clock; an uploaded file only knows how far into it a moment is.
 */
function timeLabel(takes: RecordingTake[] | null | undefined, ms: number) {
  if (!takes?.length) return formatDuration(ms)
  const take = takes[takeAt(takes, ms)]
  return wallClock.format(new Date(Date.parse(take.startedAt) + ms - take.offsetMs))
}

type State = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; transcript: Transcript }

export function TranscriptView({
  recordingId,
  fileName,
  takes,
  listedStatus,
  currentMs,
  playing = false,
  onPause,
  onSeek,
  onStatus,
}: {
  recordingId: string
  /** The recording's file name, to name the saved text after it. */
  fileName: string
  /** The room mic's takes, for the meeting's clock and a line where each new one starts. */
  takes?: RecordingTake[] | null
  /** The status the recordings list last read: a take appended makes it wait again, and the text is read anew. */
  listedStatus?: TranscriptStatus
  /** Where the player is, to highlight the segment being heard. */
  currentMs: number
  /** The player is playing: the run being heard offers a pause where its play button was. */
  playing?: boolean
  onPause?: () => void
  onSeek: (ms: number) => void
  onStatus: (status: TranscriptStatus) => void
}) {
  const toast = useToast()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [editing, setEditing] = useState<string | null>(null)
  const [retrying, setRetrying] = useState(false)
  const [redoing, setRedoing] = useState(false)
  // The take (from 1) the Secretary asked to erase, until they confirm.
  const [dropping, setDropping] = useState<number | null>(null)
  const [saving, setSaving] = useState(false)
  const generation = useRef(0)

  const load = useCallback(async () => {
    const mine = generation.current
    try {
      const transcript = await getTranscript(recordingId)
      if (mine !== generation.current) return
      setState({ kind: 'loaded', transcript })
      onStatus(transcript.status)
    } catch (error) {
      if (mine === generation.current) setState({ kind: 'error', message: errorMessage(error) })
    }
  }, [recordingId, onStatus])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load, listedStatus])

  const status = state.kind === 'loaded' ? state.transcript.status : null
  useEffect(() => {
    if (status !== 'Pending' && status !== 'Processing') return
    const timer = window.setInterval(() => void load(), POLL_MS)
    return () => window.clearInterval(timer)
  }, [status, load])

  const retry = async () => {
    setRetrying(true)
    try {
      toast.show((await retryTranscript(recordingId)).message, 'success')
      await load()
    } catch (error) {
      toast.show(errorMessage(error), 'error')
      await load()
    } finally {
      setRetrying(false)
    }
  }

  const saveText = async () => {
    setSaving(true)
    try {
      saveBlob(await getTranscriptText(recordingId), `${fileName.replace(/\.[^.]+$/, '')} - bản gỡ băng.txt`)
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      setSaving(false)
    }
  }

  if (state.kind === 'loading') return <p className="transcript-status field-hint">Đang tải bản gỡ băng…</p>
  if (state.kind === 'error')
    return (
      <div className="transcript-status alert alert-error" role="alert">
        <p>{state.message}</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
          <Icon name="refresh" size={16} />
          Thử lại
        </button>
      </div>
    )

  const { transcript } = state
  const running = transcript.status === 'Pending' || transcript.status === 'Processing'
  // A room recording that just had a take appended keeps showing what it said so far while the whole is redone.
  if (running && transcript.segments.length === 0)
    return (
      <p className="transcript-status transcript-running" role="status">
        <span className="transcript-spinner" aria-hidden="true" />
        {transcript.status === 'Pending' ? 'Đang chờ chuyển thành chữ…' : 'Đang chuyển thành chữ…'}
        {transcript.durationMs > 0 && (
          <span className="field-hint">
            {' '}
            Bản ghi dài {formatDuration(transcript.durationMs)}, thường xong trong khoảng{' '}
            {Math.max(1, Math.ceil(transcript.durationMs / 60000 / 4))} phút.
          </span>
        )}
      </p>
    )
  if (transcript.status === 'None' || transcript.status === 'Failed')
    return (
      <div className={`transcript-status alert ${transcript.status === 'Failed' ? 'alert-error' : 'alert-info'}`}>
        <p>
          {transcript.status === 'Failed'
            ? `Chưa chuyển được thành chữ: ${transcript.errorMessage}`
            : 'Bản ghi này chưa được chuyển thành chữ.'}
        </p>
        {transcript.canEdit && (
          <button type="button" className="btn btn-outline btn-sm" disabled={retrying} onClick={() => void retry()}>
            <Icon name="refresh" size={16} />
            {retrying ? 'Đang gửi…' : transcript.status === 'Failed' ? 'Chạy lại' : 'Chuyển thành chữ'}
          </button>
        )}
      </div>
    )

  const active = transcript.segments.findLast((s) => s.startMs <= currentMs)?.id
  // Voices named as one person read as one speaker: one colour, one name, one run of lines (AI-eCabinet's voiceOf).
  // Only a name the Secretary assigned replaces "Người nói N".
  const { voices, one } = voicesOf(transcript.speakers)
  const at = (ms: number) => timeLabel(takes, ms)
  const marked = takes && takes.length > 1 ? takes : null
  return (
    <div className="transcript">
      {running && (
        <p className="transcript-status transcript-running" role="status">
          <span className="transcript-spinner" aria-hidden="true" />
          Đang chuyển chữ lại cả bản ghi, có lượt ghi mới. Chữ dưới đây là bản trước.
        </p>
      )}
      <div className="transcript-note">
        <Badge tone="outline">Máy nhận dạng</Badge>
        <span className="field-hint">Chữ do máy tạo, có thể sai — bấm vào mốc giờ để nghe lại đoạn đó.</span>
        <span className="transcript-tools">
          {transcript.canExport && (
            <button
              type="button"
              className="btn btn-ghost btn-icon btn-sm"
              aria-label="Tải bản gỡ băng (.txt)"
              data-tooltip="Tải bản gỡ băng (.txt)"
              disabled={saving}
              onClick={() => void saveText()}
            >
              <Icon name="download" size={16} />
            </button>
          )}
          {transcript.canEdit && !running && (
            <button
              type="button"
              className="btn btn-ghost btn-icon btn-sm"
              aria-label="Chuyển thành chữ lại"
              data-tooltip="Chuyển thành chữ lại"
              onClick={() => setRedoing(true)}
            >
              <Icon name="refresh" size={16} />
            </button>
          )}
        </span>
      </div>
      {redoing && (
        <ConfirmDialog
          title="Chuyển thành chữ lại?"
          confirmLabel="Chuyển lại"
          pendingLabel="Đang gửi…"
          cancelLabel="Giữ bản hiện có"
          onConfirm={async () => {
            toast.show((await retryTranscript(recordingId)).message, 'success')
            await load()
          }}
          onClose={() => setRedoing(false)}
        >
          <p>Máy làm lại bản gỡ băng từ đầu, để dùng model mới hay giọng mẫu vừa đăng ký.</p>
          <p className="field-hint">Khi xong, chữ đã sửa và tên đã gán cho người nói sẽ được thay bằng bản mới.</p>
        </ConfirmDialog>
      )}
      {dropping !== null && (
        <ConfirmDialog
          title={`Xoá lượt ${dropping}?`}
          tone="danger"
          confirmLabel="Xoá lượt"
          pendingLabel="Đang xoá…"
          cancelLabel="Giữ lại"
          onConfirm={async () => {
            toast.show((await deleteRoomTake(recordingId, dropping)).message, 'success')
            await load()
          }}
          onClose={() => setDropping(null)}
        >
          <p>Tiếng và chữ của lượt này bị xoá hẳn, không lấy lại được.</p>
          <p className="field-hint">Các lượt khác giữ nguyên chữ đã sửa và tên đã gán.</p>
        </ConfirmDialog>
      )}
      <SpeakerList transcript={transcript} onChanged={() => void load()} />
      {transcript.segments.length === 0 ? (
        <p className="field-hint">Không nghe thấy lời nói nào trong bản ghi.</p>
      ) : (
        <ol className="tl-list transcript-list" aria-label="Lời thoại theo thời điểm nói">
          {runs(transcript.segments, marked, one).map((run, i, all) => {
            const voice = voices.get(one(run[0].speakerLabel))
            const tone = speakerSlotOf(one(run[0].speakerLabel))
            // Heard right now: the run's button pauses it.
            const hearing = playing && currentMs >= run[0].startMs && currentMs < run[run.length - 1].endMs
            return (
              <li key={run[0].id} className="tl-group">
                {marked && (i === 0 || takeAt(marked, all[i - 1][0].startMs) !== takeAt(marked, run[0].startMs)) && (
                  <TakeLine
                    takes={marked}
                    index={takeAt(marked, run[0].startMs)}
                    onDelete={
                      transcript.canEdit && !running ? () => setDropping(takeAt(marked, run[0].startMs) + 1) : undefined
                    }
                  />
                )}
                <div className="tl-row tl-row-head">
                  <span className="tl-time">{at(run[0].startMs)}</span>
                  <span className="tl-rail" aria-hidden="true">
                    <span className="tl-dot" style={{ background: tone.dot }} />
                  </span>
                  <div className="tl-head-row">
                    {/* The Secretary names a speaker where it speaks, as in AI-eCabinet: the name is its picker. */}
                    {transcript.canEdit && voice ? (
                      <span className="tl-name" style={{ color: tone.ink }}>
                        <SpeakerPicker transcript={transcript} voice={voice} onChanged={() => void load()} inline />
                      </span>
                    ) : (
                      <strong className="tl-name" style={{ color: tone.ink }}>
                        {voice ? voiceName(voice.speaker) : speakerName(run[0].speakerLabel)}
                      </strong>
                    )}
                    <span className="tl-time-inline">{at(run[0].startMs)}</span>
                    <button
                      type="button"
                      className="tl-play"
                      onClick={() => (hearing && onPause ? onPause() : onSeek(run[0].startMs))}
                      aria-label={hearing ? 'Tạm dừng' : `Nghe từ ${at(run[0].startMs)}`}
                      aria-pressed={hearing}
                      data-tooltip={hearing ? 'Tạm dừng' : 'Nghe lại'}
                    >
                      <Icon name={hearing ? 'pause' : 'play'} size={12} />
                    </button>
                  </div>
                </div>
                {run.map((segment, i) => (
                  <SegmentRow
                    key={segment.id}
                    segment={segment}
                    time={at(segment.startMs)}
                    first={i === 0}
                    active={segment.id === active}
                    canEdit={transcript.canEdit}
                    editing={editing === segment.id}
                    onEdit={() => setEditing(segment.id)}
                    onDone={(saved) => {
                      setEditing(null)
                      if (saved) void load()
                    }}
                    onSeek={() => onSeek(segment.startMs)}
                  />
                ))}
              </li>
            )
          })}
        </ol>
      )}
    </div>
  )
}

/**
 * Neighbouring segments of one voice read as one run under its name, as in the per-mic record. A run never crosses
 * from one take of the room mic into the next: the mic was off in between.
 */
function runs(segments: TranscriptSegment[], takes: RecordingTake[] | null, one: (label: string) => string) {
  const out: TranscriptSegment[][] = []
  for (const segment of segments) {
    const last = out.at(-1)
    const sameTake = !takes || takeAt(takes, last?.[0].startMs ?? 0) === takeAt(takes, segment.startMs)
    if (last && sameTake && one(last[0].speakerLabel) === one(segment.speakerLabel)) last.push(segment)
    else out.push([segment])
  }
  return out
}

/** Where a take of the room mic begins: its number and the clock it ran by. */
function TakeLine({ takes, index, onDelete }: { takes: RecordingTake[]; index: number; onDelete?: () => void }) {
  const take = takes[index]
  const from = Date.parse(take.startedAt)
  return (
    <div className="tl-take">
      <span role="separator">
        Lượt {index + 1} · {wallClock.format(new Date(from))}–{wallClock.format(new Date(from + take.durationMs))}
      </span>
      {onDelete && (
        <button
          type="button"
          className="tl-tool tl-take-delete"
          onClick={onDelete}
          aria-label={`Xoá lượt ${index + 1}`}
          data-tooltip="Xoá lượt này"
        >
          <Icon name="trash" size={14} />
        </button>
      )}
    </div>
  )
}

function SegmentRow({
  segment,
  time,
  first,
  active,
  canEdit,
  editing,
  onEdit,
  onDone,
  onSeek,
}: {
  segment: TranscriptSegment
  /** How the segment's moment reads: the meeting's clock for the room mic, the file's offset for an upload. */
  time: string
  first: boolean
  active: boolean
  canEdit: boolean
  editing: boolean
  onEdit: () => void
  onDone: (saved: boolean) => void
  onSeek: () => void
}) {
  const toast = useToast()
  const [draft, setDraft] = useState(segment.text)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      toast.show((await editTranscriptSegment(segment.id, draft)).message, 'success')
      onDone(true)
    } catch (failure) {
      setError(errorMessage(failure))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className={`tl-row tl-sentence${active ? ' is-active' : ''}`}>
      {first ? (
        <span className="tl-time" />
      ) : (
        <button type="button" className="tl-time tl-time-button" onClick={onSeek} aria-label={`Nghe từ ${time}`}>
          {time}
        </button>
      )}
      <span className="tl-rail" aria-hidden="true" />
      <div className="tl-content">
        {editing ? (
          <div className="transcript-editor">
            <textarea
              value={draft}
              maxLength={8000}
              rows={Math.min(8, Math.max(2, Math.ceil(draft.length / 90)))}
              onChange={(e) => setDraft(e.target.value)}
              // As in AI-eCabinet: Enter saves, Shift+Enter breaks the line, Esc leaves it as it was.
              onKeyDown={(e) => {
                if (e.key === 'Escape') onDone(false)
                if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
                  e.preventDefault()
                  if (draft.trim() && !saving) void save()
                }
              }}
              onFocus={(e) => e.currentTarget.setSelectionRange(draft.length, draft.length)}
              aria-label="Nội dung đoạn"
              aria-invalid={error ? true : undefined}
              aria-describedby={`edit-keys-${segment.id}`}
              autoFocus
            />
            {error && (
              <p className="field-error" role="alert">
                {error}
              </p>
            )}
            <div className="row-actions transcript-editor-actions">
              <span id={`edit-keys-${segment.id}`} className="field-hint transcript-editor-keys">
                Enter để lưu · Esc để huỷ
              </span>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => onDone(false)} disabled={saving}>
                Huỷ
              </button>
              <button
                type="button"
                className="btn btn-primary btn-sm"
                onClick={() => void save()}
                disabled={saving || !draft.trim()}
              >
                {saving ? 'Đang lưu…' : 'Lưu'}
              </button>
            </div>
          </div>
        ) : (
          <p
            className={`tl-text${canEdit ? ' is-editable' : ''}`}
            // A click on the words edits them, as in AI-eCabinet; selecting words to copy them does not.
            onClick={canEdit ? () => !window.getSelection()?.toString() && onEdit() : undefined}
            data-tooltip={canEdit ? 'Bấm để sửa' : undefined}
          >
            {segment.text}
            {segment.editedAt && (
              <span
                className="tl-edited"
                tabIndex={0}
                data-tooltip={`Sửa bởi ${segment.editedByName ?? 'Thư ký'} lúc ${formatDateTime(segment.editedAt)}`}
              >
                {' '}
                đã sửa
              </span>
            )}
          </p>
        )}
        {canEdit && !editing && (
          <span className="tl-tools">
            <button
              type="button"
              className="tl-tool"
              onClick={onEdit}
              aria-label="Sửa nội dung"
              data-tooltip="Sửa nội dung"
            >
              <Icon name="pencil" size={16} />
            </button>
          </span>
        )}
      </div>
    </div>
  )
}
