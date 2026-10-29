import { useCallback, useEffect, useRef, useState } from 'react'
import { eraseRecording, getRecordingDownload, getRecordingPlayback, listRecordings } from '../../api/recordings'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Badge, type BadgeTone } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDate, formatDateTime } from '../../lib/format'
import type { Recording, RecordingList } from '../../types/recording'
import type { TranscriptStatus } from '../../types/transcript'
import { useAccessLost } from '../auth/useAccessLost'
import { useMeetingEvents } from '../meetings/useMeetingEvents'
import { useUploadsSettled } from '../uploads/uploadsContext'
import { ConsentDialog } from './ConsentDialog'
import { formatDuration, formatSize, probeDuration } from './format'
import { PlayerBar } from './PlayerBar'
import { rememberSpeed, storedSpeed } from './playerSpeed'
import { TranscriptView } from './TranscriptView'
import { DEFAULT_PURPOSE, recordingsTarget, useRecordingSave } from './useRecordingSave'
import { RECORDINGS_EVENTS } from './useRoomRecordings'

/**
 * SCRUM-29 — the meeting's room recordings, in the Bản ghi tab. The Secretary records the room with the mic card in
 * the corner of every tab (RecorderCard), and uploads a file recorded elsewhere from here. ADR-0041: each take of the
 * room mic is appended to one recording, transcribed again as a whole, so the tab reads as one meeting; the take being
 * recorded shows as such to everyone, and nobody hears it before the mic goes off.
 *
 * Nothing is kept before the Secretary confirms the participants were told and nobody objected; the purpose they
 * confirm is stored with the file, and every row says when it will be erased.
 */

const ACCEPT = '.mp3,.m4a,.aac,.wav,.webm,.ogg,audio/*'
/** Same pace as an open transcript (TranscriptView). */
const TRANSCRIPT_POLL_MS = 4000

type LoadState = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; data: RecordingList }

/**
 * `existingOnly`: an online meeting has no room to record (ADR-0040) — show recordings made before, if any, and
 * nothing at all otherwise.
 */
export function RecordingsPanel({ meetingId, existingOnly = false }: { meetingId: string; existingOnly?: boolean }) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const save = useRecordingSave(meetingId)
  const [state, setState] = useState<LoadState>({ kind: 'loading' })
  const [upload, setUpload] = useState<File | null>(null)
  const [purpose, setPurpose] = useState(DEFAULT_PURPOSE)
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

  useUploadsSettled(recordingsTarget(meetingId), () => void load())
  // A take opened or saved, a file kept or erased, a transcript done: on any screen of the meeting.
  useMeetingEvents(meetingId, () => void load(true), RECORDINGS_EVENTS)

  const data = state.kind === 'loaded' ? state.data : null
  const canUpload = !existingOnly && (data?.canUpload ?? false)
  if (existingOnly && data?.items.length === 0) return null

  // An erased recording leaves the tab (PO 29/10/2026); its audit trail stays on the server.
  const kept = data?.items.filter((r) => r.status !== 'Deleted') ?? []
  return (
    <section className="list-panel recordings-panel recordings-page" aria-label="Bản ghi">
      <div className="toolbar">
        <h2 className="recordings-title">Bản ghi</h2>
        {canUpload && (
          <div className="toolbar-end row-actions">
            <button
              type="button"
              className="btn btn-outline"
              onClick={() => fileInput.current?.click()}
              data-tooltip="mp3, m4a, wav, webm, ogg"
            >
              <Icon name="upload" size={18} />
              Tải tệp lên
            </button>
            <input
              ref={fileInput}
              type="file"
              accept={ACCEPT}
              hidden
              onChange={(event) => {
                const file = event.target.files?.[0]
                event.target.value = ''
                if (file) setUpload(file)
              }}
            />
          </div>
        )}
      </div>

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

      {data && kept.length === 0 && (
        <div className="cell-empty">
          <p className="empty-title">Chưa có bản ghi.</p>
          {canUpload && <p className="field-hint">Bật mic phòng ở ô góc màn hình, hoặc tải tệp ghi sẵn lên.</p>}
        </div>
      )}

      {data && kept.length > 0 && (
        <ul className="recording-list">
          {/* Oldest first, each with its transcript under it: the tab reads in the order the meeting went. */}
          {[...kept]
            .reverse()
            .map((recording) =>
              recording.status === 'Pending' ? (
                <OpenTakeRow key={recording.id} take={recording} />
              ) : (
                <RecordingRow key={recording.id} recording={recording} onErase={() => setErasing(recording)} />
              ),
            )}
        </ul>
      )}

      {upload && (
        <ConsentDialog
          consent={{ kind: 'upload', file: upload }}
          purpose={purpose}
          onPurpose={setPurpose}
          onClose={() => setUpload(null)}
          onConfirm={() => {
            const file = upload
            setUpload(null)
            void probeDuration(file).then((durationMs) =>
              save({
                blob: file,
                fileName: file.name,
                source: 'Upload',
                durationMs,
                purpose: purpose.trim() || DEFAULT_PURPOSE,
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

/** The room mic's take being recorded: everyone sees the room is being recorded, nobody hears it yet. */
function OpenTakeRow({ take }: { take: Recording }) {
  return (
    <li className="recording-row recording-live">
      <span className="recording-icon" aria-hidden="true">
        <Icon name="mic" size={18} />
      </span>
      <div className="recording-main">
        <span className="recording-name">Mic phòng đang ghi</span>
        <span
          className="recording-meta"
          tabIndex={0}
          data-tooltip="Tắt mic thì lượt này được nối vào bản ghi phòng và chuyển chữ lại cả bản."
        >
          <Badge tone="red">Đang ghi</Badge>
          <span>{take.createdByName}</span>
          <span>từ {formatDateTime(take.createdAt)}</span>
        </span>
      </div>
    </li>
  )
}

function RecordingRow({ recording, onErase }: { recording: Recording; onErase: () => void }) {
  const deleted = recording.status === 'Deleted'
  // Open from the start: what the machine wrote down is what people come to the tab for.
  const [open, setOpen] = useState(!deleted)
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
  const [lengthMs, setLengthMs] = useState(recording.durationMs)
  const [playing, setPlaying] = useState(false)
  const [speed, setSpeed] = useState(storedSpeed)
  const [volume, setVolume] = useState(1)
  const [muted, setMuted] = useState(false)
  const seek = (ms: number) => {
    const element = audio.current
    if (!element) return
    element.currentTime = Math.max(0, Math.min(lengthMs, ms)) / 1000
    setCurrentMs(element.currentTime * 1000)
  }
  const badge = deleted ? undefined : TRANSCRIPT_BADGE[transcriptStatus]

  // The player's signed link is fetched each time the row opens: one from an earlier opening may have expired. And
  // again when the file grows (a room-mic take appended, ADR-0041): the player would keep the shorter file it loaded.
  useEffect(() => {
    if (!open || deleted) return
    let gone = false
    void Promise.resolve().then(async () => {
      if (gone) return
      setPlayback({ kind: 'loading' })
      try {
        const url = (await getRecordingPlayback(recording.id)).url
        if (!gone) setPlayback({ kind: 'ready', url })
      } catch (error) {
        if (!gone) setPlayback({ kind: 'error', message: errorMessage(error) })
      }
    })
    return () => {
      gone = true
      setPlayback(null)
    }
  }, [open, deleted, recording.id, recording.byteSize])

  const toast = useToast()
  // A signed link to the original file, followed by the browser itself: a recording can be hundreds of megabytes.
  const download = async () => {
    try {
      const link = document.createElement('a')
      link.href = (await getRecordingDownload(recording.id)).url
      link.download = recording.fileName
      document.body.append(link)
      link.click()
      link.remove()
    } catch (error) {
      toast.show(errorMessage(error), 'error')
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
    <li className={`recording-row${deleted ? ' recording-deleted' : ''}${open ? ' recording-open' : ''}`}>
      <span className="recording-icon" aria-hidden="true">
        <Icon name="fileAudio" size={18} />
      </span>
      <div className="recording-main">
        <span className="recording-name" title={recording.fileName}>
          {recording.fileName}
        </span>
        <span className="recording-meta" data-tooltip={details} tabIndex={0}>
          <Badge tone="slate">{recording.source === 'BrowserCapture' ? 'Mic phòng' : 'Tải lên'}</Badge>
          {recording.takes && recording.takes.length > 1 && <span>{recording.takes.length} lượt ghi</span>}
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
            onClick={() => setOpen(!open)}
            aria-label={open ? 'Thu gọn' : 'Nghe và xem bản gỡ băng'}
            aria-expanded={open}
            data-tooltip={open ? 'Thu gọn' : 'Nghe và xem bản gỡ băng'}
          >
            <Icon name={open ? 'chevronUp' : 'chevronDown'} size={16} />
          </button>
          {recording.canDownload && (
            <button
              type="button"
              className="btn btn-outline btn-icon btn-sm"
              onClick={() => void download()}
              aria-label="Tải tệp ghi âm"
              data-tooltip="Tải tệp ghi âm"
            >
              <Icon name="download" size={16} />
            </button>
          )}
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
            preload="metadata"
            src={playback.url}
            onTimeUpdate={(e) => setCurrentMs(e.currentTarget.currentTime * 1000)}
            onLoadedMetadata={(e) => {
              const element = e.currentTarget
              if (Number.isFinite(element.duration)) setLengthMs(element.duration * 1000)
              element.playbackRate = speed
            }}
            onPlay={() => setPlaying(true)}
            onPause={() => setPlaying(false)}
          />
          <div className="recording-player-bar">
            <PlayerBar
              positionMs={currentMs}
              totalMs={lengthMs}
              playing={playing}
              onToggle={() => {
                const element = audio.current
                if (!element) return
                if (element.paused) void element.play().catch(() => toast.show('Không phát được tệp này.', 'error'))
                else element.pause()
              }}
              onSeek={seek}
              playLabel="Nghe cả tệp"
              speed={speed}
              onSpeed={(value) => {
                setSpeed(value)
                rememberSpeed(value)
                if (audio.current) audio.current.playbackRate = value
              }}
              volume={volume}
              onVolume={(value) => {
                setVolume(value)
                setMuted(false)
                if (audio.current) {
                  audio.current.volume = value
                  audio.current.muted = false
                }
              }}
              muted={muted}
              onMute={(value) => {
                setMuted(value)
                if (audio.current) audio.current.muted = value
              }}
            />
          </div>
          <TranscriptView
            fileName={recording.fileName}
            recordingId={recording.id}
            takes={recording.takes}
            listedStatus={recording.transcriptStatus}
            currentMs={currentMs}
            playing={playing}
            onPause={() => audio.current?.pause()}
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
