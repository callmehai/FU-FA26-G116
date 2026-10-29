import { useEffect, useRef, useState } from 'react'
import { deleteSpeechClip, getSpeechClipPlayback } from '../../api/speech'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { SpeechClip } from '../../types/speech'
import type { PendingClip } from './useMicCapture'

/**
 * SCRUM-109 — "Ghi âm của tôi", as AI-eCabinet's: the person's own sentences, the ones still on their device and the
 * ones the meeting has, each to hear again or delete. Opened from the record and from the call room.
 */

/** `h:mm:ss` on the meeting's clock, zero-padded: 00:41:07. */
function meetingClock(ms: number) {
  const s = Math.max(0, Math.floor(ms / 1000))
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${pad(Math.floor(s / 3600))}:${pad(Math.floor(s / 60) % 60)}:${pad(s % 60)}`
}

/** When in the meeting it was said and how long, the labels in the tooltip. */
function When({ at, length }: { at: string; length: string }) {
  return (
    <span className="my-clip-when" tabIndex={0} data-tooltip={`Thời điểm nói ${at} · dài ${length}`}>
      <strong>{at}</strong>
      <span>{length}</span>
    </span>
  )
}

export function MyClips({
  clips,
  pending,
  startedAt,
  recordHref,
  onClose,
  onChanged,
}: {
  clips: SpeechClip[]
  pending: PendingClip[]
  startedAt: string | null
  /** From the call room: the whole record, in a new tab so the call goes on (ADR-0011). */
  recordHref?: string
  onClose: () => void
  onChanged: () => void
}) {
  const toast = useToast()
  const [playing, setPlaying] = useState<string | null>(null)
  const [removing, setRemoving] = useState<SpeechClip | null>(null)
  const audio = useRef<HTMLAudioElement | null>(null)
  const at = (iso: string | Date) =>
    meetingClock((typeof iso === 'string' ? Date.parse(iso) : iso.getTime()) - (startedAt ? Date.parse(startedAt) : 0))
  const seconds = (ms: number) => `${Math.max(1, Math.round(ms / 1000))} giây`

  useEffect(() => () => audio.current?.pause(), [])

  const play = async (clip: SpeechClip) => {
    audio.current?.pause()
    if (playing === clip.id) {
      setPlaying(null)
      return
    }
    try {
      const el = (audio.current ??= new Audio())
      el.src = (await getSpeechClipPlayback(clip.id)).url
      el.onended = () => setPlaying(null)
      setPlaying(clip.id)
      await el.play()
    } catch (failure) {
      setPlaying(null)
      toast.show(failure instanceof DOMException ? 'Không phát được câu này.' : errorMessage(failure), 'error')
    }
  }

  const newest = [...clips].reverse()
  return (
    <Dialog
      title="Ghi âm của tôi"
      description="Mỗi câu bạn nói được lưu riêng rồi chuyển thành chữ."
      onClose={onClose}
      size="sheet"
    >
      {pending.length === 0 && clips.length === 0 ? (
        <div className="my-clips-empty">
          <Icon name="mic" size={28} />
          <p className="empty-title">Chưa có câu nào</p>
          <p className="field-hint">Phát biểu trong phòng họp trực tuyến, từng câu sẽ hiện ở đây.</p>
        </div>
      ) : (
        <>
          {pending.length > 0 && (
            <section className="my-clips-section">
              <h3>Chưa lên hệ thống · {pending.length}</h3>
              <ul>
                {pending.map((clip) => (
                  <li key={clip.id} className="my-clip">
                    <div className="my-clip-line">
                      <When at={at(clip.startedAt)} length={seconds(clip.durationMs)} />
                      <span className="my-clip-chip is-wait">{clip.sending ? 'Đang gửi' : 'Chờ gửi lại'}</span>
                    </div>
                    {clip.attempts > 0 && (
                      <p className="field-hint">Đã thử {clip.attempts} lần. Hệ thống tự gửi lại.</p>
                    )}
                  </li>
                ))}
              </ul>
            </section>
          )}
          {clips.length > 0 && (
            <section className="my-clips-section">
              <h3>Đã lưu lên hệ thống · {clips.length}</h3>
              <ul>
                {newest.map((clip) => (
                  <li key={clip.id} className="my-clip">
                    <div className="my-clip-line">
                      {clip.hasAudio && (
                        <button
                          type="button"
                          className="btn btn-outline btn-icon btn-sm my-clip-play"
                          onClick={() => void play(clip)}
                          aria-label={playing === clip.id ? 'Dừng nghe' : 'Nghe lại'}
                          data-tooltip={playing === clip.id ? 'Dừng nghe' : 'Nghe lại'}
                        >
                          <Icon name={playing === clip.id ? 'pause' : 'play'} size={16} />
                        </button>
                      )}
                      <When at={at(clip.startedAt)} length={seconds(clip.durationMs)} />
                      <span className={clip.excluded ? 'my-clip-chip' : 'my-clip-chip is-done'}>
                        {clip.excluded ? 'Đã bỏ khỏi biên bản' : 'Đã chuyển chữ'}
                      </span>
                      {clip.canDelete && (
                        <button
                          type="button"
                          className="btn btn-ghost btn-icon btn-sm"
                          onClick={() => setRemoving(clip)}
                          aria-label="Xoá câu này"
                          data-tooltip="Xoá câu này"
                        >
                          <Icon name="trash" size={16} />
                        </button>
                      )}
                    </div>
                    {clip.text && <p className="my-clip-text">{clip.text}</p>}
                  </li>
                ))}
              </ul>
            </section>
          )}
        </>
      )}
      {recordHref && (
        <a className="my-clips-record" href={recordHref} target="_blank" rel="noopener">
          <Icon name="externalLink" size={16} />
          Xem cả bản ghi cuộc họp
        </a>
      )}
      {removing && (
        <ConfirmDialog
          title="Xoá câu này?"
          confirmLabel="Xoá câu"
          pendingLabel="Đang xoá…"
          cancelLabel="Giữ lại"
          tone="danger"
          onConfirm={async () => {
            toast.show((await deleteSpeechClip(removing.id)).message, 'success')
            onChanged()
          }}
          onClose={() => setRemoving(null)}
        >
          {removing.text && <p>“{removing.text}”</p>}
          <p className="field-hint">Câu và âm thanh của nó bị xoá khỏi cuộc họp.</p>
        </ConfirmDialog>
      )}
    </Dialog>
  )
}
