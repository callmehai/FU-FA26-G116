import { Fragment, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { deleteSpeechClip, editSpeechClip, getSpeechClipsText, setSpeechClipExclusion } from '../../api/speech'
import { Badge } from '../../components/Badge'
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
import { SpokenWords } from '../recordings/SpokenWords'
import { MyClips } from './MyClips'
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

export function SpeechTimeline({
  speech,
  userId,
  startedAt,
}: {
  speech: Speech
  userId: string | undefined
  /** When the meeting began, for the times in "Ghi âm của tôi". */
  startedAt: string | null
}) {
  const { clips, list } = speech
  const player = useClipPlayer(clips)
  const [filter, setFilter] = useState<string | null>(null)
  const [removing, setRemoving] = useState<SpeechClip | null>(null)
  const [mineOpen, setMineOpen] = useState(false)
  const toast = useToast()
  const box = useRef<HTMLOListElement>(null)
  const stuck = useRef(true)

  // Speakers in the order they were first heard; their colour follows that order. Each counts the paragraphs its
  // filter shows (BUGS 21/11: "Xem 26 lượt" counted sentences, and the filtered list showed fewer, merged ones), less
  // those taken out of the record.
  const speakers = useMemo(() => {
    const seen = new Map<string, { id: string; name: string; count: number; slot: number }>()
    for (const clip of clips)
      if (!seen.has(clip.speakerId))
        seen.set(clip.speakerId, { id: clip.speakerId, name: clip.speakerName, count: 0, slot: seen.size })
    for (const speaker of seen.values())
      speaker.count = groupClips(clips.filter((c) => c.speakerId === speaker.id)).filter((g) => !g.clips[0].excluded).length
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

  // Playing the record never moves the list (team test 21/11/2026): people read elsewhere while it plays, and a
  // sentence played from a click is already in view. The sentence being heard is still marked where it is.

  const kept = clips.filter((c) => !c.excluded).length
  const live = !!list?.canCapture
  const mine = clips.filter((c) => c.speakerId === userId)
  const { pending, refused, retrying } = speech.capture
  const failed = refused + retrying

  return (
    <section className="tl-card" aria-labelledby="tl-title">
      <div className="tl-head">
        <h2 id="tl-title" className="tl-title">
          {live ? 'Bản ghi trực tiếp' : 'Bản ghi cuộc họp'}
          {kept > 0 && <span className="tl-count">{kept}</span>}
        </h2>
        {(mine.length > 0 || pending.length > 0) && (
          <button type="button" className="btn btn-outline btn-sm tl-mine" onClick={() => setMineOpen(true)}>
            <Icon name="clips" size={16} />
            Ghi âm của tôi
            {failed > 0 && (
              <span className="tl-mine-badge" aria-label={`${failed} câu chưa gửi được`}>
                {failed}
              </span>
            )}
          </button>
        )}
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
          {live && <p className="field-hint">Lời nói trong phòng họp trực tuyến hiện ở đây theo từng câu.</p>}
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
                alone={!!shownFilter}
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
      {mineOpen && (
        <MyClips
          clips={mine}
          pending={pending}
          startedAt={startedAt}
          onClose={() => setMineOpen(false)}
          onChanged={speech.reload}
        />
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

/**
 * One speaker's run of sentences, read as one paragraph under one name and time (Hải, 19/11/2026): the sentences flow
 * on, each still its own — a click hears it, its time shows on hover or focus — and the tools at the paragraph's end
 * act on the sentence last clicked or focused (the first one until then).
 */
function Paragraph({
  group,
  slot,
  player,
  canEdit,
  canExclude,
  alone,
  onRemove,
  onChanged,
}: {
  group: Group
  slot: number
  player: ClipPlayer
  canEdit: boolean
  canExclude: boolean
  /** The record is filtered to one speaker: a sentence played stops at its end (Hải, Khánh 19/11/2026). */
  alone: boolean
  onRemove: (clip: SpeechClip) => void
  onChanged: () => void
}) {
  const toast = useToast()
  const first = group.clips[0]
  const colour = speakerSlot(slot)
  // The paragraph's own length, as a voice message shows it: when it was said is the clock beside the name.
  const length = group.clips.reduce((total, clip) => total + (clip.hasAudio ? clip.durationMs : 0), 0)
  const hearing = group.clips.some((c) => c.id === player.currentId)
  const leak = first.leak
  const gone = first.excluded && !canExclude
  const [pickedId, setPickedId] = useState<string | null>(null)
  const picked = group.clips.find((c) => c.id === pickedId) ?? first
  const [editing, setEditing] = useState<string | null>(null)
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const several = group.clips.length > 1
  const which = several ? ` câu ${wall(picked.startedAt)}` : ''

  const exclude = async (clip: SpeechClip, reason: 'Leak' | 'Other' | null) => {
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

  const save = async (clip: SpeechClip) => {
    setBusy(true)
    setError(null)
    try {
      toast.show((await editSpeechClip(clip.id, draft)).message, 'success')
      setEditing(null)
    } catch (failure) {
      setError(errorMessage(failure))
    } finally {
      setBusy(false)
    }
  }

  const canEditPicked = canEdit && !picked.excluded
  const tools = !gone && !editing && (canEditPicked || picked.canDelete || canExclude)
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
          {/* SCRUM-129: a guest's own mic, under the name they gave. */}
          {first.speakerIsGuest && <Badge tone="outline">Khách</Badge>}
          <time className="tl-time-inline" dateTime={first.startedAt}>
            {wall(first.startedAt)}
          </time>
          {first.hasAudio && (
            <button
              type="button"
              className={hearing && player.playing ? 'tl-play is-on' : 'tl-play'}
              onClick={() => (hearing && player.playing ? player.toggle() : player.playFrom(first, { alone, then: alone ? group.clips.slice(1) : undefined }))}
              aria-label={hearing && player.playing ? 'Tạm dừng' : `Nghe lại từ ${wall(first.startedAt)}`}
              data-tooltip={hearing && player.playing ? 'Tạm dừng' : alone ? (group.clips.length > 1 ? 'Nghe cả đoạn' : 'Nghe câu này') : 'Nghe lại'}
            >
              <Icon name={hearing && player.playing ? 'pause' : 'play'} size={12} />
              {length > 0 && formatClock(length)}
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
              data-tooltip={`${first.excludedByName ?? 'Chủ toạ hoặc Thư ký'} đã bỏ${
                first.exclusionReason === 'Leak' ? ' vì lẫn tiếng micro khác' : ''
              }`}
            >
              Đã bỏ khỏi biên bản
            </span>
          )}
        </div>
      </div>
      <div className="tl-row tl-sentence">
        <span className="tl-time" />
        <span className="tl-rail" aria-hidden="true" />
        <div className="tl-content">
          {gone ? (
            <p className="tl-text tl-gone">Câu đã được bỏ khỏi biên bản.</p>
          ) : (
            <div className="tl-text tl-para">
              {group.clips.map((clip, i) =>
                editing === clip.id ? (
                  <div key={clip.id} className="transcript-editor">
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
                          setEditing(null)
                          setError(null)
                        }}
                      >
                        Huỷ
                      </button>
                      <button
                        type="button"
                        className="btn btn-primary btn-sm"
                        disabled={busy || !draft.trim()}
                        onClick={() => void save(clip)}
                      >
                        {busy ? 'Đang lưu…' : 'Lưu'}
                      </button>
                    </div>
                  </div>
                ) : (
                  <Fragment key={clip.id}>
                    {i > 0 && ' '}
                    <Sentence
                      clip={clip}
                      player={player}
                      picked={several && clip.id === picked.id}
                      onPick={() => setPickedId(clip.id)}
                      onPlay={() => player.playFrom(clip, { alone })}
                    />
                  </Fragment>
                ),
              )}
            </div>
          )}
          {tools && (
            <span className="tl-tools">
              {canEditPicked && (
                <button
                  type="button"
                  className="tl-tool"
                  aria-label={`Sửa nội dung${which}`}
                  data-tooltip={`Sửa nội dung${which}`}
                  onClick={() => {
                    setDraft(picked.text)
                    setError(null)
                    setEditing(picked.id)
                  }}
                >
                  <Icon name="pencil" size={16} />
                </button>
              )}
              {canExclude &&
                (picked.excluded ? (
                  <button
                    type="button"
                    className="tl-tool"
                    aria-label={`Khôi phục${which}`}
                    data-tooltip={`Khôi phục${which}`}
                    disabled={busy}
                    onClick={() => void exclude(picked, null)}
                  >
                    <Icon name="undo" size={16} />
                  </button>
                ) : (
                  <button
                    type="button"
                    className="tl-tool"
                    aria-label={`Bỏ khỏi biên bản${which}`}
                    data-tooltip={`Bỏ khỏi biên bản${which}`}
                    disabled={busy}
                    onClick={() => void exclude(picked, picked.leak ? 'Leak' : 'Other')}
                  >
                    <Icon name="eyeOff" size={16} />
                  </button>
                ))}
              {picked.canDelete && (
                <button
                  type="button"
                  className="tl-tool"
                  aria-label={`Xoá câu${several ? ` ${wall(picked.startedAt)}` : ''}`}
                  data-tooltip={`Xoá câu${several ? ` ${wall(picked.startedAt)}` : ''}`}
                  onClick={() => onRemove(picked)}
                >
                  <Icon name="trash" size={16} />
                </button>
              )}
            </span>
          )}
        </div>
      </div>
    </li>
  )
}

/**
 * One sentence inside its paragraph: a click (or Enter) hears it, focus or a click picks it for the paragraph's tools,
 * and its time shows on hover or focus. While it plays, the word being said is marked when the worker timed its words.
 */
function Sentence({
  clip,
  player,
  picked,
  onPick,
  onPlay,
}: {
  clip: SpeechClip
  player: ClipPlayer
  /** The sentence the paragraph's tools act on, marked while the paragraph is pointed at. */
  picked: boolean
  onPick: () => void
  onPlay: () => void
}) {
  const active = clip.id === player.currentId
  const leak = clip.leak
  const leakWords = leak && !leak.whole ? leak.words : null
  const time = wall(clip.startedAt)
  const playable = clip.hasAudio
  return (
    <>
      <span
        className={`tl-say${active ? ' is-active' : ''}${picked ? ' is-picked' : ''}${playable ? ' is-playable' : ''}`}
        data-clip={clip.id}
        role={playable ? 'button' : undefined}
        tabIndex={0}
        aria-label={playable ? `Nghe câu lúc ${time}: ${clip.text}` : undefined}
        data-tooltip={playable ? `Nghe từ ${time}` : time}
        onFocus={onPick}
        onClick={() => {
          onPick()
          // Selecting words to copy them does not play.
          if (playable && !window.getSelection()?.toString()) onPlay()
        }}
        onKeyDown={(e) => {
          if (playable && (e.key === 'Enter' || e.key === ' ')) {
            e.preventDefault()
            onPlay()
          }
        }}
      >
        <SpokenWords
          text={clip.text}
          starts={clip.wordStartsMs}
          nowMs={active && player.playing ? player.offsetMs : null}
          marked={leakWords ? (word) => leakWords.some(([from, to]) => word >= from && word < to) : undefined}
          markClass="tl-leak-words"
        />
      </span>
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
    </>
  )
}
