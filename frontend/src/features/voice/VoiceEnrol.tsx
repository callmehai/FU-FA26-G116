import { useCallback, useEffect, useState } from 'react'
import { addVoiceSample, grantVoiceConsent } from '../../api/voice'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { VoiceProfile } from '../../types/voice'
import { formatDuration } from '../recordings/format'
import { useRecorder } from '../recordings/useRecorder'

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
      <ul className="voice-notice">
        <li>
          <strong>Mục đích:</strong> {profile.purpose}
        </li>
        <li>
          <strong>Lưu gì:</strong> bản ghi giọng mẫu bạn đọc và đặc trưng giọng (một dãy số), cả hai đã mã hoá. Chỉ bạn
          nghe lại được bản ghi mẫu.
        </li>
        <li>
          <strong>Dùng ở đâu:</strong> chỉ so với giọng trong bản ghi các cuộc họp bạn tham dự. Máy chỉ gợi ý, Thư ký
          quyết định tên.
        </li>
        <li>
          <strong>Quyền của bạn:</strong> xem và xoá từng giọng mẫu, rút lại đồng ý bất cứ lúc nào ở trang "Giọng nói
          của tôi"; rút lại thì giọng mẫu và mọi gợi ý bị xoá ngay.
        </li>
        <li>
          <strong>Bên kiểm soát dữ liệu:</strong> {profile.controller}
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

export function RecordSampleDialog({
  passage,
  onClose,
  onSaved,
  onStale,
}: {
  passage: string
  onClose: () => void
  onSaved: (message: string) => Promise<void>
  onStale: () => Promise<void>
}) {
  const recorder = useRecorder()
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const elapsed = recorder.state.kind === 'recording' || recorder.state.kind === 'paused' ? recorder.state.elapsedMs : 0
  const { stop } = recorder

  const finish = useCallback(async () => {
    const take = await stop()
    if (!take) return
    setSaving(true)
    try {
      const result = await addVoiceSample(take.blob, `giong-mau.${take.extension}`)
      await onSaved(result.message)
    } catch (failure) {
      if (isApiError(failure, 409)) void onStale()
      setError(errorMessage(failure))
    } finally {
      setSaving(false)
    }
  }, [stop, onSaved, onStale])

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
      title="Ghi giọng mẫu"
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
            {elapsed < MIN_SAMPLE_MS
              ? `Đọc tiếp… (${Math.ceil((MIN_SAMPLE_MS - elapsed) / 1000)} giây)`
              : 'Dừng và lưu'}
          </button>
        ) : (
          <button
            type="button"
            className="btn btn-primary"
            disabled={saving || recorder.state.kind === 'starting'}
            onClick={() => {
              setError(null)
              void recorder.start()
            }}
          >
            <Icon name="mic" size={18} />
            {saving ? 'Đang kiểm tra…' : error ? 'Ghi lại' : 'Bắt đầu ghi'}
          </button>
        )
      }
    >
      <p className="field-hint">Ngồi ở nơi yên tĩnh, nói tự nhiên như khi phát biểu, đọc đoạn văn sau:</p>
      <blockquote className="voice-passage">{passage}</blockquote>
      {active && (
        <p className="voice-recording" role="status">
          <span className="recorder-dot" aria-hidden="true" />
          Đang ghi {formatDuration(elapsed)}
        </p>
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
