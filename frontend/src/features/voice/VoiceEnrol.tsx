import { useCallback, useEffect, useRef, useState } from 'react'
import { addVoiceSample, grantVoiceConsent } from '../../api/voice'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { VoiceProfile } from '../../types/voice'
import { formatDuration } from '../recordings/format'
import { type RecordedTake, useRecorder } from '../recordings/useRecorder'

/**
 * SCRUM-35 — agreeing to Voice ID and recording a sample, shared by "Giọng nói của tôi" and the call lobby's shortcut
 * (`VoiceShortcut`). The notice is shown in full before agreeing, wherever that happens: consent to biometric data is
 * valid only for what the person was told (Luật BVDLCN 91/2025/QH15).
 */

const MIN_SAMPLE_MS = 8000
const MAX_SAMPLE_MS = 30000

/** What is kept, where it is used and how to take it back, then the box to tick. */
export function ConsentForm({
  profile,
  onGranted,
  submitLabel = 'Đồng ý và bật',
}: {
  profile: VoiceProfile
  onGranted: (profile: VoiceProfile) => void
  submitLabel?: string
}) {
  const toast = useToast()
  const [agreed, setAgreed] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const grant = async () => {
    setSaving(true)
    setError(null)
    try {
      const result = await grantVoiceConsent(profile.noticeVersion)
      toast.show(result.message, 'success')
      onGranted(result.data)
    } catch (failure) {
      setError(errorMessage(failure))
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      {/* In the first person throughout, as the purpose the server sends and the box ticked under it are. */}
      <ul className="voice-notice">
        <li>
          <strong>Mục đích:</strong> {profile.purpose}
        </li>
        <li>
          <strong>Ai thu:</strong> tôi tự thu trên máy mình, hoặc người được quản trị viên giao quyền "Thu giọng mẫu hộ"
          thu, thường bằng micro phòng họp. Người thu hỏi ý tôi trước mỗi lần thu, và tôi nhận một thông báo sau đó.
        </li>
        <li>
          <strong>Lưu gì:</strong> bản ghi giọng mẫu và đặc trưng giọng (một dãy số), cả hai đã mã hoá. Chỉ tôi nghe lại
          được mọi mẫu; người có quyền "Thu giọng mẫu hộ" nghe được mẫu thu hộ, mỗi lần nghe đều ghi nhật ký.
        </li>
        <li>
          <strong>Dùng ở đâu:</strong> chỉ so với giọng trong bản ghi các cuộc họp tôi tham dự. Máy gợi ý tên người nói,
          và tự điền tên tôi khi giọng khớp rõ; Thư ký xem và sửa được.
        </li>
        <li>
          <strong>Quyền của tôi:</strong> xem, nghe và xoá từng giọng mẫu, rút lại đồng ý bất cứ lúc nào ở trang "Giọng
          nói của tôi"; rút lại thì mọi giọng mẫu (cả mẫu thu hộ), gợi ý và tên máy đã điền bị xoá ngay.
        </li>
        <li>
          <strong>Bên kiểm soát dữ liệu:</strong> {profile.controller}
          {/[.!?]$/.test(profile.controller) ? '' : '.'}
        </li>
      </ul>
      <p className="field-hint">
        Giọng nói dùng để nhận ra bạn là dữ liệu sinh trắc học, thuộc dữ liệu cá nhân nhạy cảm (Luật Bảo vệ dữ liệu cá
        nhân 2025). Không bật thì bạn vẫn dùng được mọi chức năng khác.
      </p>
      <label className="consent-check">
        <input type="checkbox" checked={agreed} onChange={(e) => setAgreed(e.target.checked)} />
        <span>Tôi đã đọc và đồng ý cho hệ thống lưu đặc trưng giọng nói của tôi cho mục đích trên.</span>
      </label>
      {error && (
        <p className="field-error" role="alert">
          {error}
        </p>
      )}
      <div className="form-actions">
        <button type="button" className="btn btn-primary" disabled={!agreed || saving} onClick={() => void grant()}>
          {saving ? 'Đang lưu…' : submitLabel}
        </button>
      </div>
    </>
  )
}

/**
 * Read the passage, stop, hear it back, then keep it or read it again — as AI-eCabinet does. Nothing leaves the browser
 * until the person keeps the take.
 */
export function RecordSampleDialog({
  passage,
  onClose,
  onSaved,
  onStale,
  title = 'Ghi giọng mẫu',
  hint = 'Ngồi ở nơi yên tĩnh, nói tự nhiên như khi phát biểu, đọc đoạn văn sau:',
  upload = addVoiceSample,
  askConsentOf,
}: {
  passage: string
  onClose: () => void
  onSaved: (message: string) => Promise<void>
  onStale: () => Promise<void>
  title?: string
  hint?: string
  /**
   * Where the kept take goes: the caller's own set by default; a collector recording someone else passes its own
   * (ADR-0060), which receives whether the person agreed to this take (ADR-0073).
   */
  upload?: (audio: Blob, fileName: string, personAgreed: boolean) => Promise<{ message: string }>
  /**
   * ADR-0073 — recording someone else: their name. Nothing can be recorded until the collector ticks that this person,
   * there and then, agreed to this take; the tick goes with the upload and the server refuses a sample without it.
   */
  askConsentOf?: string
}) {
  const recorder = useRecorder()
  const [agreed, setAgreed] = useState(false)
  const mayRecord = askConsentOf === undefined || agreed
  const [take, setTake] = useState<{ file: RecordedTake; url: string } | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const elapsed = recorder.state.kind === 'recording' || recorder.state.kind === 'paused' ? recorder.state.elapsedMs : 0
  const { stop } = recorder

  // The take plays from a blob: URL, freed when it is replaced and when the dialog closes.
  const shown = useRef<string | null>(null)
  const show = useCallback((file: RecordedTake | null) => {
    if (shown.current) URL.revokeObjectURL(shown.current)
    shown.current = file ? URL.createObjectURL(file.blob) : null
    setTake(file && shown.current ? { file, url: shown.current } : null)
  }, [])
  useEffect(
    () => () => {
      if (shown.current) URL.revokeObjectURL(shown.current)
    },
    [],
  )

  const finish = useCallback(async () => {
    const file = await stop()
    if (file) show(file)
  }, [stop, show])

  const record = () => {
    show(null)
    setError(null)
    void recorder.start()
  }

  const save = async () => {
    if (!take) return
    setSaving(true)
    setError(null)
    try {
      const result = await upload(take.file.blob, `giong-mau.${take.file.extension}`, mayRecord)
      await onSaved(result.message)
    } catch (failure) {
      if (isApiError(failure, 409)) void onStale()
      setError(errorMessage(failure))
    } finally {
      setSaving(false)
    }
  }

  // Thirty seconds is plenty for one passage; stop for the person rather than record silence.
  // Re-armed with the time left whenever recording resumes.
  const recordingKind = recorder.state.kind
  const { elapsedMs } = recorder
  useEffect(() => {
    if (recordingKind !== 'recording') return
    const timer = window.setTimeout(() => void finish(), Math.max(0, MAX_SAMPLE_MS - elapsedMs()))
    return () => window.clearTimeout(timer)
  }, [recordingKind, finish, elapsedMs])

  const active = recorder.state.kind === 'recording' || recorder.state.kind === 'paused'
  return (
    <Dialog
      title={title}
      size="lg"
      onClose={() => {
        recorder.discard()
        onClose()
      }}
      footer={
        active ? (
          <button
            type="button"
            className="btn btn-primary"
            disabled={elapsed < MIN_SAMPLE_MS}
            onClick={() => void finish()}
          >
            {elapsed >= MIN_SAMPLE_MS && <Icon name="recordStop" size={18} />}
            {elapsed < MIN_SAMPLE_MS ? `Đọc tiếp… (${Math.ceil((MIN_SAMPLE_MS - elapsed) / 1000)} giây)` : 'Dừng'}
          </button>
        ) : take ? (
          <>
            <button type="button" className="btn btn-outline" disabled={saving || !mayRecord} onClick={record}>
              <Icon name="undo" size={18} />
              Ghi lại
            </button>
            <button type="button" className="btn btn-primary" disabled={saving || !mayRecord} onClick={() => void save()}>
              <Icon name="check" size={18} />
              {saving ? 'Đang kiểm tra…' : 'Lưu giọng mẫu'}
            </button>
          </>
        ) : (
          <button
            type="button"
            className="btn btn-primary"
            disabled={recorder.state.kind === 'starting' || !mayRecord}
            onClick={record}
          >
            <Icon name="mic" size={18} />
            Bắt đầu ghi
          </button>
        )
      }
    >
      {askConsentOf !== undefined && (
        <label className="consent-check voice-ask">
          <input
            type="checkbox"
            checked={agreed}
            disabled={active || saving}
            onChange={(event) => setAgreed(event.target.checked)}
          />
          <span>
            Đã hỏi, {askConsentOf} đồng ý cho thu giọng mẫu lần này
            <InfoTip text="Hỏi ý người được thu ngay trước khi ghi. Họ nhận thông báo sau mỗi lần thu, nghe lại và xoá được mẫu ở trang Giọng nói của tôi." />
          </span>
        </label>
      )}
      <p className="field-hint">{hint}</p>
      <blockquote className="voice-passage">{passage}</blockquote>
      {/* Kept in place before and after recording, so the dialog does not grow and jump when "Bắt đầu ghi" is pressed. */}
      {!take && (
        <p className="voice-recording" role="status">
          {active && (
            <>
              <span className="recorder-dot" aria-hidden="true" />
              Đang ghi {formatDuration(elapsed)}
            </>
          )}
        </p>
      )}
      {take && !active && (
        <div className="voice-review">
          <p className="field-hint" role="status">
            Nghe lại trước khi lưu. Có tạp âm hay đọc vấp thì ghi lại.
          </p>
          <audio
            controls
            controlsList="nodownload"
            src={take.url}
            aria-label="Nghe lại giọng mẫu vừa ghi"
            onLoadedMetadata={(event) => {
              // Chrome records WebM without its length, so the player shows none; one seek to the end makes it count.
              const player = event.currentTarget
              if (player.duration !== Infinity) return
              const rewind = () => {
                player.currentTime = 0
              }
              player.addEventListener('timeupdate', rewind, { once: true })
              player.currentTime = Number.MAX_SAFE_INTEGER
            }}
          />
        </div>
      )}
      {recorder.state.kind === 'error' && (
        <p className="field-error" role="alert">
          {recorder.state.message}
        </p>
      )}
      {error && (
        <p className="alert alert-error" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  )
}
