import { useCallback, useEffect, useRef, useState } from 'react'
import { eraseRecording, getRecordingPlayback, listRecordings, uploadRecording } from '../../api/recordings'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { Badge, type BadgeTone } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDate, formatDateTime } from '../../lib/format'
import type { Recording, RecordingList, RecordingSource } from '../../types/recording'
import type { TranscriptStatus } from '../../types/transcript'
import { useAccessLost } from '../auth/useAccessLost'
import { formatDuration, formatSize, probeDuration } from './format'
import { TranscriptView } from './TranscriptView'
import { useRecorder, type RecordedTake } from './useRecorder'

/**
 * SCRUM-29 — the meeting's recordings, as a panel (ADR-0011): the detail tab renders it, and the
 * call room's side panel can render the same component.
 *
 * Nothing is recorded or kept before the Secretary confirms the participants were told and nobody
 * objected; the purpose they confirm is stored with the file, and every row says when it will be
 * erased.
 */

const DEFAULT_PURPOSE = 'Lập biên bản cuộc họp'
const ACCEPT = '.mp3,.m4a,.aac,.wav,.webm,.ogg,audio/*'
/** Same pace as an open transcript (TranscriptView). */
const TRANSCRIPT_POLL_MS = 4000

type LoadState = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; data: RecordingList }

type Consent = { kind: 'record' } | { kind: 'upload'; file: File }

/** A take or file that failed to save stays here, so a network hiccup never loses a meeting. */
interface PendingUpload {
  blob: Blob
  fileName: string
  source: RecordingSource
  durationMs?: number
  purpose: string
  error: string | null
}

function takeName(take: RecordedTake) {
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `ghi-am-${pad(now.getDate())}-${pad(now.getMonth() + 1)}-${now.getFullYear()}-${pad(now.getHours())}${pad(now.getMinutes())}.${take.extension}`
}

export function RecordingsPanel({ meetingId, variant = 'page' }: { meetingId: string; variant?: 'page' | 'room' }) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const recorder = useRecorder()
  const [state, setState] = useState<LoadState>({ kind: 'loading' })
  const [consent, setConsent] = useState<Consent | null>(null)
  const [purpose, setPurpose] = useState(DEFAULT_PURPOSE)
  const [pending, setPending] = useState<PendingUpload | null>(null)
  const [saving, setSaving] = useState(false)
  const [erasing, setErasing] = useState<Recording | null>(null)
  const fileInput = useRef<HTMLInputElement>(null)

  // A response that arrives after the panel moved on (another meeting, unmounted) is dropped.
  const generation = useRef(0)
  const load = useCallback(
    async (quiet = false) => {
      const mine = generation.current
      try {
        const data = await listRecordings(meetingId)
        if (mine === generation.current) setState({ kind: 'loaded', data })
      } catch (error) {
        // A background refresh that fails keeps the list on screen; the next one tries again.
        if (mine !== generation.current || handleAccessLost(error) || quiet) return
        setState({ kind: 'error', message: errorMessage(error) })
      }
    },
    [meetingId, handleAccessLost],
  )

  useEffect(() => {
    void Promise.resolve().then(() => load())
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  // Transcripts are made in the background after a save (SCRUM-31). Follow the list until none is
  // left waiting, so a row's badge changes without opening it or reloading the page.
  const transcribing =
    state.kind === 'loaded' &&
    state.data.items.some(
      (r) => r.status !== 'Deleted' && (r.transcriptStatus === 'Pending' || r.transcriptStatus === 'Processing'),
    )
  useEffect(() => {
    if (!transcribing) return
    const timer = window.setInterval(() => void load(true), TRANSCRIPT_POLL_MS)
    return () => window.clearInterval(timer)
  }, [transcribing, load])

  const save = useCallback(
    async (upload: PendingUpload) => {
      setSaving(true)
      try {
        const result = await uploadRecording(meetingId, {
          file: upload.blob,
          fileName: upload.fileName,
          source: upload.source,
          consentPurpose: upload.purpose,
          durationMs: upload.durationMs,
        })
        setPending(null)
        toast.show(result.message, 'success')
        await load()
      } catch (error) {
        // The meeting ended for recording, or the role moved: the list now tells the truth.
        if (isApiError(error, 409, 'MSG294') || isApiError(error, 403, 'MSG295')) void load()
        else if (handleAccessLost(error)) return
        setPending({ ...upload, error: errorMessage(error) })
      } finally {
        setSaving(false)
      }
    },
    [meetingId, toast, load, handleAccessLost],
  )

  const stopAndSave = async () => {
    const take = await recorder.stop()
    if (!take) return
    await save({
      blob: take.blob,
      fileName: takeName(take),
      source: 'BrowserCapture',
      durationMs: take.durationMs,
      purpose,
      error: null,
    })
  }

  const downloadPending = () => {
    if (!pending) return
    const url = URL.createObjectURL(pending.blob)
    const link = document.createElement('a')
    link.href = url
    link.download = pending.fileName
    link.click()
    window.setTimeout(() => URL.revokeObjectURL(url), 1000)
  }

  const data = state.kind === 'loaded' ? state.data : null
  const canUpload = data?.canUpload ?? false
  const busy = recorder.active || recorder.state.kind === 'starting' || saving || pending !== null

  return (
    <section className={`list-panel recordings-panel recordings-${variant}`} aria-label="Bản ghi">
      <div className="toolbar">
        <h2 className="recordings-title">Bản ghi</h2>
        {canUpload && (
          <div className="toolbar-end row-actions">
            <button
              type="button"
              className="btn btn-outline btn-icon"
              onClick={() => fileInput.current?.click()}
              disabled={busy}
              aria-label="Tải tệp ghi âm lên"
              data-tooltip="Tải tệp ghi âm lên (mp3, m4a, wav, webm, ogg)"
            >
              <Icon name="upload" size={18} />
            </button>
            <button
              type="button"
              className="btn btn-primary btn-cta"
              onClick={() => setConsent({ kind: 'record' })}
              disabled={busy}
            >
              <Icon name="mic" size={18} />
              Ghi âm
            </button>
            <input
              ref={fileInput}
              type="file"
              accept={ACCEPT}
              hidden
              onChange={(event) => {
                const file = event.target.files?.[0]
                event.target.value = ''
                if (file) setConsent({ kind: 'upload', file })
              }}
            />
          </div>
        )}
      </div>

      {recorder.state.kind === 'error' && (
        <div className="alert alert-error recordings-alert" role="alert">
          <p>{recorder.state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={recorder.reset}>
            Đóng
          </button>
        </div>
      )}

      {(recorder.state.kind === 'recording' || recorder.state.kind === 'paused') && (
        <div className={`recorder-bar${recorder.state.kind === 'paused' ? ' recorder-paused' : ''}`} role="status">
          <span className="recorder-dot" aria-hidden="true" />
          <span className="recorder-label">{recorder.state.kind === 'paused' ? 'Đã tạm dừng' : 'Đang ghi âm'}</span>
          <span className="recorder-time">{formatDuration(recorder.state.elapsedMs)}</span>
          <div className="row-actions recorder-actions">
            {recorder.state.kind === 'recording' ? (
              <button
                type="button"
                className="btn btn-outline btn-icon btn-sm"
                onClick={recorder.pause}
                aria-label="Tạm dừng"
                data-tooltip="Tạm dừng"
              >
                <Icon name="pause" size={16} />
              </button>
            ) : (
              <button
                type="button"
                className="btn btn-outline btn-icon btn-sm"
                onClick={recorder.resume}
                aria-label="Ghi tiếp"
                data-tooltip="Ghi tiếp"
              >
                <Icon name="mic" size={16} />
              </button>
            )}
            <button type="button" className="btn btn-primary btn-sm" onClick={() => void stopAndSave()}>
              <Icon name="recordStop" size={16} />
              Dừng và lưu
            </button>
          </div>
        </div>
      )}

      {saving && (
        <p className="recordings-saving" role="status">
          Đang lưu bản ghi…
        </p>
      )}

      {pending && !saving && (
        <div className="alert alert-error recordings-alert" role="alert">
          <p>
            Chưa lưu được “{pending.fileName}”: {pending.error} Bản ghi vẫn còn trên trình duyệt này.
          </p>
          <div className="row-actions">
            <button type="button" className="btn btn-primary btn-sm" onClick={() => void save(pending)}>
              <Icon name="refresh" size={16} />
              Lưu lại
            </button>
            <button type="button" className="btn btn-outline btn-sm" onClick={downloadPending}>
              Tải về máy
            </button>
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => setPending(null)}>
              Bỏ
            </button>
          </div>
        </div>
      )}

      {state.kind === 'error' && (
        <div className="alert alert-error recordings-alert" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}

      {state.kind === 'loading' && (
        <ul className="recording-list" aria-busy="true">
          {[0, 1].map((i) => (
            <li key={i} className="recording-row recording-skeleton">
              <span className="recording-icon" />
              <span className="skeleton" />
            </li>
          ))}
        </ul>
      )}

      {data && data.items.length === 0 && (
        <div className="cell-empty">
          <p className="empty-title">Chưa có bản ghi.</p>
          {canUpload && <p className="field-hint">Ghi âm ngay trên trình duyệt hoặc tải tệp ghi sẵn lên.</p>}
        </div>
      )}

      {data && data.items.length > 0 && (
        <ul className="recording-list">
          {data.items.map((recording) => (
            <RecordingRow key={recording.id} recording={recording} onErase={() => setErasing(recording)} />
          ))}
        </ul>
      )}

      {consent && (
        <ConsentDialog
          consent={consent}
          purpose={purpose}
          onPurpose={setPurpose}
          onClose={() => setConsent(null)}
          onConfirm={() => {
            const current = consent
            setConsent(null)
            if (current.kind === 'record') void recorder.start()
            else
              void probeDuration(current.file).then((durationMs) =>
                save({
                  blob: current.file,
                  fileName: current.file.name,
                  source: 'Upload',
                  durationMs,
                  purpose: purpose.trim() || DEFAULT_PURPOSE,
                  error: null,
                }),
              )
          }}
        />
      )}

      {erasing && (
        <ConfirmDialog
          title="Xoá bản ghi?"
          confirmLabel="Xoá bản ghi"
          pendingLabel="Đang xoá…"
          cancelLabel="Giữ lại"
          tone="danger"
          onClose={() => setErasing(null)}
          onConfirm={async () => {
            try {
              const result = await eraseRecording(erasing.id)
              toast.show(result.message, 'success')
            } catch (error) {
              if (isApiError(error, 403, 'MSG295')) {
                void load()
                throw error
              }
              if (handleAccessLost(error)) return
              throw error
            }
            setErasing(null)
            await load()
          }}
        >
          <p>
            Âm thanh và bản gỡ băng của “{erasing.fileName}” bị xoá vĩnh viễn. Hệ thống chỉ giữ lại dòng ghi nhận đã có
            bản ghi này và ai đã xoá.
          </p>
        </ConfirmDialog>
      )}
    </section>
  )
}

const TRANSCRIPT_BADGE: Partial<Record<TranscriptStatus, { tone: BadgeTone; label: string }>> = {
  Pending: { tone: 'amber', label: 'Đang chuyển chữ' },
  Processing: { tone: 'amber', label: 'Đang chuyển chữ' },
  Completed: { tone: 'emerald', label: 'Có bản gỡ băng' },
  Failed: { tone: 'red', label: 'Lỗi chuyển chữ' },
}

function RecordingRow({ recording, onErase }: { recording: Recording; onErase: () => void }) {
  const [playback, setPlayback] = useState<
    { kind: 'loading' } | { kind: 'ready'; url: string } | { kind: 'error'; message: string } | null
  >(null)
  // Follows the transcript while it is open, so the badge changes the moment it finishes; a newer
  // status from the list (refreshed while transcripts are pending) wins whenever it changes.
  const [transcriptStatus, setTranscriptStatus] = useState(recording.transcriptStatus)
  const [listedStatus, setListedStatus] = useState(recording.transcriptStatus)
  if (recording.transcriptStatus !== listedStatus) {
    setListedStatus(recording.transcriptStatus)
    setTranscriptStatus(recording.transcriptStatus)
  }
  const audio = useRef<HTMLAudioElement>(null)
  const [currentMs, setCurrentMs] = useState(0)
  const deleted = recording.status === 'Deleted'
  const badge = deleted ? undefined : TRANSCRIPT_BADGE[transcriptStatus]

  const toggle = async () => {
    if (playback) {
      setPlayback(null)
      return
    }
    setPlayback({ kind: 'loading' })
    try {
      setPlayback({ kind: 'ready', url: (await getRecordingPlayback(recording.id)).url })
    } catch (error) {
      setPlayback({ kind: 'error', message: errorMessage(error) })
    }
  }

  const details = [
    `Lưu bởi ${recording.createdByName} lúc ${formatDateTime(recording.createdAt)}`,
    recording.consentPurpose && `Mục đích: ${recording.consentPurpose}`,
    !deleted && recording.retainUntil && `Tự xoá ngày ${formatDate(recording.retainUntil)}`,
    deleted && recording.deletedAt && `Đã xoá lúc ${formatDateTime(recording.deletedAt)}`,
  ]
    .filter(Boolean)
    .join(' · ')

  return (
    <li className={`recording-row${deleted ? ' recording-deleted' : ''}${playback ? ' recording-open' : ''}`}>
      <span className="recording-icon" aria-hidden="true">
        <Icon name="fileAudio" size={18} />
      </span>
      <div className="recording-main">
        <span className="recording-name" title={recording.fileName}>
          {recording.fileName}
        </span>
        <span className="recording-meta" data-tooltip={details} tabIndex={0}>
          <Badge tone="slate">{recording.source === 'BrowserCapture' ? 'Ghi trực tiếp' : 'Tải lên'}</Badge>
          {deleted ? (
            <Badge tone="red">Đã xoá</Badge>
          ) : (
            <>
              {badge && <Badge tone={badge.tone}>{badge.label}</Badge>}
              {recording.durationMs > 0 && <span>{formatDuration(recording.durationMs)}</span>}
              <span>{formatSize(recording.byteSize)}</span>
            </>
          )}
          <span>{formatDateTime(recording.createdAt)}</span>
        </span>
      </div>
      {!deleted && (
        <div className="row-actions recording-actions">
          <button
            type="button"
            className="btn btn-outline btn-icon btn-sm"
            onClick={() => void toggle()}
            aria-label={playback ? 'Đóng' : 'Nghe và xem bản gỡ băng'}
            aria-expanded={playback !== null}
            data-tooltip={playback ? 'Đóng' : 'Nghe và xem bản gỡ băng'}
          >
            <Icon name={playback ? 'x' : 'play'} size={16} />
          </button>
          {recording.canErase && (
            <button
              type="button"
              className="btn btn-danger btn-icon btn-sm"
              onClick={onErase}
              aria-label="Xoá bản ghi"
              data-tooltip="Xoá bản ghi"
            >
              <Icon name="trash" size={16} />
            </button>
          )}
        </div>
      )}
      {playback?.kind === 'loading' && <p className="recording-player field-hint">Đang mở…</p>}
      {playback?.kind === 'error' && (
        <p className="recording-player field-error" role="alert">
          {playback.message}
        </p>
      )}
      {playback?.kind === 'ready' && (
        <div className="recording-player recording-detail">
          <audio
            ref={audio}
            controls
            preload="metadata"
            src={playback.url}
            onTimeUpdate={(e) => setCurrentMs(e.currentTarget.currentTime * 1000)}
          />
          <TranscriptView
            recordingId={recording.id}
            currentMs={currentMs}
            onSeek={(ms) => {
              const element = audio.current
              if (!element) return
              element.currentTime = ms / 1000
              void element.play().catch(() => undefined)
            }}
            onStatus={setTranscriptStatus}
          />
        </div>
      )}
    </li>
  )
}

function ConsentDialog({
  consent,
  purpose,
  onPurpose,
  onClose,
  onConfirm,
}: {
  consent: Consent
  purpose: string
  onPurpose: (value: string) => void
  onClose: () => void
  onConfirm: () => void
}) {
  const [confirmed, setConfirmed] = useState(false)
  const record = consent.kind === 'record'
  return (
    <Dialog
      title={record ? 'Bắt đầu ghi âm' : 'Lưu tệp ghi âm'}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn-ghost" onClick={onClose}>
            Huỷ
          </button>
          <button type="button" className="btn btn-primary" disabled={!confirmed} onClick={onConfirm}>
            <Icon name={record ? 'mic' : 'upload'} size={18} />
            {record ? 'Bắt đầu ghi' : 'Tải lên'}
          </button>
        </>
      }
    >
      {consent.kind === 'upload' && (
        <p className="consent-file">
          <Icon name="fileAudio" size={16} /> {consent.file.name} · {formatSize(consent.file.size)}
        </p>
      )}
      <label className="field">
        <span>Mục đích ghi âm</span>
        <input value={purpose} maxLength={1000} onChange={(e) => onPurpose(e.target.value)} />
      </label>
      <label className="consent-check">
        <input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
        <span>Tôi đã thông báo cho người dự rằng cuộc họp được ghi âm cho mục đích trên, và không ai phản đối.</span>
      </label>
      <p className="field-hint">Chỉ người trong cuộc họp được nghe; bản ghi tự xoá khi hết thời hạn lưu trữ.</p>
    </Dialog>
  )
}
