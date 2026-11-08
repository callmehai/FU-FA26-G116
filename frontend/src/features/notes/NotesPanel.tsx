import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { getAgenda } from '../../api/agenda'
import { ApiError } from '../../api/client'
import { getNotes, saveNote } from '../../api/notes'
import { Icon } from '../../components/Icon'
import { Select, type SelectOption } from '../../components/Select'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatClock, formatDateTime } from '../../lib/format'
import { routes } from '../../lib/routes'
import { NOTES_PARAM } from '../meetings/meetingDialogLink'
import type { AgendaItem } from '../../types/agenda'
import { noteMaxLength, type MeetingNote, type NotesState } from '../../types/note'
import { useAccessLost } from '../auth/useAccessLost'
import { useAuth } from '../auth/useAuth'
import { useMeetingEvents } from '../meetings/useMeetingEvents'
import {
  listNoteDrafts,
  readNoteDraft,
  removeNoteDraft,
  settleNoteDrafts,
  takeNoteDrafts,
  writeNoteDraft,
  type DraftScope,
  type NoteDraft,
} from './noteDrafts'

/** The key of the meeting-wide note; an agenda item's note is keyed by the item's id. */
const GENERAL = 'general'
const itemOf = (key: string) => (key === GENERAL ? null : key)
const keyOf = (note: MeetingNote) => note.agendaItemId ?? GENERAL

/** Saves this long after typing stops (UC-82 step 4). */
const SAVE_DELAY_MS = 1_000
/** After a failed save that was not a refusal, tries again this often. */
const RETRY_MS = 10_000
/** The counter shows once the text gets this close to the limit. */
const COUNTER_FROM = noteMaxLength - 2_000

/** MSG423 (AF-3): shown by the panel itself, with no request to answer it. */
const OFFLINE = 'Mất kết nối. Ghi chú vẫn giữ trên máy này và sẽ lưu khi có mạng lại.'
/**
 * MSG418 (EX-3), the server's sentence: a conflict is also found without a refused save — a copy kept on this device
 * that the server has moved past, or another window's save heard on the hub — so the panel holds the text itself.
 */
const STALE = 'Ghi chú vừa được sửa ở cửa sổ khác. Tải lại để xem bản mới nhất.'

type Load =
  | { kind: 'loading' }
  | { kind: 'loaded'; data: NotesState; items: AgendaItem[] }
  | { kind: 'error'; message: string }
  /** No longer the Chair's or the Secretary's: what was typed here and never saved is shown once, to copy. */
  | { kind: 'gone'; unsent: Unsent[]; titles: Map<string, string> }

type Status =
  | { kind: 'idle' }
  | { kind: 'dirty' }
  | { kind: 'saving' }
  /** MSG415 with the time it was saved (UC-82 step 5). */
  | { kind: 'saved'; at: string; message: string }
  | { kind: 'offline' }
  | { kind: 'failed'; message: string }
  /** Saved elsewhere after this window read it (MSG418): the newer text is shown beside the unsaved one. */
  | { kind: 'conflict'; latest: MeetingNote | null }
  /** Not the Secretary any more, the meeting stopped taking notes, or the item went (MSG416, MSG417, MSG420). */
  | { kind: 'refused'; message: string }

/**
 * The Secretary's running notes (SCRUM-30, UC-82), on the meeting page and in the call room (ADR-0011).
 *
 * The Secretary types into the note of the item the Chair has in progress — it follows the Chair until the Secretary
 * picks another — or into the meeting-wide note. Text is saved a second after typing stops, and kept in this browser
 * until the server has it, so a dropped connection or a closed tab loses nothing (BR-403). Two windows never overwrite
 * each other: a save from an older copy is refused (MSG418) and both versions are shown (BR-399). The Chair reads the
 * notes; the server says who may write (`canWrite`), never a guess from the role.
 */
export function NotesPanel({ meetingId, variant }: { meetingId: string; variant: 'dialog' | 'room' }) {
  const { user } = useAuth()
  const userId = user?.id ?? ''
  const handleAccessLost = useAccessLost()
  const [load, setLoad] = useState<Load>({ kind: 'loading' })

  const read = useCallback(async () => {
    try {
      const [data, agenda] = await Promise.all([getNotes(meetingId), getAgenda(meetingId)])
      settleNoteDrafts({ userId, meetingId, secret: data.secret })
      setLoad({ kind: 'loaded', data, items: agenda.items })
    } catch (err) {
      if (handleAccessLost(err)) return
      if (isApiError(err, 404)) {
        // No longer the Chair's or the Secretary's (EX-1, EX-8): nothing of these notes stays on the device (BR-403),
        // but text typed here and never saved stays on the page to copy until it is closed.
        const taken = takeNoteDrafts(userId, meetingId)
        return setLoad((s) => {
          const saved = s.kind === 'loaded' ? s.data.notes : []
          const unsent = [
            ...(s.kind === 'gone' ? s.unsent : []),
            ...taken.filter((d) => d.body !== (saved.find((n) => keyOf(n) === d.noteKey)?.body ?? '')),
          ]
          const titles = s.kind === 'loaded' ? titlesOf(s.items) : s.kind === 'gone' ? s.titles : new Map<string, string>()
          return { kind: 'gone', unsent, titles }
        })
      }
      setLoad((s) => (s.kind === 'loaded' ? s : { kind: 'error', message: errorMessage(err) }))
    }
  }, [meetingId, userId, handleAccessLost])
  const secret = load.kind === 'loaded' && load.data.secret
  const scope = useMemo<DraftScope>(() => ({ userId, meetingId, secret }), [userId, meetingId, secret])

  useEffect(() => {
    void Promise.resolve().then(read)
  }, [read])
  useMeetingEvents(meetingId, () => void read(), [
    'notesChanged',
    'agendaChanged',
    'participantsChanged',
    'meetingEnded',
  ])

  const room = variant === 'room'
  const wrap = (body: ReactNode) =>
    room ? (
      <div className="room-panel-body notes-room">
        <div className="notes-room-head">
          <h2 className="room-panel-title">Ghi chú</h2>
          {/* A wider page to write in, without leaving the call (ADR-0011); the two never overwrite each other. */}
          <a
            className="icon-button icon-button-sm"
            href={`${routes.meeting(meetingId)}?${NOTES_PARAM}=1`}
            target="_blank"
            rel="noopener"
            aria-label="Mở ghi chú ở tab mới"
            data-tooltip="Mở ở tab mới"
          >
            <Icon name="externalLink" size={16} />
          </a>
        </div>
        {body}
      </div>
    ) : (
      // Inside the meeting page's dialog, which already has the title and the frame: no card in a card.
      <div className="notes-card">{body}</div>
    )

  if (load.kind === 'gone')
    return wrap(
      <>
        <UnsentDrafts initial={load.unsent} titles={load.titles} />
        <p className="card-pending">Bạn không xem được ghi chú của cuộc họp này.</p>
      </>,
    )
  if (load.kind === 'error')
    return wrap(
      <div className="alert alert-error" role="alert">
        <p>{load.message}</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={() => void read()}>
          <Icon name="refresh" size={16} />
          Thử lại
        </button>
      </div>,
    )
  if (load.kind === 'loading')
    return wrap(
      <div className="notes-skeleton" aria-busy="true" aria-label="Đang tải ghi chú">
        <span />
        <span />
      </div>,
    )

  return wrap(
    load.data.canWrite ? (
      <NoteEditor
        key={meetingId}
        meetingId={meetingId}
        scope={scope}
        data={load.data}
        items={load.items}
        reload={read}
      />
    ) : (
      <NoteReader data={load.data} items={load.items} scope={scope} />
    ),
  )
}

/** The Chair's view, and the Secretary's once the meeting stops taking notes: every note, read-only. */
function NoteReader({
  data,
  items,
  scope,
}: {
  data: NotesState
  items: AgendaItem[]
  scope: DraftScope
}) {
  const titles = titlesOf(items)
  const titleOf = (key: string) => titleIn(titles, key)
  const notes = data.notes.filter((n) => n.body.trim() !== '')
  // Text typed here that never reached the server before the meeting stopped taking notes (EX-1, EX-2): shown so it
  // can be copied, then let go.
  const [unsent] = useState(() =>
    listNoteDrafts(scope).filter(
      (d) => d.body !== (data.notes.find((n) => keyOf(n) === d.noteKey)?.body ?? ''),
    ),
  )
  return (
    <>
      <UnsentDrafts initial={unsent} titles={titles} onDrop={(noteKey) => removeNoteDraft(scope, noteKey)} />
      {notes.length === 0 ? (
        <p className="value-empty notes-empty">Chưa có ghi chú.</p>
      ) : (
        <NoteList notes={notes} titleOf={titleOf} />
      )}
    </>
  )
}

type Unsent = NoteDraft & { noteKey: string }

const titlesOf = (items: AgendaItem[]) => new Map(items.map((i) => [i.id, `${i.position}. ${i.title}`]))
const titleIn = (titles: Map<string, string>, key: string) =>
  key === GENERAL ? 'Chung' : (titles.get(key) ?? 'Mục đã xoá')

/** Text that can no longer be saved where it was typed, shown so it can be copied (EX-1, EX-2, EX-5), then let go. */
function UnsentDrafts({
  initial,
  titles,
  onDrop,
}: {
  initial: Unsent[]
  titles: Map<string, string>
  onDrop?: (noteKey: string) => void
}) {
  const [dropped, setDropped] = useState<string[]>([])
  return initial
    .filter((d) => !dropped.includes(d.noteKey))
    .map((d) => (
      <div key={d.noteKey} className="alert alert-warning notes-conflict" role="alert">
        <p>Chưa lưu được: {titleIn(titles, d.noteKey)}</p>
        <p className="notes-text notes-latest">{d.body}</p>
        <div className="notes-conflict-actions">
          <button
            type="button"
            className="btn btn-outline btn-sm"
            onClick={() => void navigator.clipboard?.writeText(d.body)}
          >
            <Icon name="copy" size={16} />
            Sao chép
          </button>
          <button
            type="button"
            className="btn btn-outline btn-sm"
            onClick={() => {
              onDrop?.(d.noteKey)
              setDropped((list) => [...list, d.noteKey])
            }}
          >
            Bỏ
          </button>
        </div>
      </div>
    ))
}

function NoteList({ notes, titleOf }: { notes: MeetingNote[]; titleOf: (key: string) => string }) {
  return (
    <ol className="notes-read">
      {notes.map((note) => (
        <li key={keyOf(note)}>
          <h3 className="notes-read-title">{titleOf(keyOf(note))}</h3>
          <p
            className="notes-text"
            tabIndex={0}
            data-tooltip={`${note.author.fullName}, ${formatDateTime(note.updatedAt)}`}
          >
            {note.body}
          </p>
        </li>
      ))}
    </ol>
  )
}

function NoteEditor({
  meetingId,
  scope,
  data,
  items,
  reload,
}: {
  meetingId: string
  scope: DraftScope
  data: NotesState
  items: AgendaItem[]
  reload: () => Promise<void>
}) {
  const handleAccessLost = useAccessLost()
  const notes = useMemo(() => new Map(data.notes.map((n) => [keyOf(n), n])), [data.notes])
  const current = data.currentAgendaItemId ?? GENERAL
  // null follows the item the Chair has in progress (BR-397, UC-24); a pick holds until it is the current one again.
  const [picked, setPicked] = useState<string | null>(null)
  // A picked item that has since been deleted (EX-5) no longer holds: the note follows the Chair again.
  const held = picked === GENERAL || items.some((i) => i.id === picked) ? picked : null
  const key = held ?? current

  const [text, setText] = useState('')
  const [status, setStatus] = useState<Status>({ kind: 'idle' })
  // What the text on screen builds on, and what the server is known to hold at that version.
  const editing = useRef({ key: '', base: null as string | null, saved: '', text: '' })
  const timer = useRef<number | undefined>(undefined)
  const busy = useRef(false)
  const pending = useRef(false)
  /** `save` itself, for the retries it schedules from inside. */
  const again = useRef(() => {})
  /** `sendOthers` itself, for `save` to call once a save gets through. */
  const others = useRef(() => {})
  /** The note and text a save is sending: its own `notesChanged` can arrive before its answer. */
  const inflight = useRef<{ key: string; body: string } | null>(null)
  const othersBusy = useRef(false)

  const draft = useCallback(
    (noteKey: string, body: string, base: string | null) => writeNoteDraft(scope, noteKey, { body, base }),
    [scope],
  )

  /** Sends what is on screen. One save at a time; typing during a save sends again once it lands. */
  const save = useCallback(async () => {
    window.clearTimeout(timer.current)
    if (busy.current) {
      pending.current = true
      return
    }
    const { key: noteKey, base, text: body, saved } = editing.current
    if (!noteKey || body === saved) return
    busy.current = true
    inflight.current = { key: noteKey, body }
    setStatus({ kind: 'saving' })
    try {
      const { note, message } = await saveNote(meetingId, { agendaItemId: itemOf(noteKey), body, updatedAt: base })
      // The connection works again: what other notes kept unsent goes too (AF-3).
      others.current()
      if (editing.current.key !== noteKey) {
        // The Secretary moved to another note meanwhile; what they typed after this save is still kept.
        if (readNoteDraft(scope, noteKey)?.body === body) removeNoteDraft(scope, noteKey)
        return
      }
      editing.current.base = note.updatedAt
      editing.current.saved = body
      if (editing.current.text === body) {
        removeNoteDraft(scope, noteKey)
        setStatus({ kind: 'saved', at: note.updatedAt, message })
      } else {
        draft(noteKey, editing.current.text, note.updatedAt)
        pending.current = true
      }
    } catch (err) {
      // MSG416 is a 403 that is not lost access: the Secretary was replaced (EX-1), and the panel keeps the text to copy.
      const replaced = isApiError(err, 403, 'MSG416')
      if ((!replaced && handleAccessLost(err)) || editing.current.key !== noteKey) return
      if (isApiError(err, 409, 'MSG418')) {
        setStatus({ kind: 'conflict', latest: null })
        void reload()
      } else if (isApiError(err, 404)) {
        // EX-8: no longer Chair or Secretary, or the meeting went; the re-read shows the panel as gone.
        void reload()
      } else if (replaced || isApiError(err, 409, 'MSG417') || isApiError(err, 409, 'MSG420')) {
        setStatus({ kind: 'refused', message: errorMessage(err) })
      } else if (isApiError(err, 400, 'MSG419')) {
        setStatus({ kind: 'failed', message: errorMessage(err) })
      } else if (!(err instanceof ApiError) || !navigator.onLine) {
        setStatus({ kind: 'offline' })
      } else {
        setStatus({ kind: 'failed', message: errorMessage(err) })
        timer.current = window.setTimeout(() => again.current(), RETRY_MS)
      }
    } finally {
      busy.current = false
      inflight.current = null
      if (pending.current) {
        pending.current = false
        if (editing.current.text !== editing.current.saved) again.current()
      }
    }
  }, [meetingId, scope, draft, handleAccessLost, reload])
  useEffect(() => {
    again.current = () => void save()
  }, [save])

  /**
   * AF-3 for the notes not on screen: text kept unsent for another item (typed before the Chair moved on, or before the
   * tab was closed) is sent when the panel opens and when the connection returns, so it is in the minutes even if that
   * note is never opened again. A copy the server has moved past waits to be opened, where both versions are shown;
   * text for a deleted item is shown below to copy.
   */
  const sendOthers = useCallback(async () => {
    if (othersBusy.current || !navigator.onLine) return
    othersBusy.current = true
    let sent = false
    try {
      for (const d of listNoteDrafts(scope)) {
        if (d.noteKey === editing.current.key) continue
        if (d.noteKey !== GENERAL && !items.some((i) => i.id === d.noteKey)) continue
        const server = notes.get(d.noteKey)
        if (d.body === (server?.body ?? '')) {
          removeNoteDraft(scope, d.noteKey)
          continue
        }
        if (d.base !== (server?.updatedAt ?? null)) continue
        try {
          await saveNote(meetingId, { agendaItemId: itemOf(d.noteKey), body: d.body, updatedAt: d.base })
          if (readNoteDraft(scope, d.noteKey)?.body === d.body) removeNoteDraft(scope, d.noteKey)
          sent = true
        } catch (err) {
          if (isApiError(err, 403, 'MSG416')) return
          if (handleAccessLost(err) || !(err instanceof ApiError)) return
          // Refused or out of date: left for when that note is opened.
        }
      }
    } finally {
      othersBusy.current = false
      if (sent) void reload()
    }
  }, [scope, items, notes, meetingId, handleAccessLost, reload])
  useEffect(() => {
    others.current = () => void sendOthers()
    void Promise.resolve().then(() => others.current())
  }, [sendOthers])

  // Opens the note of `key`: what this browser kept unsent if the server has not moved on, else the server's text.
  useEffect(() => {
    const before = editing.current
    if (before.key === key) return
    if (before.key && before.text !== before.saved) void save()
    const server = notes.get(key) ?? null
    const kept = readNoteDraft(scope, key)
    const serverBody = server?.body ?? ''
    const serverBase = server?.updatedAt ?? null
    editing.current = { key, base: serverBase, saved: serverBody, text: serverBody }
    let next: Status = { kind: 'idle' }
    if (kept && kept.body !== serverBody) {
      editing.current.text = kept.body
      if (kept.base === serverBase) next = { kind: 'dirty' }
      else next = { kind: 'conflict', latest: server }
    } else if (kept) removeNoteDraft(scope, key)
    setText(editing.current.text)
    setStatus(next)
    if (next.kind === 'dirty') timer.current = window.setTimeout(() => void save(), SAVE_DELAY_MS)
  }, [key, notes, scope, save])

  // A newer copy from another window: taken as is when nothing here is unsaved, else shown as a conflict.
  useEffect(() => {
    const server = notes.get(editing.current.key)
    const { key: noteKey, base, text: body, saved } = editing.current
    // This window's own save, heard on the hub before its answer: the answer settles it.
    if (server && inflight.current?.key === noteKey && inflight.current.body === server.body) return
    if (server && server.body === body) {
      // The server already holds exactly this text (sent from another tab, say): nothing to resolve.
      if (server.updatedAt !== base) {
        editing.current = { ...editing.current, base: server.updatedAt, saved: server.body }
        removeNoteDraft(scope, noteKey)
      }
      return
    }
    if (!server || (base && Date.parse(server.updatedAt) <= Date.parse(base))) {
      if (status.kind === 'conflict' && status.latest === null && server)
        setStatus({ kind: 'conflict', latest: server })
      return
    }
    if (body === saved && status.kind !== 'conflict') {
      editing.current = { ...editing.current, base: server.updatedAt, saved: server.body, text: server.body }
      setText(server.body)
    } else if (status.kind !== 'conflict' || status.latest?.updatedAt !== server.updatedAt)
      setStatus({ kind: 'conflict', latest: server })
  }, [notes, status, scope])

  // Back online, or the tab is being left: send what waits.
  useEffect(() => {
    const flush = () => {
      if (editing.current.text !== editing.current.saved) void save()
      else others.current()
    }
    const onHidden = () => document.visibilityState === 'hidden' && flush()
    window.addEventListener('online', flush)
    document.addEventListener('visibilitychange', onHidden)
    return () => {
      window.removeEventListener('online', flush)
      document.removeEventListener('visibilitychange', onHidden)
      window.clearTimeout(timer.current)
      flush()
    }
  }, [save])

  // Nothing to resolve or send once the server holds exactly the text on screen (the copy above adopted its version).
  const shown: Status =
    (status.kind === 'conflict' || status.kind === 'dirty') && notes.get(key)?.body === text ? { kind: 'idle' } : status
  const locked = shown.kind === 'refused' || shown.kind === 'conflict'

  function change(value: string) {
    setText(value)
    editing.current.text = value
    draft(editing.current.key, value, editing.current.base)
    setStatus(value === editing.current.saved ? { kind: 'idle' } : { kind: 'dirty' })
    window.clearTimeout(timer.current)
    timer.current = window.setTimeout(() => void save(), SAVE_DELAY_MS)
  }

  function takeLatest() {
    const latest = status.kind === 'conflict' ? status.latest : null
    editing.current = {
      ...editing.current,
      base: latest?.updatedAt ?? null,
      saved: latest?.body ?? '',
      text: latest?.body ?? '',
    }
    removeNoteDraft(scope, editing.current.key)
    setText(editing.current.text)
    setStatus({ kind: 'idle' })
  }

  function keepMine() {
    const latest = status.kind === 'conflict' ? status.latest : null
    editing.current = { ...editing.current, base: latest?.updatedAt ?? null, saved: latest?.body ?? '' }
    draft(editing.current.key, editing.current.text, editing.current.base)
    setStatus({ kind: 'dirty' })
    void save()
  }

  // Text kept unsent for an item deleted since (EX-5): its saved text went into the meeting-wide note on the server;
  // this part never reached it, so it is shown to copy.
  const titles = titlesOf(items)
  const orphans = listNoteDrafts(scope).filter(
    (d) => d.noteKey !== GENERAL && d.noteKey !== key && !items.some((i) => i.id === d.noteKey),
  )

  const options: SelectOption<string>[] = [
    { value: GENERAL, label: 'Chung', hint: notes.get(GENERAL)?.body ? 'Có ghi chú' : undefined },
    ...items.map((item) => ({
      value: item.id,
      label: `${item.position}. ${item.title}`,
      hint: item.id === data.currentAgendaItemId ? 'Đang bàn' : notes.get(item.id)?.body ? 'Có ghi chú' : undefined,
      hintAccent: item.id === data.currentAgendaItemId,
    })),
  ]

  return (
    <div className="notes-editor">
      <div className="notes-toolbar">
        <Select
          value={options.some((o) => o.value === key) ? key : GENERAL}
          options={options}
          onChange={(value) => setPicked(value === current ? null : value)}
          label="Ghi chú cho"
          tooltip="Ghi chú theo mục đang bàn; chọn mục khác hoặc Chung để ghi riêng"
        />
      </div>

      <textarea
        className="notes-text-input"
        value={text}
        maxLength={noteMaxLength}
        readOnly={locked}
        aria-label="Nội dung ghi chú"
        placeholder="Gõ ghi chú… Dòng bắt đầu bằng “Kết luận:” hoặc “Nhiệm vụ:” vào mục III của biên bản."
        onChange={(e) => change(e.target.value)}
        onBlur={() => editing.current.text !== editing.current.saved && !locked && void save()}
      />

      <NoteStatus status={shown} length={text.length} onRetry={() => void save()} />

      <UnsentDrafts
        key={orphans.map((d) => d.noteKey).join()}
        initial={orphans}
        titles={titles}
        onDrop={(noteKey) => removeNoteDraft(scope, noteKey)}
      />

      {shown.kind === 'conflict' && (
        <div className="alert alert-warning notes-conflict" role="alert">
          <p>{STALE}</p>
          {shown.latest?.body ? <p className="notes-text notes-latest">{shown.latest.body}</p> : null}
          <div className="notes-conflict-actions">
            <button type="button" className="btn btn-outline btn-sm" onClick={takeLatest}>
              Dùng bản mới
            </button>
            <button type="button" className="btn btn-primary btn-sm" onClick={keepMine}>
              Giữ bản này
            </button>
          </div>
        </div>
      )}
      {shown.kind === 'refused' && (
        <div className="alert alert-error" role="alert">
          <p>{shown.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void reload()}>
            <Icon name="refresh" size={16} />
            Tải lại
          </button>
        </div>
      )}
    </div>
  )
}

function NoteStatus({ status, length, onRetry }: { status: Status; length: number; onRetry: () => void }) {
  const counter =
    length >= COUNTER_FROM ? (
      <span className="notes-counter">
        {length.toLocaleString('vi-VN')}/{noteMaxLength.toLocaleString('vi-VN')}
      </span>
    ) : null
  const line =
    status.kind === 'saving' ? (
      'Đang lưu…'
    ) : status.kind === 'dirty' ? (
      'Chưa lưu'
    ) : status.kind === 'saved' ? (
      <span tabIndex={0} data-tooltip={formatDateTime(status.at)}>
        <Icon name="check" size={14} /> {status.message} ({formatClock(status.at)})
      </span>
    ) : status.kind === 'offline' ? (
      OFFLINE
    ) : status.kind === 'failed' ? (
      <>
        {status.message}{' '}
        <button type="button" className="link-button" onClick={onRetry}>
          Thử lại
        </button>
      </>
    ) : null
  if (!line && !counter) return <p className="notes-status" aria-hidden="true" />
  return (
    <p
      className={`notes-status${status.kind === 'offline' || status.kind === 'failed' ? ' notes-status-warn' : ''}`}
      role="status"
    >
      <span>{line}</span>
      {counter}
    </p>
  )
}
