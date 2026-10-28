import { useCallback, useEffect, useRef, useState } from 'react'
import { editTranscriptSegment, getTranscript, getTranscriptText, retryTranscript } from '../../api/transcripts'
import { Badge } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import type { Transcript, TranscriptSegment, TranscriptStatus } from '../../types/transcript'
import { saveBlob } from '../documents/documentFile'
import { formatDuration } from './format'
import { SpeakerList } from './SpeakerList'
import { speakerName, speakerSlotOf } from './speakers'

/**
 * SCRUM-31 — the machine transcript of one recording, under its player. Clicking a time jumps the
 * audio there and the segment being heard is highlighted, so the Secretary can check the text
 * against the voice and correct it in place.
 *
 * The text is machine output and says so: the minutes the Secretary writes from it stay theirs.
 */

const POLL_MS = 4000

type State = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; transcript: Transcript }

export function TranscriptView({
  recordingId,
  fileName,
  currentMs,
  onSeek,
  onStatus,
}: {
  recordingId: string
  /** The recording's file name, to name the saved text after it. */
  fileName: string
  /** Where the player is, to highlight the segment being heard. */
  currentMs: number
  onSeek: (ms: number) => void
  onStatus: (status: TranscriptStatus) => void
}) {
  const toast = useToast()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [editing, setEditing] = useState<string | null>(null)
  const [retrying, setRetrying] = useState(false)
  const [redoing, setRedoing] = useState(false)
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
  }, [load])

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
  if (transcript.status === 'Pending' || transcript.status === 'Processing')
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
  // Only a name the Secretary assigned replaces "Người nói N" on the segments.
  const names = new Map(transcript.speakers.filter((s) => s.assignedName).map((s) => [s.label, s.assignedName!]))
  return (
    <div className="transcript">
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
          {transcript.canEdit && (
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
      <SpeakerList transcript={transcript} onChanged={() => void load()} />
      {transcript.segments.length === 0 ? (
        <p className="field-hint">Không nghe thấy lời nói nào trong bản ghi.</p>
      ) : (
        <ol className="tl-list transcript-list" aria-label="Lời thoại theo thời điểm nói">
          {runs(transcript.segments).map((run) => (
            <li key={run[0].id} className="tl-group">
              <div className="tl-row tl-row-head">
                <span className="tl-time">{formatDuration(run[0].startMs)}</span>
                <span className="tl-rail" aria-hidden="true">
                  <span className="tl-dot" style={{ background: speakerSlotOf(run[0].speakerLabel).dot }} />
                </span>
                <div className="tl-head-row">
                  <strong className="tl-name" style={{ color: speakerSlotOf(run[0].speakerLabel).ink }}>
                    {names.get(run[0].speakerLabel) ?? speakerName(run[0].speakerLabel)}
                  </strong>
                  <span className="tl-time-inline">{formatDuration(run[0].startMs)}</span>
                  <button
                    type="button"
                    className="tl-play"
                    onClick={() => onSeek(run[0].startMs)}
                    aria-label={`Nghe từ ${formatDuration(run[0].startMs)}`}
                    data-tooltip="Nghe lại"
                  >
                    <Icon name="play" size={12} />
                  </button>
                </div>
              </div>
              {run.map((segment, i) => (
                <SegmentRow
                  key={segment.id}
                  segment={segment}
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
          ))}
        </ol>
      )}
    </div>
  )
}

/** Neighbouring segments of one voice read as one run under its name, as in the per-mic record. */
function runs(segments: TranscriptSegment[]) {
  const out: TranscriptSegment[][] = []
  for (const segment of segments) {
    const last = out.at(-1)
    if (last && last[0].speakerLabel === segment.speakerLabel) last.push(segment)
    else out.push([segment])
  }
  return out
}

function SegmentRow({
  segment,
  first,
  active,
  canEdit,
  editing,
  onEdit,
  onDone,
  onSeek,
}: {
  segment: TranscriptSegment
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
        <button
          type="button"
          className="tl-time tl-time-button"
          onClick={onSeek}
          aria-label={`Nghe từ ${formatDuration(segment.startMs)}`}
        >
          {formatDuration(segment.startMs)}
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
              aria-label="Nội dung đoạn"
              aria-invalid={error ? true : undefined}
              autoFocus
            />
            {error && (
              <p className="field-error" role="alert">
                {error}
              </p>
            )}
            <div className="row-actions transcript-editor-actions">
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
          <p className="tl-text">
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
            <button type="button" className="tl-tool" onClick={onEdit} aria-label="Sửa nội dung" data-tooltip="Sửa nội dung">
              <Icon name="pencil" size={16} />
            </button>
          </span>
        )}
      </div>
    </div>
  )
}
