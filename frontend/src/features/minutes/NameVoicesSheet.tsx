import { useCallback, useEffect, useRef, useState } from 'react'
import { getRecordingPlayback, listRecordings } from '../../api/recordings'
import { getTranscript } from '../../api/transcripts'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { errorMessage } from '../../lib/errors'
import type { Recording } from '../../types/recording'
import type { Transcript, TranscriptSegment } from '../../types/transcript'
import { SpeakerPicker } from '../recordings/SpeakerList'
import { speakerColor, voiceName, voicesOf, type Voice } from '../recordings/speakers'

/** How many of a voice's lines are shown to tell who it is. */
const SAMPLES = 2

type Loaded = { recording: Recording; transcript: Transcript }

const named = (voice: Voice) => !!(voice.speaker.assignedUserId ?? voice.speaker.assignedGuestId)

/**
 * AI-eCabinet's NameVoicesSheet: every voice of the meeting's recordings that has no name yet, each with what it said
 * and a play button per line, so it is named by ear right from the Biên bản tab instead of hunting for it in Bản ghi.
 * A voice drops out of the list once named; the draft is then made again from the footer.
 */
export function NameVoicesSheet({
  meetingId,
  drafted,
  canGenerate,
  onClose,
  onGenerate,
}: {
  meetingId: string
  /** There is a draft already: the footer offers to write it again with the names. */
  drafted: boolean
  /** Why the draft cannot be asked for now, when it cannot; otherwise undefined. */
  canGenerate: string | undefined
  /** Closes; the caller reads the readiness again, since names may have been given. */
  onClose: () => void
  onGenerate: () => void
}) {
  const [state, setState] = useState<{ kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'ready'; items: Loaded[] }>(
    { kind: 'loading' },
  )

  const load = useCallback(async () => {
    try {
      const recordings = (await listRecordings(meetingId)).items.filter(
        (r) => r.status === 'Available' && !r.deletedAt && r.transcriptStatus === 'Completed',
      )
      const items = await Promise.all(recordings.map(async (recording) => ({ recording, transcript: await getTranscript(recording.id) })))
      setState({ kind: 'ready', items })
    } catch (error) {
      setState({ kind: 'error', message: errorMessage(error) })
    }
  }, [meetingId])

  useEffect(() => {
    void Promise.resolve().then(load)
  }, [load])

  // One transcript read again after a name is picked: the voice leaves the list, the count below goes down.
  const reload = async (recordingId: string) => {
    try {
      const transcript = await getTranscript(recordingId)
      setState((s) =>
        s.kind === 'ready'
          ? { kind: 'ready', items: s.items.map((x) => (x.recording.id === recordingId ? { ...x, transcript } : x)) }
          : s,
      )
    } catch {
      void load()
    }
  }

  const left =
    state.kind === 'ready'
      ? state.items.reduce((n, x) => n + [...voicesOf(x.transcript.speakers).voices.values()].filter((v) => !named(v)).length, 0)
      : null
  const canEdit = state.kind === 'ready' && state.items.some((x) => x.transcript.canEdit)

  return (
    <Dialog
      size="sheet"
      title="Gán tên người nói"
      description="Nghe lời mẫu dưới mỗi giọng rồi chọn đúng người. Giọng chưa có tên hiện là “Người nói N” trong dự thảo."
      onClose={onClose}
      footer={
        <div className="name-voices-foot">
          {left !== null && left > 0 && (
            <p role="status" className="name-voices-left">
              Còn {left} giọng chưa có tên
            </p>
          )}
          <div className="name-voices-actions">
            <button type="button" className="btn btn-outline btn-lg" onClick={onClose}>
              Để sau
            </button>
            <button
              type="button"
              className="btn btn-primary btn-lg"
              disabled={!!canGenerate}
              data-tooltip={canGenerate}
              onClick={onGenerate}
            >
              <Icon name={drafted ? 'refresh' : 'sparkles'} size={16} />
              {drafted ? 'Tạo lại dự thảo' : 'Tạo dự thảo'}
            </button>
          </div>
        </div>
      }
    >
      {state.kind === 'loading' && <div className="skeleton name-voices-skeleton" aria-busy="true" />}
      {state.kind === 'error' && (
        <div className="alert alert-error" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}
      {state.kind === 'ready' && (
        <div className="name-voices">
          {!canEdit && left !== 0 && <p className="field-hint">Chỉ Thư ký cuộc họp gán tên được.</p>}
          {state.items.map((item) => (
            <RecordingVoices key={item.recording.id} item={item} onChanged={() => void reload(item.recording.id)} />
          ))}
          {left === 0 && (
            <p className="name-voices-done">
              <Icon name="checkCircle" size={16} />
              Mọi giọng đã có tên.
            </p>
          )}
        </div>
      )}
    </Dialog>
  )
}

function RecordingVoices({ item, onChanged }: { item: Loaded; onChanged: () => void }) {
  const { recording, transcript } = item
  const audio = useRef<HTMLAudioElement>(null)
  const stopAt = useRef(0)
  const [src, setSrc] = useState<string | null>(null)
  const [playing, setPlaying] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)

  // One line at a time, from its first word to its last. The signed link is asked for on the first play only.
  const listen = async (line: TranscriptSegment) => {
    const el = audio.current
    if (!el) return
    if (playing === line.id) return el.pause()
    try {
      if (!src) {
        const url = (await getRecordingPlayback(recording.id)).url
        setSrc(url)
        el.src = url
      }
      stopAt.current = line.endMs
      el.currentTime = line.startMs / 1000
      setPlaying(line.id)
      await el.play()
    } catch {
      setPlaying(null)
      setFailed(true)
    }
  }

  const unnamed = [...voicesOf(transcript.speakers).voices.values()].filter((v) => !named(v))
  if (unnamed.length === 0) return null
  const linesOf = (voice: Voice) => transcript.segments.filter((s) => voice.labels.includes(s.speakerLabel) && s.text.trim())

  return (
    <section className="name-voices-recording" aria-label={`Giọng chưa có tên trong ${recording.fileName}`}>
      <audio
        ref={audio}
        preload="none"
        onPause={() => setPlaying(null)}
        onEnded={() => setPlaying(null)}
        onError={() => setPlaying(null)}
        onTimeUpdate={(event) => {
          if (event.currentTarget.currentTime * 1000 >= stopAt.current) event.currentTarget.pause()
        }}
      />
      <ul className="name-voices-list">
        {unnamed.map((voice) => {
          const lines = linesOf(voice)
          return (
            <li key={voice.speaker.label} className="name-voice">
              <div className="name-voice-head">
                {transcript.canEdit ? (
                  <SpeakerPicker transcript={transcript} voice={voice} onChanged={onChanged} />
                ) : (
                  <span className="speaker-name">
                    <span className="select-dot" style={{ background: speakerColor(voice.speaker.label) }} aria-hidden="true" />
                    {voiceName(voice.speaker)}
                  </span>
                )}
                <span className="name-voice-count">{lines.length} câu</span>
              </div>
              {voice.speaker.suggestedName && (
                <p className="name-voice-guess">
                  <Icon name="sparkles" size={14} />
                  Khớp giọng nhất: {voice.speaker.suggestedName}
                </p>
              )}
              <ul className="name-voice-lines">
                {lines.slice(0, SAMPLES).map((line) => {
                  const on = playing === line.id
                  const label = on ? 'Dừng nghe' : 'Nghe câu này'
                  return (
                    <li key={line.id}>
                      <button
                        type="button"
                        className={on ? 'name-voice-play is-on' : 'name-voice-play'}
                        aria-label={label}
                        aria-pressed={on}
                        data-tooltip={failed ? 'Không phát được bản ghi' : label}
                        onClick={() => void listen(line)}
                      >
                        <Icon name={on ? 'pause' : 'play'} size={12} />
                      </button>
                      <span className="name-voice-text">“{line.text.trim()}”</span>
                    </li>
                  )
                })}
              </ul>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
