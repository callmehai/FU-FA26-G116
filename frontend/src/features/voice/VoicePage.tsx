import { useCallback, useEffect, useRef, useState } from 'react'
import { addVoiceSample, deleteVoiceSample, getMyVoice, grantVoiceConsent, withdrawVoiceConsent } from '../../api/voice'
import { Badge } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDate, formatDateTime } from '../../lib/format'
import type { VoiceProfile } from '../../types/voice'
import { formatDuration } from '../recordings/format'
import { useRecorder } from '../recordings/useRecorder'
import { PASSAGES } from './passages'

/**
 * SCRUM-35 — the signed-in person's own Voice ID. A voice used to recognise someone is biometric data
 * (Luật BVDLCN 91/2025/QH15), so the page says plainly what is kept, where it is used and how to take
 * it back before asking, and turning it off erases everything at once.
 */

const MIN_SAMPLE_MS = 8000
const MAX_SAMPLE_MS = 30000

type State = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; profile: VoiceProfile }

export function VoicePage() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const generation = useRef(0)

  const load = useCallback(async () => {
    const mine = generation.current
    try {
      const profile = await getMyVoice()
      if (mine === generation.current) setState({ kind: 'loaded', profile })
    } catch (error) {
      if (mine === generation.current) setState({ kind: 'error', message: errorMessage(error) })
    }
  }, [])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  return (
    <div className="form-page voice-page">
      <PageHeader
        title="Giọng nói của tôi"
        description="Voice ID giúp Thư ký biết ai đang phát biểu trong bản gỡ băng của cuộc họp bạn dự."
      />
      {state.kind === 'loading' && <div className="card voice-card voice-skeleton" aria-busy="true" />}
      {state.kind === 'error' && (
        <div className="alert alert-error" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}
      {state.kind === 'loaded' &&
        (state.profile.consent ? (
          <Enrolled
            profile={state.profile}
            onChange={(profile) => setState({ kind: 'loaded', profile })}
            onReload={load}
          />
        ) : (
          <ConsentCard profile={state.profile} onChange={(profile) => setState({ kind: 'loaded', profile })} />
        ))}
    </div>
  )
}

function ConsentCard({ profile, onChange }: { profile: VoiceProfile; onChange: (profile: VoiceProfile) => void }) {
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
      onChange(result.data)
    } catch (failure) {
      setError(errorMessage(failure))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="card voice-card" aria-labelledby="voice-consent-title">
      <h2 id="voice-consent-title" className="card-title">
        Bật nhận diện giọng nói
      </h2>
      <ul className="voice-notice">
        <li>
          <strong>Mục đích:</strong> {profile.purpose}
        </li>
        <li>
          <strong>Lưu gì:</strong> chỉ đặc trưng giọng (một dãy số) đã mã hoá — không lưu âm thanh bạn ghi.
        </li>
        <li>
          <strong>Dùng ở đâu:</strong> chỉ so với giọng trong bản ghi các cuộc họp bạn tham dự. Máy chỉ gợi ý, Thư ký
          quyết định tên.
        </li>
        <li>
          <strong>Quyền của bạn:</strong> xem và xoá từng giọng mẫu, rút lại đồng ý bất cứ lúc nào ngay trên trang này;
          rút lại thì giọng mẫu và mọi gợi ý bị xoá ngay.
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
          {saving ? 'Đang lưu…' : 'Đồng ý và bật'}
        </button>
      </div>
    </section>
  )
}

function Enrolled({
  profile,
  onChange,
  onReload,
}: {
  profile: VoiceProfile
  onChange: (profile: VoiceProfile) => void
  onReload: () => Promise<void>
}) {
  const toast = useToast()
  const [recording, setRecording] = useState(false)
  const [withdrawing, setWithdrawing] = useState(false)
  const full = profile.samples.length >= profile.maxSamples

  const remove = async (sampleId: string) => {
    try {
      const result = await deleteVoiceSample(sampleId)
      toast.show(result.message, 'success')
      onChange(result.data)
    } catch (error) {
      toast.show(errorMessage(error), 'error')
      await onReload()
    }
  }

  return (
    <section className="card voice-card" aria-labelledby="voice-enrolled-title">
      <div className="voice-status">
        <h2 id="voice-enrolled-title" className="card-title">
          Giọng mẫu
        </h2>
        <Badge tone="emerald">Đang bật</Badge>
        <span
          className="field-hint"
          data-tooltip={`Đồng ý lúc ${formatDateTime(profile.consent!.grantedAt)}`}
          tabIndex={0}
        >
          từ {formatDate(profile.consent!.grantedAt)}
        </span>
        <span className="voice-count">
          {profile.samples.length}/{profile.maxSamples}
        </span>
      </div>
      {profile.samples.length < 2 && (
        <p className="alert alert-info">
          {profile.samples.length === 0
            ? 'Chưa có giọng mẫu nào — hãy ghi ít nhất 2 mẫu để hệ thống nhận ra bạn.'
            : 'Ghi thêm một mẫu nữa (ở chỗ khác, giọng tự nhiên) để nhận diện chắc hơn.'}
        </p>
      )}
      {profile.samples.length > 0 && (
        <ol className="voice-samples">
          {profile.samples.map((sample, index) => (
            <li key={sample.id} className="voice-sample">
              <Icon name="mic" size={16} />
              <span className="voice-sample-text">
                <span className="voice-sample-name">Giọng mẫu {index + 1}</span>
                <span className="field-hint">
                  {formatDuration(sample.speechMs)} · {formatDate(sample.createdAt)}
                </span>
              </span>
              <button
                type="button"
                className="btn btn-ghost btn-icon btn-sm"
                onClick={() => void remove(sample.id)}
                aria-label={`Xoá giọng mẫu ${index + 1}`}
                data-tooltip="Xoá giọng mẫu"
              >
                <Icon name="trash" size={16} />
              </button>
            </li>
          ))}
        </ol>
      )}
      <div className="form-actions voice-actions">
        <button type="button" className="btn btn-danger" onClick={() => setWithdrawing(true)}>
          Tắt và xoá giọng mẫu
        </button>
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => setRecording(true)}
          disabled={full}
          data-tooltip={full ? `Đã đủ ${profile.maxSamples} giọng mẫu — xoá bớt để ghi lại` : undefined}
        >
          <Icon name="mic" size={18} />
          Ghi giọng mẫu
        </button>
      </div>

      {recording && (
        <RecordSampleDialog
          passage={PASSAGES[profile.samples.length % PASSAGES.length]}
          onClose={() => setRecording(false)}
          onSaved={async (message) => {
            setRecording(false)
            toast.show(message, 'success')
            await onReload()
          }}
          onStale={onReload}
        />
      )}

      {withdrawing && (
        <ConfirmDialog
          title="Tắt nhận diện giọng nói?"
          confirmLabel="Tắt và xoá"
          pendingLabel="Đang xoá…"
          cancelLabel="Giữ lại"
          tone="danger"
          onClose={() => setWithdrawing(false)}
          onConfirm={async () => {
            const result = await withdrawVoiceConsent()
            toast.show(result.message, 'success')
            setWithdrawing(false)
            onChange(result.data)
          }}
        >
          <p>
            Toàn bộ {profile.samples.length} giọng mẫu và mọi gợi ý tên từ giọng của bạn bị xoá ngay. Tên Thư ký đã gán
            trong biên bản cũ giữ nguyên. Bạn có thể bật lại sau.
          </p>
        </ConfirmDialog>
      )}
    </section>
  )
}

function RecordSampleDialog({
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
            {elapsed >= MIN_SAMPLE_MS && <Icon name="stop" size={18} />}
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
