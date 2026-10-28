import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { deleteSpeechClip, getSpeechClipPlayback } from '../../api/speech'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { SpeechClip } from '../../types/speech'
import type { PendingClip } from './useMicCapture'
import type { Speech } from './useSpeech'

/**
 * SCRUM-109 — the attendee's own mic as AI-eCabinet's floating card: in the corner of every tab of a running meeting,
 * so switching tabs never switches the mic off. It says whether what they say is being written down and kept, lets
 * them stop it, and lists their own sentences. Nobody can switch it on for them.
 */

const MIN_KEY = 'trustcabinet.mic.min'

/** Open the first time on a laptop, so the person reads what is kept; a bubble on a phone, where the card would
 * cover the record. Opening it shows the same before the mic can go on. */
function storedMin() {
  const phone = window.matchMedia('(max-width: 560px)').matches
  try {
    const stored = localStorage.getItem(MIN_KEY)
    return stored === null ? phone : stored === '1'
  } catch {
    return phone
  }
}

/** `h:mm:ss` on the meeting's clock, zero-padded: 00:41:07. */
function meetingClock(ms: number) {
  const s = Math.max(0, Math.floor(ms / 1000))
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${pad(Math.floor(s / 3600))}:${pad(Math.floor(s / 60) % 60)}:${pad(s % 60)}`
}

function useNow(active: boolean) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!active) return
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [active])
  return now
}

export function MicCard({
  speech,
  title,
  startedAt,
  userId,
}: {
  speech: Speech
  title: string
  /** When the meeting began, for its clock. */
  startedAt: string | null
  userId: string | undefined
}) {
  const { capture, mine, capturing, list } = speech
  const [min, setMin] = useState(storedMin)
  const [sheet, setSheet] = useState(false)
  const now = useNow(!!startedAt)
  const elsewhere = capture.state === 'elsewhere'
  const live = capturing && (capture.state === 'listening' || capture.state === 'speaking')
  const failed = capture.refused + capture.retrying
  // Something the person must act on opens the card, whatever they chose.
  const forced = capture.state === 'error'
  const minimised = min && !forced

  const remember = (value: boolean) => {
    setMin(value)
    try {
      localStorage.setItem(MIN_KEY, value ? '1' : '0')
    } catch {
      // A private window: the choice lasts for this page.
    }
  }

  const status =
    capture.state === 'error'
      ? (capture.error ?? 'Không mở được micro.')
      : elsewhere
        ? 'Micro đang ghi ở tab khác'
        : !mine
          ? 'Micro đang tắt'
          : capture.state === 'starting'
            ? 'Đang mở micro…'
            : 'Đang ghi âm'
  const mineSaved = speech.clips.filter((c) => c.speakerId === userId)
  const elapsed = startedAt ? meetingClock(now - Date.parse(startedAt)) : null

  const card = minimised ? (
    <button
      type="button"
      className={`mic-bubble${live ? ' is-live' : ''}`}
      onClick={() => remember(false)}
      aria-label={`Mở ô micro: ${status} · ${title}`}
      data-tooltip={`${title} · ${status}`}
    >
      <Icon name={mine ? 'mic' : 'micOff'} size={24} />
      {failed > 0 && <span className="mic-bubble-badge" aria-hidden="true" />}
    </button>
  ) : (
    <section className="mic-card" aria-labelledby="mic-card-title">
      <header className="mic-card-head">
        <h2 id="mic-card-title">{title}</h2>
        <button
          type="button"
          className="mic-card-min"
          onClick={() => remember(true)}
          aria-label="Thu nhỏ ô micro"
          data-tooltip="Thu nhỏ ô micro"
        >
          <Icon name="minus" size={16} />
        </button>
      </header>
      <div className="mic-card-body">
        <div className="mic-status" role="status">
          <span className={`mic-status-dot${live ? ' is-live' : capture.state === 'error' ? ' is-error' : ''}`} />
          <span className="mic-status-text" title={status}>
            {status}
          </span>
          {elapsed && (
            <span className="mic-clock" data-tooltip="Thời gian đã họp">
              {elapsed}
            </span>
          )}
        </div>
        {elsewhere && capturing ? (
          <>
            <p className="mic-hint">Mỗi người chỉ ghi ở một tab, để câu không bị ghi hai lần.</p>
            <div className="mic-controls">
              <button type="button" className="btn btn-primary mic-take" onClick={capture.takeOver}>
                <Icon name="mic" size={18} />
                Ghi ở tab này
              </button>
              <ClipsButton count={mineSaved.length} failed={failed} onOpen={() => setSheet(true)} />
            </div>
          </>
        ) : (
          <div className="mic-controls">
            <button
              type="button"
              className={`mic-toggle${mine ? ' is-on' : ''}`}
              aria-pressed={mine}
              onClick={() => speech.setMine(!mine)}
              aria-label={mine ? 'Tắt micro' : 'Bật micro'}
              data-tooltip={mine ? 'Tắt micro' : 'Bật micro'}
            >
              <Icon name={mine ? 'mic' : 'micOff'} size={22} />
            </button>
            <Meter level={live ? capture.level : 0} live={live} />
            <ClipsButton count={mineSaved.length} failed={failed} onOpen={() => setSheet(true)} />
          </div>
        )}
        {!mine && list && (
          <p
            className="mic-hint"
            tabIndex={0}
            data-tooltip="Người trong cuộc họp đọc và nghe được. Bạn xoá được câu của mình trong Ghi âm của tôi."
          >
            Lưu chữ và tiếng từng câu, tự xoá sau {list.retentionDays} ngày.
          </p>
        )}
        {capture.engineDown ? (
          <p className="mic-notice">
            <Icon name="alertCircle" size={16} />
            Máy chuyển chữ chưa chạy: {capture.waiting}&nbsp;câu đang chờ, tự gửi khi máy chạy lại.
          </p>
        ) : (
          capture.retrying > 0 && (
            <p className="mic-notice">
              <Icon name="alertCircle" size={16} />
              Chưa gửi được {capture.waiting}&nbsp;câu, đang thử lại.
            </p>
          )
        )}
        {capture.refused > 0 && (
          <p className="mic-notice mic-notice-error">
            <Icon name="alertCircle" size={16} />
            {capture.refused}&nbsp;câu không gửi được.
          </p>
        )}
      </div>
    </section>
  )

  return (
    <>
      {createPortal(<div className="mic-dock">{card}</div>, document.body)}
      {sheet && (
        <MyClips
          clips={mineSaved}
          pending={capture.pending}
          startedAt={startedAt}
          onClose={() => setSheet(false)}
          onChanged={speech.reload}
        />
      )}
    </>
  )
}

function ClipsButton({ count, failed, onOpen }: { count: number; failed: number; onOpen: () => void }) {
  const label = `Ghi âm của tôi · ${count} câu đã lưu`
  return (
    <button type="button" className="mic-clips" onClick={onOpen} aria-label={label} data-tooltip={label}>
      <Icon name="clips" size={20} />
      {failed > 0 && <span className="mic-clips-badge">{failed}</span>}
    </button>
  )
}

/** Bars that light from the left with loudness, like a call app's mic test; none in silence. */
function Meter({ level, live }: { level: number; live: boolean }) {
  const ref = useRef<HTMLSpanElement>(null)
  const [count, setCount] = useState(18)
  useLayoutEffect(() => {
    const el = ref.current
    if (!el) return
    const measure = () => setCount(Math.max(8, Math.floor(el.clientWidth / 8)))
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(el)
    return () => observer.disconnect()
  }, [])
  const lit = Math.round(level * count)
  return (
    <span ref={ref} className={live ? 'mic-meter is-live' : 'mic-meter'} aria-hidden="true">
      {Array.from({ length: count }, (_, i) => (
        <i key={i} className={i < lit ? 'is-lit' : undefined} />
      ))}
    </span>
  )
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

function MyClips({
  clips,
  pending,
  startedAt,
  onClose,
  onChanged,
}: {
  clips: SpeechClip[]
  pending: PendingClip[]
  startedAt: string | null
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
          <p className="field-hint">Bật micro và phát biểu, từng câu sẽ hiện ở đây.</p>
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
