import { useState } from 'react'
import { Link } from 'react-router-dom'
import { deleteVoiceSample, grantProxyVoiceConsent, withdrawProxyVoiceConsent } from '../../api/voice'
import { Badge } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatDate, formatDateTime } from '../../lib/format'
import { routes } from '../../lib/routes'
import type { VoiceProfile } from '../../types/voice'
import { formatDuration } from '../recordings/format'
import { useSamplePlayer } from './samplePlayer'

/**
 * ADR-0060 — the samples meetings' Secretaries recorded for the person on the room's mic. A second set, under a
 * consent of its own that only the person gives: the Secretary records, the person decides. Everything taken is listed
 * with who took it and where, can be heard back and deleted, and withdrawing erases the whole set at once.
 */
export function ProxyTab({
  profile,
  onChange,
  onReload,
}: {
  profile: VoiceProfile
  onChange: (profile: VoiceProfile) => void
  onReload: () => Promise<void>
}) {
  const proxy = profile.proxy
  return proxy.consent ? (
    <ProxySamples profile={profile} onChange={onChange} onReload={onReload} />
  ) : (
    <section className="card voice-card" aria-labelledby="voice-proxy-title">
      <h2 id="voice-proxy-title" className="card-title">
        Cho phép Thư ký thu giọng mẫu hộ
      </h2>
      <ProxyConsentForm profile={profile} onGranted={onChange} />
    </section>
  )
}

function ProxyConsentForm({ profile, onGranted }: { profile: VoiceProfile; onGranted: (profile: VoiceProfile) => void }) {
  const toast = useToast()
  const [agreed, setAgreed] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const grant = async () => {
    setSaving(true)
    setError(null)
    try {
      const result = await grantProxyVoiceConsent(profile.proxy.noticeVersion)
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
          <strong>Mục đích:</strong> {profile.proxy.purpose}
        </li>
        <li>
          <strong>Ai thu:</strong> chỉ Thư ký của cuộc họp bạn dự, và chỉ người được quản trị viên giao quyền "Thu giọng
          mẫu hộ". Thư ký không đồng ý thay bạn được.
        </li>
        <li>
          <strong>Lưu gì:</strong> bản ghi giọng mẫu và đặc trưng giọng, cả hai đã mã hoá, tách riêng với giọng mẫu bạn tự
          thu. Mỗi lần thu bạn nhận một thông báo.
        </li>
        <li>
          <strong>Quyền của bạn:</strong> xem, nghe lại, xoá từng mẫu, rút đồng ý bất cứ lúc nào; rút lại thì mọi mẫu thu
          hộ bị xoá ngay, giọng mẫu tự thu giữ nguyên.
        </li>
        <li>
          <strong>Bên kiểm soát dữ liệu:</strong> {profile.controller}
        </li>
      </ul>
      <label className="consent-check">
        <input type="checkbox" checked={agreed} onChange={(e) => setAgreed(e.target.checked)} />
        <span>Tôi đồng ý cho Thư ký cuộc họp tôi dự thu giọng mẫu của tôi bằng micro phòng họp cho mục đích trên.</span>
      </label>
      {error && (
        <p className="field-error" role="alert">
          {error}
        </p>
      )}
      <div className="form-actions">
        <button type="button" className="btn btn-primary" disabled={!agreed || saving} onClick={() => void grant()}>
          {saving ? 'Đang lưu…' : 'Đồng ý'}
        </button>
      </div>
    </>
  )
}

function ProxySamples({
  profile,
  onChange,
  onReload,
}: {
  profile: VoiceProfile
  onChange: (profile: VoiceProfile) => void
  onReload: () => Promise<void>
}) {
  const toast = useToast()
  const proxy = profile.proxy
  const consent = proxy.consent!
  const player = useSamplePlayer()
  const [withdrawing, setWithdrawing] = useState(false)

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
    <section className="card voice-card" aria-labelledby="voice-proxy-title">
      <div className="voice-status">
        <h2 id="voice-proxy-title" className="card-title">
          Thư ký thu hộ
        </h2>
        <Badge tone="emerald">Đang cho phép</Badge>
        <span className="field-hint" data-tooltip={`Đồng ý lúc ${formatDateTime(consent.grantedAt)}`} tabIndex={0}>
          từ {formatDate(consent.grantedAt)}
        </span>
        <span className="voice-count">
          {proxy.samples.length}/{proxy.maxSamples}
        </span>
      </div>
      {proxy.samples.length === 0 ? (
        <p className="alert alert-info">Chưa có mẫu nào. Thư ký sẽ thu ở cuộc họp trực tiếp hoặc kết hợp bạn dự.</p>
      ) : (
        <ol className="voice-samples">
          {proxy.samples.map((sample, index) => (
            <li key={sample.id} className="voice-sample voice-sample-stacked">
              <span className="voice-sample-text">
                <span className="voice-sample-name">
                  <Link to={routes.meeting(sample.meetingId)}>{sample.meetingTitle}</Link>
                </span>
                <span
                  className="field-hint"
                  data-tooltip={`${formatDuration(sample.speechMs)} giọng nói, thu lúc ${formatDateTime(sample.createdAt)}`}
                  tabIndex={0}
                >
                  {sample.collectedByName} · {formatDate(sample.createdAt)}
                </span>
              </span>
              {sample.playable && (
                <button
                  type="button"
                  className="btn btn-ghost btn-icon btn-sm"
                  onClick={() =>
                    void player.toggle(sample.id).catch((error) => toast.show(errorMessage(error), 'error'))
                  }
                  aria-label={`${player.playing === sample.id ? 'Dừng' : 'Nghe lại'} mẫu thu hộ ${index + 1}`}
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
                aria-label={`Xoá mẫu thu hộ ${index + 1}`}
                data-tooltip="Xoá mẫu"
              >
                <Icon name="trash" size={16} />
              </button>
            </li>
          ))}
        </ol>
      )}
      <div className="form-actions voice-actions">
        <button type="button" className="btn btn-danger" onClick={() => setWithdrawing(true)}>
          Rút đồng ý và xoá mẫu thu hộ
        </button>
      </div>

      {withdrawing && (
        <ConfirmDialog
          title="Rút đồng ý thu hộ?"
          confirmLabel="Rút và xoá"
          pendingLabel="Đang xoá…"
          cancelLabel="Giữ lại"
          tone="danger"
          onClose={() => setWithdrawing(false)}
          onConfirm={async () => {
            player.stop()
            const result = await withdrawProxyVoiceConsent()
            toast.show(result.message, 'success')
            setWithdrawing(false)
            onChange(result.data)
          }}
        >
          <p>
            {proxy.samples.length} mẫu Thư ký đã thu bị xoá ngay, Thư ký không thu thêm được nữa. Giọng mẫu tự thu giữ
            nguyên. Bạn có thể cho phép lại sau.
          </p>
        </ConfirmDialog>
      )}
    </section>
  )
}
