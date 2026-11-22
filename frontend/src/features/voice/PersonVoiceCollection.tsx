import { useCallback, useEffect, useRef, useState } from 'react'
import {
  collectPersonVoiceSample,
  deletePersonSample,
  getPersonVoiceCollection,
  personSampleAudio,
} from '../../api/voice'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatDate, formatDateTime } from '../../lib/format'
import type { PersonVoiceCollection as Collection, ProxySample } from '../../types/voice'
import { formatDuration } from '../recordings/format'
import { useAccessLost } from '../auth/useAccessLost'
import { PASSAGES } from './passages'
import { useSamplePlayer } from './samplePlayer'
import { RecordSampleDialog } from './VoiceEnrol'

type State = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; data: Collection }

/**
 * ADR-0071 — a holder of "Thu giọng mẫu hộ" records one person's Voice ID samples at any time, with no meeting, and sees,
 * hears and deletes every sample anyone took for them. Only for a person whose own consent is to notice 1.4 or later;
 * the server rechecks the capability and the consent on every call.
 */
export function PersonVoiceCollection({ userId }: { userId: string }) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [recording, setRecording] = useState(false)
  const [erasing, setErasing] = useState<ProxySample | null>(null)
  const player = useSamplePlayer((sampleId) => personSampleAudio(userId, sampleId))

  const generation = useRef(0)
  const load = useCallback(async () => {
    const mine = generation.current
    try {
      const data = await getPersonVoiceCollection(userId)
      if (mine === generation.current) setState({ kind: 'loaded', data })
    } catch (error) {
      if (mine !== generation.current || handleAccessLost(error)) return
      setState({ kind: 'error', message: errorMessage(error) })
    }
  }, [userId, handleAccessLost])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  if (state.kind === 'loading') return <p className="field-hint">Đang tải…</p>
  if (state.kind === 'error')
    return (
      <div className="alert alert-error" role="alert">
        <p>{state.message}</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
          <Icon name="refresh" size={16} />
          Thử lại
        </button>
      </div>
    )

  const { data } = state
  return (
    <div className="voice-collect-for">
      <div className="voice-person-head">
        <span
          className="voice-person-count"
          data-tooltip="Mọi mẫu thu hộ của người này"
          tabIndex={0}
        >
          {data.samples.length}/{data.maxSamples} mẫu thu hộ
        </span>
        {data.canCollect && (
          <button type="button" className="btn btn-primary btn-sm" onClick={() => setRecording(true)}>
            <Icon name="mic" size={16} />
            Thu
          </button>
        )}
      </div>
      {!data.consented && (
        <p className="alert alert-info">
          Người này chưa đồng ý điều khoản Voice ID mới. Họ bật ở trang Giọng nói của tôi.
        </p>
      )}
      {data.samples.length > 0 && (
        <ol className="voice-samples">
          {data.samples.map((sample, index) => (
            <li key={sample.id} className="voice-sample">
              <span className="voice-sample-text">
                <span className="voice-sample-name">{sample.meetingTitle ?? 'Ngoài cuộc họp'}</span>
                <span
                  className="field-hint"
                  data-tooltip={`${formatDuration(sample.speechMs)} giọng nói, ${sample.collectedByName} thu lúc ${formatDateTime(sample.createdAt)}`}
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
                  aria-label={`${player.playing === sample.id ? 'Dừng' : 'Nghe lại'} mẫu ${index + 1} của ${data.fullName}`}
                  aria-pressed={player.playing === sample.id}
                  data-tooltip={player.playing === sample.id ? 'Dừng' : 'Nghe lại'}
                >
                  <Icon name={player.playing === sample.id ? 'pause' : 'play'} size={16} />
                </button>
              )}
              <button
                type="button"
                className="btn btn-ghost btn-icon btn-sm"
                onClick={() => setErasing(sample)}
                aria-label={`Xoá mẫu ${index + 1} của ${data.fullName}`}
                data-tooltip="Xoá mẫu"
              >
                <Icon name="trash" size={16} />
              </button>
            </li>
          ))}
        </ol>
      )}

      {recording && (
        <RecordSampleDialog
          title={`Thu giọng mẫu: ${data.fullName}`}
          hint="Nên dùng micro phòng họp. Mời người này ngồi chỗ quen thuộc, nói tự nhiên như khi phát biểu, đọc đoạn văn sau:"
          passage={PASSAGES[data.samples.length % PASSAGES.length]}
          askConsentOf={data.fullName}
          upload={(audio, fileName, personAgreed) => collectPersonVoiceSample(userId, audio, fileName, personAgreed)}
          onClose={() => setRecording(false)}
          onSaved={async (message) => {
            setRecording(false)
            toast.show(message, 'success')
            await load()
          }}
          onStale={load}
        />
      )}

      {erasing && (
        <ConfirmDialog
          title="Xoá mẫu thu hộ?"
          confirmLabel="Xoá"
          pendingLabel="Đang xoá…"
          cancelLabel="Giữ lại"
          tone="danger"
          onClose={() => setErasing(null)}
          onConfirm={async () => {
            player.stop()
            try {
              const result = await deletePersonSample(userId, erasing.id)
              toast.show(result.message, 'success')
            } finally {
              setErasing(null)
              await load()
            }
          }}
        >
          <p>Mẫu này của {data.fullName} bị xoá hẳn. Muốn thay thì thu lại mẫu mới.</p>
        </ConfirmDialog>
      )}
    </div>
  )
}
