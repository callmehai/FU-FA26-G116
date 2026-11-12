import { useCallback, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { deleteVoiceSample, getMyVoice, grantVoiceConsent, withdrawVoiceConsent } from '../../api/voice'
import { Badge } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatDate, formatDateTime } from '../../lib/format'
import type { VoiceProfile } from '../../types/voice'
import { formatDuration } from '../recordings/format'
import { PASSAGES } from './passages'
import { ProxyTab } from './ProxyVoice'
import { useSamplePlayer } from './samplePlayer'
import { ConsentForm, RecordSampleDialog } from './VoiceEnrol'

/**
 * SCRUM-35 — the signed-in person's own Voice ID. A voice used to recognise someone is biometric data
 * (Luật BVDLCN 91/2025/QH15), so the page says plainly what is kept, where it is used and how to take
 * it back before asking, and turning it off erases everything at once.
 */

type State = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; profile: VoiceProfile }

export function VoicePage() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const generation = useRef(0)
  // ADR-0060: two sets, each under its own consent. A "Thư ký đã thu giọng mẫu" notice opens ?tab=proxy.
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') === 'proxy' ? 'proxy' : 'self'
  const openTab = (next: 'self' | 'proxy') => setParams(next === 'proxy' ? { tab: 'proxy' } : {}, { replace: true })
  const change = (profile: VoiceProfile) => setState({ kind: 'loaded', profile })

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
      {state.kind === 'loaded' && (
        <>
          <div className="tabs" role="tablist" aria-label="Bộ giọng mẫu">
            <button
              type="button"
              role="tab"
              aria-selected={tab === 'self'}
              className={`tab${tab === 'self' ? ' tab-active' : ''}`}
              onClick={() => openTab('self')}
            >
              Tự thu
              <span className="tab-count">{state.profile.samples.length}</span>
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={tab === 'proxy'}
              className={`tab${tab === 'proxy' ? ' tab-active' : ''}`}
              onClick={() => openTab('proxy')}
            >
              Thư ký thu hộ
              <span className="tab-count">{state.profile.proxy.samples.length}</span>
            </button>
          </div>
          {tab === 'proxy' ? (
            <ProxyTab profile={state.profile} onChange={change} onReload={load} />
          ) : state.profile.consent ? (
            <Enrolled profile={state.profile} onChange={change} onReload={load} />
          ) : (
            <ConsentCard profile={state.profile} onChange={change} />
          )}
        </>
      )}
    </div>
  )
}

function ConsentCard({ profile, onChange }: { profile: VoiceProfile; onChange: (profile: VoiceProfile) => void }) {
  return (
    <section className="card voice-card" aria-labelledby="voice-consent-title">
      <h2 id="voice-consent-title" className="card-title">
        Bật nhận diện giọng nói
      </h2>
      <ConsentForm profile={profile} onGranted={onChange} />
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
  const player = useSamplePlayer()

  const remove = async (sampleId: string) => {
    player.stop()
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
      {profile.consent!.noticeVersion !== profile.noticeVersion && (
        <RenewNotice profile={profile} onChange={onChange} />
      )}
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
              {sample.playable && (
                <button
                  type="button"
                  className="btn btn-ghost btn-icon btn-sm"
                  onClick={() =>
                    void player.toggle(sample.id).catch((error) => toast.show(errorMessage(error), 'error'))
                  }
                  aria-label={`${player.playing === sample.id ? 'Dừng' : 'Nghe lại'} giọng mẫu ${index + 1}`}
                  aria-pressed={player.playing === sample.id}
                  data-tooltip={player.playing === sample.id ? 'Dừng' : 'Nghe lại'}
                >
                  <Icon name={player.playing === sample.id ? 'pause' : 'play'} size={16} />
                </button>
              )}
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
            Toàn bộ {profile.samples.length} giọng mẫu tự thu, mọi gợi ý và tên máy đã điền từ giọng của bạn bị xoá ngay.
            Tên Thư ký đã gán và giọng mẫu Thư ký thu hộ giữ nguyên. Bạn có thể bật lại sau.
          </p>
        </ConfirmDialog>
      )}
    </section>
  )
}

/**
 * The notice changed since the person agreed: 1.1 keeps the sample recording for its owner to hear back (ADR-0042), 1.2
 * lets the machine fill in their name on a sure match (ADR-0048). Until they renew, nothing new applies to them: the
 * next samples are not kept as recordings, and the machine only suggests their name.
 */
function RenewNotice({ profile, onChange }: { profile: VoiceProfile; onChange: (profile: VoiceProfile) => void }) {
  const toast = useToast()
  const [saving, setSaving] = useState(false)
  const renew = async () => {
    setSaving(true)
    try {
      const result = await grantVoiceConsent(profile.noticeVersion)
      toast.show(result.message, 'success')
      onChange(result.data)
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      setSaving(false)
    }
  }
  const agreed = profile.consent!.noticeVersion
  const changes = [
    agreed < '1.1' && 'giọng mẫu ghi từ nay được lưu cả bản ghi (đã mã hoá) để bạn nghe lại',
    agreed < '1.2' && 'máy tự điền tên bạn khi giọng khớp rõ, Thư ký vẫn sửa được',
  ].filter(Boolean)
  return (
    <div className="alert alert-info voice-renew">
      <p>Điều khoản mới: {changes.join('; ')}. Chưa đồng ý thì mọi thứ giữ như cũ.</p>
      <button type="button" className="btn btn-outline btn-sm" disabled={saving} onClick={() => void renew()}>
        {saving ? 'Đang lưu…' : 'Đồng ý điều khoản mới'}
      </button>
    </div>
  )
}
