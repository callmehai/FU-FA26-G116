import { useCallback, useEffect, useRef, useState } from 'react'
import { editTranscriptSegment, getTranscript, retryTranscript } from '../../api/transcripts'
import { Badge } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import type { Transcript, TranscriptSegment, TranscriptStatus } from '../../types/transcript'
import { formatDuration } from './format'
import { SpeakerList } from './SpeakerList'
import { speakerName, speakerTone } from './speakers'

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
  currentMs,
  onSeek,
  onStatus,
}: {
  recordingId: string
  /** Where the player is, to highlight the segment being heard. */
  currentMs: number
  onSeek: (ms: number) => void
  onStatus: (status: TranscriptStatus) => void
}) {
  const toast = useToast()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [editing, setEditing] = useState<string | null>(null)
  const [retrying, setRetrying] = useState(false)
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
      <p className="transcript-note">
        <Badge tone="outline">Máy nhận dạng</Badge>
        <span className="field-hint">Chữ do máy tạo, có thể sai — bấm vào mốc giờ để nghe lại đoạn đó.</span>
      </p>
      <SpeakerList transcript={transcript} onChanged={() => void load()} />
      {transcript.segments.length === 0 ? (
        <p className="field-hint">Không nghe thấy lời nói nào trong bản ghi.</p>
      ) : (
        <ol className="transcript-segments">
          {transcript.segments.map((segment) => (
            <SegmentRow
              key={segment.id}
              segment={segment}
              name={names.get(segment.speakerLabel)}
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
        </ol>
      )}
    </div>
  )
}

function SegmentRow({
  segment,
  name,
  active,
  canEdit,
  editing,
  onEdit,
  onDone,
  onSeek,
}: {
  segment: TranscriptSegment
  name: string | undefined
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
    <li className={`transcript-segment${active ? ' transcript-active' : ''}`}>
      <button
        type="button"
        className="transcript-time"
        onClick={onSeek}
        aria-label={`Nghe từ ${formatDuration(segment.startMs)}`}
      >
        {formatDuration(segment.startMs)}
      </button>
      <div className="transcript-body">
        <span className="transcript-speaker">
          <Badge tone={speakerTone(segment.speakerLabel)}>{name ?? speakerName(segment.speakerLabel)}</Badge>
          {segment.editedAt && (
            <span
              className="transcript-edited"
              tabIndex={0}
              data-tooltip={`Sửa bởi ${segment.editedByName ?? 'Thư ký'} lúc ${formatDateTime(segment.editedAt)}`}
            >
              đã sửa
            </span>
          )}
        </span>
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
          <p className="transcript-text">{segment.text}</p>
        )}
      </div>
      {canEdit && !editing && (
        <button
          type="button"
          className="btn btn-ghost btn-icon btn-sm transcript-edit"
          onClick={onEdit}
          aria-label="Sửa đoạn này"
          data-tooltip="Sửa đoạn này"
        >
          <Icon name="pencil" size={16} />
        </button>
      )}
    </li>
  )
}
