import { Fragment, type ReactNode, useCallback, useEffect, useRef, useState } from 'react'
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
import { useMeetingEvents, type MeetingEvent } from '../meetings/useMeetingEvents'
import { formatDuration } from './format'
import { LineSpeakerPicker, SpeakerList } from './SpeakerList'
import { speakerName, speakerSlotOf, voiceName, voicesOf } from './speakers'
import { SpokenWords } from './SpokenWords'

/**
 * SCRUM-31 — the machine transcript of one recording, under its player. Clicking a line jumps the
 * audio there and the line being heard is highlighted, word by word when the worker timed its words,
 * so the Secretary can check the text against the voice and correct it in place.
 *
 * The text is machine output and says so: the minutes the Secretary writes from it stay theirs.
 */

const POLL_MS = 4000

/** A voice named or a line corrected or moved on another screen (team test 21/11/2026): the transcript re-reads. */
const TRANSCRIPT_EVENTS: readonly MeetingEvent[] = ['transcriptChanged']

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
  meetingId,
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
  meetingId: string
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
  // The takes (by index) the reader folded: a meeting of an hour reads as a long list otherwise (team test 21/11/2026).
  const [folded, setFolded] = useState<ReadonlySet<number>>(() => new Set())
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

  // Re-read in place: the list stays mounted, and keeps where the reader scrolled to.
  useMeetingEvents(meetingId, () => void load(), TRANSCRIPT_EVENTS)

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

  if (state.kind === 'loading')
    return (
      <div className="transcript-skeleton" aria-busy="true" aria-label="Đang tải bản gỡ băng">
        <span className="skeleton" />
        <span className="skeleton" />
        <span className="skeleton" />
      </div>
    )
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
  // A line is said by its voice: moving one line to someone moves it to their voice (AI-eCabinet's "just this line"),
  // so lines run together, and take their colour and name, by voice.
  const speakerOf = (segment: TranscriptSegment) => {
    const voice = voices.get(one(segment.speakerLabel))
    return {
      voice,
      name: voice ? voiceName(voice.speaker) : speakerName(segment.speakerLabel),
      tone: speakerSlotOf(one(segment.speakerLabel)),
    }
  }
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
        <span className="field-hint">Chữ do máy tạo, có thể sai — bấm vào một câu để nghe lại.</span>
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
      {/* As the per-mic record: the voices beside the lines on a wide card, above them when narrow. */}
      <div className={transcript.speakers.length > 0 ? 'transcript-body is-split' : 'transcript-body'}>
        <SpeakerList transcript={transcript} onChanged={() => void load()} />
        {transcript.segments.length === 0 ? (
          <p className="field-hint">Không nghe thấy lời nói nào trong bản ghi.</p>
        ) : (
          <ol className="tl-list transcript-list" aria-label="Lời thoại theo thời điểm nói">
            {runs(transcript.segments, marked, (segment) => one(segment.speakerLabel)).map((run, i, all) => {
              const who = speakerOf(run[0])
              const tone = who.tone
              // Heard right now: the run's button pauses it.
              const hearing = playing && currentMs >= run[0].startMs && currentMs < run[run.length - 1].endMs
              const take = marked ? takeAt(marked, run[0].startMs) : 0
              const startsTake = !!marked && (i === 0 || takeAt(marked, all[i - 1][0].startMs) !== take)
              const isFolded = !!marked && folded.has(take)
              // A folded take keeps only its line.
              if (isFolded && !startsTake) return null
              return (
                <li key={run[0].id} className={isFolded ? 'tl-group is-folded' : 'tl-group'}>
                  {marked && startsTake && (
                    <TakeLine
                      takes={marked}
                      index={take}
                      folded={isFolded}
                      onToggle={() =>
                        setFolded((was) => {
                          const next = new Set(was)
                          if (!next.delete(take)) next.add(take)
                          return next
                        })
                      }
                      onDelete={transcript.canEdit && !running ? () => setDropping(take + 1) : undefined}
                    />
                  )}
                  {!isFolded && (
                    <>
                      <div className="tl-row tl-row-head">
                        <span className="tl-time">{at(run[0].startMs)}</span>
                        <span className="tl-rail" aria-hidden="true">
                          <span className="tl-dot" style={{ background: tone.dot }} />
                        </span>
                        <div className="tl-head-row">
                          {/* As in AI-eCabinet, the name heading a run says who said its first line, and changes that line
                              only; a whole voice is named from its card above. */}
                          {transcript.canEdit ? (
                            <span className="tl-name" style={{ color: tone.ink }}>
                              <span className="tl-name-pick">
                                <LineSpeakerPicker
                                  transcript={transcript}
                                  segment={run[0]}
                                  voice={who.voice}
                                  time={at(run[0].startMs)}
                                  onChanged={() => void load()}
                                />
                              </span>
                            </span>
                          ) : (
                            <strong className="tl-name" style={{ color: tone.ink }}>
                              {who.name}
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
                      <RunParagraph
                        run={run}
                        at={at}
                        activeId={active}
                        currentMs={currentMs}
                        playing={playing}
                        canEdit={transcript.canEdit}
                        editing={editing}
                        onEdit={setEditing}
                        onDone={(saved) => {
                          setEditing(null)
                          if (saved) void load()
                        }}
                        onSeek={onSeek}
                        lineSpeaker={(segment) => (
                          <LineSpeakerPicker
                            transcript={transcript}
                            segment={segment}
                            voice={speakerOf(segment).voice}
                            time={at(segment.startMs)}
                            onChanged={() => void load()}
                            icon
                          />
                        )}
                      />
                    </>
                  )}
                </li>
              )
            })}
          </ol>
        )}
      </div>
    </div>
  )
}

/**
 * Neighbouring segments of one voice read as one run under its name, as in the per-mic record. A run never crosses
 * from one take of the room mic into the next: the mic was off in between.
 */
function runs(segments: TranscriptSegment[], takes: RecordingTake[] | null, keyOf: (segment: TranscriptSegment) => string) {
  const out: TranscriptSegment[][] = []
  for (const segment of segments) {
    const last = out.at(-1)
    const sameTake = !takes || takeAt(takes, last?.[0].startMs ?? 0) === takeAt(takes, segment.startMs)
    if (last && sameTake && keyOf(last[0]) === keyOf(segment)) last.push(segment)
    else out.push([segment])
  }
  return out
}

/** Where a take of the room mic begins: its number and the clock it ran by; a click folds or opens its lines. */
function TakeLine({
  takes,
  index,
  folded,
  onToggle,
  onDelete,
}: {
  takes: RecordingTake[]
  index: number
  folded: boolean
  onToggle: () => void
  onDelete?: () => void
}) {
  const take = takes[index]
  const from = Date.parse(take.startedAt)
  return (
    <div className="tl-take">
      <button
        type="button"
        className="tl-take-toggle"
        aria-expanded={!folded}
        onClick={onToggle}
        data-tooltip={folded ? 'Mở lượt này' : 'Thu gọn lượt này'}
      >
        <Icon name={folded ? 'chevronRight' : 'chevronDown'} size={14} />
        Lượt {index + 1} · {wallClock.format(new Date(from))}–{wallClock.format(new Date(from + take.durationMs))}
      </button>
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

/**
 * A voice's run of lines read as one paragraph under its name and time (Hải, 19/11/2026), as the per-mic record does:
 * each line is still its own — a click hears the recording from it, its time shows on hover or focus — and the tools at
 * the paragraph's end act on the line last clicked or focused (the first one until then): the Secretary corrects its
 * words, or moves it alone to another voice.
 */
function RunParagraph({
  run,
  at,
  activeId,
  currentMs,
  playing,
  canEdit,
  editing,
  onEdit,
  onDone,
  onSeek,
  lineSpeaker,
}: {
  run: TranscriptSegment[]
  /** How a moment reads: the meeting's clock for the room mic, the file's offset for an upload. */
  at: (ms: number) => string
  /** The line being heard. */
  activeId: string | undefined
  currentMs: number
  playing: boolean
  canEdit: boolean
  editing: string | null
  onEdit: (segmentId: string) => void
  onDone: (saved: boolean) => void
  onSeek: (ms: number) => void
  /** The person button that changes who said one line; the run's first line is changed from its name. */
  lineSpeaker: (segment: TranscriptSegment) => ReactNode
}) {
  const [pickedId, setPickedId] = useState<string | null>(null)
  const index = Math.max(0, run.findIndex((s) => s.id === pickedId))
  const picked = run[index]
  const several = run.length > 1
  const which = several ? ` câu ${at(picked.startMs)}` : ''
  const editingHere = run.some((s) => s.id === editing)
  return (
    <div className="tl-row tl-sentence">
      <span className="tl-time" />
      <span className="tl-rail" aria-hidden="true" />
      <div className="tl-content">
        <div className="tl-text tl-para">
          {run.map((segment, i) => {
            if (segment.id === editing) return <SegmentEditor key={segment.id} segment={segment} onDone={onDone} />
            const time = at(segment.startMs)
            const active = segment.id === activeId
            return (
              <Fragment key={segment.id}>
                {i > 0 && ' '}
                <span
                  className={`tl-say is-playable${active ? ' is-active' : ''}${
                    several && segment.id === picked.id ? ' is-picked' : ''
                  }`}
                  role="button"
                  tabIndex={0}
                  aria-label={`Nghe từ ${time}: ${segment.text}`}
                  data-tooltip={`Nghe từ ${time}`}
                  onFocus={() => setPickedId(segment.id)}
                  onClick={() => {
                    setPickedId(segment.id)
                    // Selecting words to copy them does not jump the audio.
                    if (!window.getSelection()?.toString()) onSeek(segment.startMs)
                  }}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault()
                      onSeek(segment.startMs)
                    }
                  }}
                >
                  <SpokenWords
                    text={segment.text}
                    starts={segment.wordStartsMs}
                    confidence={segment.wordConfidence}
                    nowMs={active && playing ? currentMs : null}
                  />
                </span>
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
              </Fragment>
            )
          })}
        </div>
        {canEdit && !editingHere && (
          <span className="tl-tools">
            {index > 0 && lineSpeaker(picked)}
            <button
              type="button"
              className="tl-tool"
              onClick={() => onEdit(picked.id)}
              aria-label={`Sửa nội dung${which}`}
              data-tooltip={`Sửa nội dung${which}`}
            >
              <Icon name="pencil" size={16} />
            </button>
          </span>
        )}
      </div>
    </div>
  )
}

/** The Secretary corrects one line's words in place, as in AI-eCabinet: Enter saves, Esc leaves it as it was. */
function SegmentEditor({ segment, onDone }: { segment: TranscriptSegment; onDone: (saved: boolean) => void }) {
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
    <div className="transcript-editor">
      <textarea
        value={draft}
        maxLength={8000}
        rows={Math.min(8, Math.max(2, Math.ceil(draft.length / 90)))}
        onChange={(e) => setDraft(e.target.value)}
        // Shift+Enter breaks the line.
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
  )
}
