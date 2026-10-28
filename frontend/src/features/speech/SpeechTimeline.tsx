import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { deleteSpeechClip, editSpeechClip, getSpeechClipsText, setSpeechClipExclusion } from '../../api/speech'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import type { SpeechClip } from '../../types/speech'
import { saveBlob } from '../documents/documentFile'
import { formatClock } from '../recordings/format'
import { PlayerBar } from '../recordings/PlayerBar'
import { speakerSlot } from '../recordings/speakers'
import { useClipPlayer, type ClipPlayer } from './useClipPlayer'
import type { Speech } from './useSpeech'

/**
 * SCRUM-109, ADR-0039 — the per-mic record laid out as AI-eCabinet's timeline: one player for the whole meeting above
 * it, then each speaker's run of sentences under one name on a rail of coloured dots, with the speakers beside it to
 * filter by. Sentences one mic overheard from another are marked, never hidden; the Chair or the Secretary takes them
 * out of the record.
 */

const clock = new Intl.DateTimeFormat('vi-VN', {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
  timeZone: 'Asia/Ho_Chi_Minh',
})
const wall = (iso: string) => clock.format(new Date(iso))

/** A run of one person's sentences read as one paragraph: under 10 s apart, nothing else between them. */
const PARAGRAPH_GAP_MS = 10_000

interface Group {
  key: string
  speakerId: string
  clips: SpeechClip[]
}

/** A sentence that is out of the record or probably someone else's stands alone, as in eCab. */
const standsAlone = (clip: SpeechClip) => clip.excluded || !!clip.leak

function groupClips(clips: SpeechClip[]) {
  const groups: Group[] = []
  for (const clip of clips) {
    const last = groups.at(-1)
    const before = last?.clips.at(-1)
    if (
      last &&
      before &&
      last.speakerId === clip.speakerId &&
      !standsAlone(clip) &&
      !standsAlone(before) &&
      Date.parse(clip.startedAt) - (Date.parse(before.startedAt) + before.durationMs) < PARAGRAPH_GAP_MS
    )
      last.clips.push(clip)
    else groups.push({ key: clip.id, speakerId: clip.speakerId, clips: [clip] })
  }
  return groups
}

export function SpeechTimeline({ speech, room }: { speech: Speech; room: boolean }) {
  const { clips, list } = speech
  const player = useClipPlayer(clips)
  const [filter, setFilter] = useState<string | null>(null)
  const [removing, setRemoving] = useState<SpeechClip | null>(null)
  const toast = useToast()
  const box = useRef<HTMLOListElement>(null)
  const stuck = useRef(true)

  // Speakers in the order they were first heard; their colour follows that order.
  const speakers = useMemo(() => {
    const seen = new Map<string, { id: string; name: string; count: number; slot: number }>()
    for (const clip of clips) {
      const known = seen.get(clip.speakerId)
      if (known) known.count += clip.excluded ? 0 : 1
      else
        seen.set(clip.speakerId, {
          id: clip.speakerId,
          name: clip.speakerName,
          count: clip.excluded ? 0 : 1,
          slot: seen.size,
        })
    }
    return [...seen.values()]
  }, [clips])
  const slotOf = useMemo(() => new Map(speakers.map((s) => [s.id, s.slot])), [speakers])
  const shownFilter = filter && slotOf.has(filter) ? filter : null
  const groups = useMemo(
    () => groupClips(shownFilter ? clips.filter((c) => c.speakerId === shownFilter) : clips),
    [clips, shownFilter],
  )

  // Like a chat: a new line scrolls into view while the reader is at the bottom, and leaves them alone when not.
  useLayoutEffect(() => {
    const el = box.current
    if (el && stuck.current) el.scrollTop = el.scrollHeight
  }, [clips.length])

  // While the record plays, the sentence being heard stays in view.
  useEffect(() => {
    const el = box.current
    if (!el || !player.playing || !player.currentId) return
    const row = el.querySelector<HTMLElement>(`[data-clip="${CSS.escape(player.currentId)}"]`)
    if (!row) return
    const top = row.offsetTop - el.offsetTop
    if (top < el.scrollTop || top + row.offsetHeight > el.scrollTop + el.clientHeight)
      el.scrollTo({ top: Math.max(0, top - 8), behavior: 'smooth' })
  }, [player.currentId, player.playing])

  const kept = clips.filter((c) => !c.excluded).length
  const live = !!list?.canCapture

  return (
    <section className={room ? 'tl-card tl-card-room' : 'tl-card'} aria-labelledby="tl-title">
      <div className="tl-head">
        <h2 id="tl-title" className="tl-title">
          {live ? 'Bản ghi trực tiếp' : 'Bản ghi cuộc họp'}
          {kept > 0 && <span className="tl-count">{kept}</span>}
        </h2>
        {list?.canExport && kept > 0 && <SaveText meetingId={speech.meetingId} />}
      </div>
      {player.places.length > 0 && (
        <div className="tl-player">
          <PlayerBar
            positionMs={player.positionMs}
            totalMs={player.totalMs}
            playing={player.playing}
            onToggle={player.toggle}
            onSeek={player.seek}
            playLabel="Nghe cả cuộc họp"
            waveform={player.waveform}
            speed={player.speed}
            onSpeed={player.setSpeed}
            volume={player.volume}
            onVolume={player.setVolume}
            muted={player.muted}
            onMute={player.setMuted}
          />
          {player.error && (
            <p className="field-error tl-play-error" role="alert">
              {player.error}
            </p>
          )}
        </div>
      )}
      {clips.length === 0 ? (
        <div className="tl-empty">
          <Icon name="messageSquare" size={28} />
          <p className="empty-title">Cuộc họp này chưa có câu nào được ghi</p>
          {list?.canCapture && !room && <p className="field-hint">Bật micro ở ô micro góc dưới để ghi lời bạn.</p>}
        </div>
      ) : (
        <div className={speakers.length > 1 ? 'tl-body tl-body-split' : 'tl-body'}>
          {speakers.length > 1 && (
            <aside className="tl-speakers" aria-labelledby="tl-speakers-title">
              <h3 id="tl-speakers-title" className="tl-speakers-title">
                Người nói <span className="tl-pill">{speakers.length}</span>
              </h3>
              <ul>
                {speakers.map((s) => {
                  const on = shownFilter === s.id
                  return (
                    <li key={s.id} className={on ? 'tl-speaker is-on' : 'tl-speaker'}>
                      <span className="tl-speaker-name" style={{ color: speakerSlot(s.slot).ink }} title={s.name}>
                        {s.name}
                      </span>
                      <button
                        type="button"
                        className={on ? 'tl-filter is-on' : 'tl-filter'}
                        aria-pressed={on}
                        onClick={() => setFilter(on ? null : s.id)}
                        data-tooltip={`Chỉ xem lời trên micro của ${s.name}`}
                      >
                        <Icon name={on ? 'filterOff' : 'filter'} size={14} />
                        {on ? 'Bỏ lọc' : `Xem ${s.count} lượt`}
                      </button>
                    </li>
                  )
                })}
              </ul>
            </aside>
          )}
          <ol
            className="tl-list"
            ref={box}
            aria-label="Các câu theo thời điểm nói"
            onScroll={(e) => {
              const el = e.currentTarget
              stuck.current = el.scrollHeight - el.scrollTop - el.clientHeight < 32
            }}
          >
            {groups.map((group) => (
              <Paragraph
                key={group.key}
                group={group}
                slot={slotOf.get(group.speakerId) ?? 0}
                player={player}
                canEdit={!!list?.canEdit}
                canExclude={!!list?.canExclude}
                onRemove={setRemoving}
                onChanged={speech.reload}
              />
            ))}
          </ol>
        </div>
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
            speech.reload()
          }}
          onClose={() => setRemoving(null)}
        >
          <p>“{removing.text}”</p>
          <p className="field-hint">Câu và âm thanh của nó bị xoá khỏi cuộc họp.</p>
        </ConfirmDialog>
      )}
    </section>
  )
}

/** The Chair or a Secretary saves the per-mic transcript as text, without the sentences taken out of the record. */
function SaveText({ meetingId }: { meetingId: string }) {
  const toast = useToast()
  const [saving, setSaving] = useState(false)
  return (
    <button
      type="button"
      className="btn btn-outline btn-icon btn-sm"
      aria-label="Tải lời thoại (.txt)"
      data-tooltip="Tải lời thoại (.txt)"
      disabled={saving}
      onClick={async () => {
        setSaving(true)
        try {
          saveBlob(await getSpeechClipsText(meetingId), 'bản gỡ băng theo micro.txt')
        } catch (error) {
          toast.show(errorMessage(error), 'error')
        } finally {
          setSaving(false)
        }
      }}
    >
      <Icon name="download" size={16} />
    </button>
  )
}

const leakHint = (clip: SpeechClip) =>
  clip.leak &&
  (clip.leak.whole
    ? `Micro của ${clip.leak.speakerName} thu cùng câu này to hơn`
    : `Phần tô màu có thể là lời của ${clip.leak.speakerName}: micro của họ thu to hơn`)

function Paragraph({
  group,
  slot,
  player,
  canEdit,
  canExclude,
  onRemove,
  onChanged,
}: {
  group: Group
  slot: number
  player: ClipPlayer
  canEdit: boolean
  canExclude: boolean
  onRemove: (clip: SpeechClip) => void
  onChanged: () => void
}) {
  const first = group.clips[0]
  const colour = speakerSlot(slot)
  const at = player.placeOf.get(first.id)
  const hearing = group.clips.some((c) => c.id === player.currentId)
  const leak = first.leak
  const gone = first.excluded && !canExclude

  return (
    <li className={`tl-group${first.excluded ? ' is-excluded' : ''}`}>
      <div className="tl-row tl-row-head">
        <time className="tl-time" dateTime={first.startedAt}>
          {wall(first.startedAt)}
        </time>
        <span className="tl-rail" aria-hidden="true">
          <span className="tl-dot" style={{ background: first.excluded ? undefined : colour.dot }} />
        </span>
        <div className="tl-head-row">
          <strong className="tl-name" style={{ color: first.excluded ? undefined : colour.ink }}>
            {first.speakerName}
          </strong>
          <time className="tl-time-inline" dateTime={first.startedAt}>
            {wall(first.startedAt)}
          </time>
          {first.hasAudio && (
            <button
              type="button"
              className={hearing && player.playing ? 'tl-play is-on' : 'tl-play'}
              onClick={() => (hearing && player.playing ? player.toggle() : player.playFrom(first))}
              aria-label={hearing && player.playing ? 'Tạm dừng' : `Nghe lại từ ${wall(first.startedAt)}`}
              data-tooltip={hearing && player.playing ? 'Tạm dừng' : 'Nghe lại'}
            >
              <Icon name={hearing && player.playing ? 'pause' : 'play'} size={12} />
              {at !== undefined && formatClock(at)}
            </button>
          )}
          {leak && !first.excluded && (
            <span className="tl-leak" tabIndex={0} data-tooltip={leakHint(first) ?? undefined}>
              <Icon name={leak.whole ? 'leakWhole' : 'leakPart'} size={14} />
              <span>{leak.whole ? `Có thể là ${leak.speakerName}` : `Có đoạn của ${leak.speakerName}`}</span>
            </span>
          )}
          {first.excluded && canExclude && (
            <span
              className="tl-struck-label"
              tabIndex={0}
              data-tooltip={`${first.excludedByName ?? 'Chủ tọa hoặc Thư ký'} đã bỏ${
                first.exclusionReason === 'Leak' ? ' vì lẫn tiếng micro khác' : ''
              }`}
            >
              Đã bỏ khỏi biên bản
            </span>
          )}
        </div>
      </div>
      {gone ? (
        <div className="tl-row">
          <span className="tl-time" />
          <span className="tl-rail" aria-hidden="true" />
          <p className="tl-text tl-gone">Câu đã được bỏ khỏi biên bản.</p>
        </div>
      ) : (
        group.clips.map((clip, i) => (
          <Sentence
            key={clip.id}
            clip={clip}
            first={i === 0}
            player={player}
            canEdit={canEdit && !clip.excluded}
            canExclude={canExclude}
            onRemove={() => onRemove(clip)}
            onChanged={onChanged}
          />
        ))
      )}
    </li>
  )
}

/** The sentence's words, with those another mic's speaker probably said highlighted. */
function Words({ clip }: { clip: SpeechClip }) {
  const leak = clip.leak
  if (!leak || leak.whole) return <>{clip.text}</>
  const inLeak = (word: number) => leak.words.some(([from, to]) => word >= from && word < to)
  let word = -1
  return (
    <>
      {clip.text.split(/(\s+)/).map((part, i) => {
        if (!part.trim()) return part
        word++
        return inLeak(word) ? (
          <mark key={i} className="tl-leak-words">
            {part}
          </mark>
        ) : (
          part
        )
      })}
    </>
  )
}

function Sentence({
  clip,
  first,
  player,
  canEdit,
  canExclude,
  onRemove,
  onChanged,
}: {
  clip: SpeechClip
  first: boolean
  player: ClipPlayer
  canEdit: boolean
  canExclude: boolean
  onRemove: () => void
  onChanged: () => void
}) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(clip.text)
  const [error, setError] = useState<string | null>(null)
  const active = clip.id === player.currentId

  const exclude = async (reason: 'Leak' | 'Other' | null) => {
    setBusy(true)
    try {
      toast.show((await setSpeechClipExclusion(clip.id, reason)).message, 'success')
      onChanged()
    } catch (failure) {
      toast.show(errorMessage(failure), 'error')
    } finally {
      setBusy(false)
    }
  }

  const save = async () => {
    setBusy(true)
    setError(null)
    try {
      toast.show((await editSpeechClip(clip.id, draft)).message, 'success')
      setEditing(false)
    } catch (failure) {
      setError(errorMessage(failure))
    } finally {
      setBusy(false)
    }
  }

  const tools = !editing && (canEdit || clip.canDelete || canExclude)
  return (
    <div className={`tl-row tl-sentence${active ? ' is-active' : ''}`} data-clip={clip.id}>
      {first || !clip.hasAudio ? (
        <span className="tl-time">{first ? '' : wall(clip.startedAt)}</span>
      ) : (
        <button
          type="button"
          className="tl-time tl-time-button"
          onClick={() => player.playFrom(clip)}
          aria-label={`Nghe lại từ ${wall(clip.startedAt)}`}
        >
          {wall(clip.startedAt)}
        </button>
      )}
      <span className="tl-rail" aria-hidden="true" />
      <div className="tl-content">
        {editing ? (
          <div className="transcript-editor">
            <textarea
              value={draft}
              maxLength={8000}
              rows={Math.min(6, Math.max(2, Math.ceil(draft.length / 70)))}
              onChange={(e) => setDraft(e.target.value)}
              aria-label="Nội dung câu"
              aria-invalid={error ? true : undefined}
              autoFocus
            />
            {error && (
              <p className="field-error" role="alert">
                {error}
              </p>
            )}
            <div className="row-actions transcript-editor-actions">
              <button
                type="button"
                className="btn btn-ghost btn-sm"
                disabled={busy}
                onClick={() => {
                  setDraft(clip.text)
                  setEditing(false)
                }}
              >
                Huỷ
              </button>
              <button
                type="button"
                className="btn btn-primary btn-sm"
                disabled={busy || !draft.trim()}
                onClick={() => void save()}
              >
                {busy ? 'Đang lưu…' : 'Lưu'}
              </button>
            </div>
          </div>
        ) : (
          <p className="tl-text">
            <Words clip={clip} />
            {clip.editedAt && (
              <span
                className="tl-edited"
                tabIndex={0}
                data-tooltip={`Sửa bởi ${clip.editedByName ?? 'Thư ký'} lúc ${formatDateTime(clip.editedAt)}`}
              >
                {' '}
                đã sửa
              </span>
            )}
          </p>
        )}
        {tools && (
          <span className="tl-tools">
            {canEdit && (
              <button
                type="button"
                className="tl-tool"
                aria-label="Sửa nội dung"
                data-tooltip="Sửa nội dung"
                onClick={() => {
                  setDraft(clip.text)
                  setEditing(true)
                }}
              >
                <Icon name="pencil" size={16} />
              </button>
            )}
            {canExclude &&
              (clip.excluded ? (
                <button
                  type="button"
                  className="tl-tool"
                  aria-label="Khôi phục"
                  data-tooltip="Khôi phục"
                  disabled={busy}
                  onClick={() => void exclude(null)}
                >
                  <Icon name="undo" size={16} />
                </button>
              ) : (
                <button
                  type="button"
                  className="tl-tool"
                  aria-label="Bỏ khỏi biên bản"
                  data-tooltip="Bỏ khỏi biên bản"
                  disabled={busy}
                  onClick={() => void exclude(clip.leak ? 'Leak' : 'Other')}
                >
                  <Icon name="eyeOff" size={16} />
                </button>
              ))}
            {clip.canDelete && (
              <button type="button" className="tl-tool" aria-label="Xoá câu" data-tooltip="Xoá câu" onClick={onRemove}>
                <Icon name="trash" size={16} />
              </button>
            )}
          </span>
        )}
      </div>
    </div>
  )
}
