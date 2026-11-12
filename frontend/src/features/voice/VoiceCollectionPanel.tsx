import { useCallback, useEffect, useRef, useState } from 'react'
import { collectedSampleAudio, collectVoiceSample, deleteCollectedSample, getVoiceCollection } from '../../api/voice'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatDate, formatDateTime } from '../../lib/format'
import type { CollectedSample, VoiceCollection, VoiceCollectionPerson } from '../../types/voice'
import { formatDuration } from '../recordings/format'
import { useAccessLost } from '../auth/useAccessLost'
import { PASSAGES } from './passages'
import { useSamplePlayer } from './samplePlayer'
import { RecordSampleDialog } from './VoiceEnrol'

type State = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; data: VoiceCollection }

/**
 * ADR-0060 — the Secretary of an in-person or hybrid meeting records Voice ID samples for its people on the room's mic,
 * the mic the meeting is transcribed from, so the room's voices are named right. Shown only to the meeting's Secretary
 * holding "Thu giọng mẫu hộ"; the server rechecks both on every call. A person who has not agreed cannot be recorded:
 * they agree themselves, on their own "Giọng nói của tôi" page.
 */
export function VoiceCollectionPanel({ meetingId }: { meetingId: string }) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [recordFor, setRecordFor] = useState<VoiceCollectionPerson | null>(null)
  const [erasing, setErasing] = useState<{ person: VoiceCollectionPerson; sample: CollectedSample } | null>(null)
  const player = useSamplePlayer((sampleId) => collectedSampleAudio(meetingId, sampleId))

  const generation = useRef(0)
  const load = useCallback(async () => {
    const mine = generation.current
    try {
      const data = await getVoiceCollection(meetingId)
      if (mine === generation.current) setState({ kind: 'loaded', data })
    } catch (error) {
      if (mine !== generation.current || handleAccessLost(error)) return
      setState({ kind: 'error', message: errorMessage(error) })
    }
  }, [meetingId, handleAccessLost])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  if (state.kind !== 'loaded') {
    return (
      <section className="card voice-card voice-collection" aria-busy={state.kind === 'loading'}>
        <Header />
        {state.kind === 'error' ? (
          <div className="alert alert-error" role="alert">
            <p>{state.message}</p>
            <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
              <Icon name="refresh" size={16} />
              Thử lại
            </button>
          </div>
        ) : (
          <p className="field-hint">Đang tải…</p>
        )}
      </section>
    )
  }

  const { data } = state
  return (
    <section className="card voice-card voice-collection" aria-label="Giọng mẫu cho mic phòng">
      <Header />
      {!data.canCollect && (
        <p className="alert alert-info">Chỉ thu thêm được khi cuộc họp đã công bố hoặc đang họp.</p>
      )}
      <ul className="voice-people">
        {data.people.map((person) => {
          const canRecord = person.consented && data.canCollect && person.sampleCount < data.maxSamples
          return (
            <li key={person.userId} className="voice-person">
              {/* One line per person: who, then what can be done. Nothing disabled is shown: no consent reads as words. */}
              <div className="voice-person-head">
                <span className="voice-person-name">{person.fullName}</span>
                {person.consented ? (
                  <span
                    className="voice-person-count"
                    data-tooltip="Mẫu thu hộ của người này, tính cả ở cuộc họp khác"
                    tabIndex={0}
                  >
                    {person.sampleCount}/{data.maxSamples}
                  </span>
                ) : (
                  <span className="voice-person-state">Chưa đồng ý</span>
                )}
                {canRecord && (
                  <button type="button" className="btn btn-outline btn-sm" onClick={() => setRecordFor(person)}>
                    <Icon name="mic" size={16} />
                    Thu
                  </button>
                )}
              </div>
              {person.samples.length > 0 && (
                <ol className="voice-samples">
                  {person.samples.map((sample, index) => (
                    <li key={sample.id} className="voice-sample">
                      <span className="voice-sample-text">
                        <span className="voice-sample-name">Mẫu {index + 1}</span>
                        <span
                          className="field-hint"
                          data-tooltip={`${sample.collectedByName} thu lúc ${formatDateTime(sample.createdAt)}`}
                          tabIndex={0}
                        >
                          {formatDuration(sample.speechMs)} · {formatDate(sample.createdAt)}
                        </span>
                      </span>
                      <button
                        type="button"
                        className="btn btn-ghost btn-icon btn-sm"
                        onClick={() =>
                          void player.toggle(sample.id).catch((error) => toast.show(errorMessage(error), 'error'))
                        }
                        aria-label={`${player.playing === sample.id ? 'Dừng' : 'Nghe lại'} mẫu ${index + 1} của ${person.fullName}`}
                        aria-pressed={player.playing === sample.id}
                        data-tooltip={player.playing === sample.id ? 'Dừng' : 'Nghe lại'}
                      >
                        <Icon name={player.playing === sample.id ? 'pause' : 'play'} size={16} />
                      </button>
                      <button
                        type="button"
                        className="btn btn-ghost btn-icon btn-sm"
                        onClick={() => setErasing({ person, sample })}
                        aria-label={`Xoá mẫu ${index + 1} của ${person.fullName}`}
                        data-tooltip="Xoá mẫu"
                      >
                        <Icon name="trash" size={16} />
                      </button>
                    </li>
                  ))}
                </ol>
              )}
            </li>
          )
        })}
      </ul>

      {recordFor && (
        <RecordSampleDialog
          title={`Thu giọng mẫu: ${recordFor.fullName}`}
          hint="Dùng micro phòng họp. Mời người này ngồi chỗ quen thuộc, nói tự nhiên như khi phát biểu, đọc đoạn văn sau:"
          passage={PASSAGES[recordFor.sampleCount % PASSAGES.length]}
          upload={(audio, fileName) => collectVoiceSample(meetingId, recordFor.userId, audio, fileName)}
          onClose={() => setRecordFor(null)}
          onSaved={async (message) => {
            setRecordFor(null)
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
              const result = await deleteCollectedSample(meetingId, erasing.sample.id)
              toast.show(result.message, 'success')
            } finally {
              setErasing(null)
              await load()
            }
          }}
        >
          <p>Mẫu này của {erasing.person.fullName} bị xoá hẳn. Muốn thay thì thu lại mẫu mới.</p>
        </ConfirmDialog>
      )}
    </section>
  )
}

function Header() {
  return (
    <div className="voice-status">
      <h2 className="card-title">Giọng mẫu cho mic phòng</h2>
      <InfoTip text="Thu giọng từng người bằng chính micro phòng họp để bản gỡ băng nhận đúng tên. Người dự phải tự bật đồng ý ở trang Giọng nói của tôi, tab Thư ký thu hộ; họ nhận thông báo mỗi lần được thu và xoá được mẫu bất cứ lúc nào." />
    </div>
  )
}
