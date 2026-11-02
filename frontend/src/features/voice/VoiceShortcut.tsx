import { useCallback, useEffect, useRef, useState } from 'react'
import { getMyVoice } from '../../api/voice'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import type { VoiceProfile } from '../../types/voice'
import { PASSAGES } from './passages'
import { ConsentForm, RecordSampleDialog } from './VoiceEnrol'

/**
 * SCRUM-35 — a Voice ID shortcut for the call lobby: someone about to join, with no sample yet, records one there and
 * then, without looking for "Giọng nói của tôi". One line and one button; why it helps is in the tooltip. Agreeing
 * still shows the whole notice first. Says nothing once a sample exists, or when the profile cannot be read: the
 * lobby is about joining, and Voice ID is optional.
 */
export function VoiceShortcut() {
  const toast = useToast()
  const [profile, setProfile] = useState<VoiceProfile | null>(null)
  const [step, setStep] = useState<'consent' | 'record' | null>(null)
  const loads = useRef(0)

  const load = useCallback(async () => {
    const mine = ++loads.current
    try {
      const next = await getMyVoice()
      if (mine === loads.current) setProfile(next)
    } catch {
      // Unreadable profile: no shortcut, the lobby goes on.
    }
  }, [])

  useEffect(() => {
    void Promise.resolve().then(load)
    const current = loads
    return () => {
      current.current++
    }
  }, [load])

  if (!profile || (profile.consent && profile.samples.length > 0)) return null

  return (
    <>
      <p
        className="prejoin-sample"
        tabIndex={0}
        data-tooltip="Giọng mẫu giúp bản gỡ băng tự nhận ra lời bạn. Đọc một đoạn văn khoảng 20 giây, chỉ bạn nghe lại được; xoá được bất cứ lúc nào."
      >
        <Icon name="mic" size={16} />
        Chưa có giọng mẫu
        <button type="button" className="prejoin-sample-action" onClick={() => setStep(profile.consent ? 'record' : 'consent')}>
          Ghi ngay
        </button>
      </p>

      {step === 'consent' && (
        <Dialog title="Bật nhận diện giọng nói" size="lg" onClose={() => setStep(null)}>
          <ConsentForm
            profile={profile}
            submitLabel="Đồng ý và ghi giọng mẫu"
            onGranted={(next) => {
              setProfile(next)
              setStep('record')
            }}
          />
        </Dialog>
      )}

      {step === 'record' && (
        <RecordSampleDialog
          passage={PASSAGES[profile.samples.length % PASSAGES.length]}
          onClose={() => setStep(null)}
          onSaved={async (message) => {
            setStep(null)
            toast.show(message, 'success')
            await load()
          }}
          onStale={load}
        />
      )}
    </>
  )
}
