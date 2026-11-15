import { useEffect, useEffectEvent, useId, useState } from 'react'
import { acceptShare, changeShare, listShareCandidates, requestShare, shareDirectly } from '../../api/calendarSharing'
import { Badge } from '../../components/Badge'
import { DateRangeField, type DayRange } from '../../components/DateRangeField'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { errorMessage, isApiError } from '../../lib/errors'
import { isStale, type ShareOutcome } from './shareOutcome'
import type { Share, ShareCandidate, ShareLevel, ShareResult, ShareValues } from '../../types/calendarSharing'
import { Person } from '../participants/Person'
import {
  LEVEL_HINT,
  LEVEL_LABEL,
  PRESETS,
  addDaysTo,
  dayText,
  periodText,
  rangeError,
  vietnamToday,
  type RangeRules,
} from './shareFormat'

const SEARCH_DELAY_MS = 300

/** The radio rows for a level, each saying what the viewer would see (AI-eCabinet's "Mức xem"). */
function LevelPick({
  value,
  onChange,
  levels = ['Level1', 'Level2'],
}: {
  value: ShareLevel
  onChange: (level: ShareLevel) => void
  levels?: ShareLevel[]
}) {
  const name = useId()
  return (
    <fieldset className="choice-list">
      <legend>Mức xem</legend>
      {levels.map((level) => (
        <label key={level} className="choice-row">
          <input
            type="radio"
            name={name}
            checked={value === level}
            onChange={() => onChange(level)}
            aria-labelledby={`${name}-${level}`}
            aria-describedby={`${name}-${level}-desc`}
          />
          <span className="choice-text">
            <span id={`${name}-${level}`} className="choice-label">
              {LEVEL_LABEL[level]}
            </span>
            <span id={`${name}-${level}-desc`} className="choice-desc">
              {LEVEL_HINT[level]}
            </span>
          </span>
        </label>
      ))}
    </fieldset>
  )
}

/** The period: one calendar for both ends, quick lengths from the start, and BR-431 checked as the user picks. */
function RangePick({
  value,
  onChange,
  error,
  fixedStart,
  hint,
}: {
  value: DayRange
  onChange: (value: DayRange) => void
  error: string | null
  /** An Effective share keeps its start: the presets then count from it. */
  fixedStart?: string
  hint?: string
}) {
  const from = fixedStart ?? value.from
  return (
    <div className="field share-range">
      <span className="field-required">Thời gian chia sẻ</span>
      <DateRangeField
        value={value}
        onChange={(next) => onChange(fixedStart ? { from: fixedStart, to: next.to } : next)}
        invalid={!!error}
        labels={{ from: 'Ngày bắt đầu', to: 'Ngày kết thúc' }}
      />
      <div className="share-presets" role="group" aria-label="Chọn nhanh thời gian">
        {PRESETS.map((preset) => (
          <button
            key={preset.days}
            type="button"
            className="chip-toggle"
            aria-pressed={value.from === from && value.to === addDaysTo(from, preset.days - 1)}
            onClick={() => onChange({ from, to: addDaysTo(from, preset.days - 1) })}
          >
            {preset.label}
          </button>
        ))}
      </div>
      {error ? <small className="field-error">{error}</small> : hint && <small className="field-hint">{hint}</small>}
    </div>
  )
}

/** One person, picked from a server-searched list; people who already have a share are shown but cannot be picked. */
function PersonPick({
  purpose,
  value,
  onChange,
  onRefused,
  initialQuery = '',
}: {
  purpose: 'request' | 'direct'
  value: ShareCandidate | null
  onChange: (person: ShareCandidate) => void
  onRefused: (error: unknown) => void
  /** Starts the search with this text, e.g. the email of a person the dialog was opened for. */
  initialQuery?: string
}) {
  const [query, setQuery] = useState(initialQuery)
  const [page, setPage] = useState<{ items: ShareCandidate[]; total: number } | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const name = useId()
  // The caller usually passes an inline arrow: read the latest one without making each render search again.
  const refused = useEffectEvent((error: unknown) => onRefused(error))

  useEffect(() => {
    let cancelled = false
    const timer = window.setTimeout(async () => {
      try {
        const next = await listShareCandidates(purpose, query, 1, 20)
        if (cancelled) return
        setPage({ items: next.items, total: next.total })
        setLoadError(null)
      } catch (err) {
        if (cancelled) return
        if (isApiError(err, 403)) refused(err)
        else setLoadError(errorMessage(err))
      }
    }, SEARCH_DELAY_MS)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [purpose, query])

  return (
    <div className="field">
      <label className="field pick-search">
        <span className="field-required">{purpose === 'request' ? 'Xem lịch của' : 'Chia sẻ cho'}</span>
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Tìm theo tên hoặc email"
          maxLength={200}
        />
      </label>
      <div className="pick-list share-pick-list" role="radiogroup" aria-busy={page === null}>
        {loadError ? (
          <p className="pick-note pick-note-error">{loadError}</p>
        ) : page === null ? (
          <p className="pick-note">Đang tải…</p>
        ) : page.items.length === 0 ? (
          <p className="pick-note">
            {query.trim()
              ? 'Không có ai khớp.'
              : purpose === 'direct'
                ? 'Chưa có ai có quyền tạo cuộc họp để chia sẻ.'
                : 'Chưa có ai để xin xem lịch.'}
          </p>
        ) : (
          page.items.map((person) => (
            <label
              key={person.id}
              className={`pick-row${person.hasOpenShare ? ' pick-row-off' : ''}`}
              data-tooltip={person.hasOpenShare ? 'Đã có chia sẻ chờ hoặc đang chia sẻ với người này.' : undefined}
            >
              <input
                type="radio"
                name={name}
                checked={value?.id === person.id}
                disabled={person.hasOpenShare}
                onChange={() => onChange(person)}
              />
              <Person name={person.fullName} email={person.email} />
              {person.hasOpenShare && (
                <span className="pick-row-tag">
                  <Badge tone="slate">Đã có</Badge>
                </span>
              )}
            </label>
          ))
        )}
      </div>
      {page && page.total > page.items.length && (
        <small className="field-hint">
          Hiện {page.items.length}/{page.total} người. Gõ tên hoặc email để tìm người khác.
        </small>
      )}
    </div>
  )
}

/** Shared footer and submit state of the form dialogs below. */
function useSubmit(onDone: (outcome: ShareOutcome) => void) {
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  async function run(call: () => Promise<ShareResult>) {
    setPending(true)
    setError(null)
    try {
      onDone({ kind: 'saved', result: await call() })
    } catch (err) {
      setPending(false)
      if (isStale(err)) onDone({ kind: 'stale', error: err })
      else setError(errorMessage(err))
    }
  }
  return { pending, error, run }
}

function Footer({
  pending,
  disabled,
  label,
  pendingLabel,
  icon,
  onCancel,
  onSubmit,
}: {
  pending: boolean
  disabled: boolean
  label: string
  pendingLabel: string
  icon: 'send' | 'check' | 'calendarCheck'
  onCancel: () => void
  onSubmit: () => void
}) {
  return (
    <>
      <button type="button" className="btn btn-outline" onClick={onCancel} disabled={pending}>
        Huỷ
      </button>
      <button type="button" className="btn btn-primary" onClick={onSubmit} disabled={pending || disabled}>
        <Icon name={icon} size={16} />
        {pending ? pendingLabel : label}
      </button>
    </>
  )
}

/**
 * UC-84a (`request`): a Meeting Organizer asks to see someone's busy hours at a level, over a period.
 * UC-84b (`direct`): anyone shares their own calendar with a Meeting Organizer, always "Họp Thường"; the recipient
 * accepts it, and may later ask for more (UC-84d).
 */
export function NewShareDialog({
  mode,
  onClose,
  onDone,
  initialPerson,
}: {
  mode: 'request' | 'direct'
  onClose: () => void
  onDone: (outcome: ShareOutcome) => void
  /** Opened for one person (SCRUM-137: "Yêu cầu chia sẻ lịch" on the find-a-time page): picked and searched for. */
  initialPerson?: { id: string; fullName: string; email: string }
}) {
  const today = vietnamToday()
  const [person, setPerson] = useState<ShareCandidate | null>(() =>
    initialPerson ? { ...initialPerson, hasOpenShare: false } : null,
  )
  const [level, setLevel] = useState<ShareLevel>('Level1')
  const [range, setRange] = useState<DayRange>({ from: today, to: addDaysTo(today, 29) })
  const { pending, error, run } = useSubmit(onDone)
  const rules: RangeRules = { minStart: today, today }
  const problem = rangeError(range.from, range.to, rules)
  const close = () => !pending && onClose()

  function submit() {
    if (!person || problem) return
    void run(() =>
      mode === 'request'
        ? requestShare({ ownerId: person.id, level, startDate: range.from, endDate: range.to })
        : shareDirectly({ viewerId: person.id, startDate: range.from, endDate: range.to }),
    )
  }

  return (
    <Dialog
      title={mode === 'request' ? 'Xin xem lịch' : 'Chia sẻ lịch của tôi'}
      description={
        mode === 'request'
          ? 'Người được hỏi đồng ý thì bạn thấy giờ bận của họ, không thấy tên hay nội dung cuộc họp.'
          : 'Người nhận thấy giờ bận của bạn ở các cuộc họp Thường, không thấy tên hay nội dung cuộc họp.'
      }
      onClose={close}
      footer={
        <Footer
          pending={pending}
          disabled={!person || !!problem}
          label={mode === 'request' ? 'Gửi yêu cầu' : 'Chia sẻ'}
          pendingLabel="Đang gửi…"
          icon="send"
          onCancel={close}
          onSubmit={submit}
        />
      }
    >
      <div className="form-stack">
        <PersonPick
          purpose={mode}
          initialQuery={initialPerson?.email}
          value={person}
          onChange={setPerson}
          onRefused={(err) => onDone({ kind: 'stale', error: err })}
        />
        {mode === 'request' && <LevelPick value={level} onChange={setLevel} />}
        <RangePick value={range} onChange={setRange} error={problem} />
        {error && (
          <p className="alert alert-error" role="alert">
            {error}
          </p>
        )}
      </div>
    </Dialog>
  )
}

/**
 * UC-84c — accept what is waiting for the caller:
 * - a request (UC-84a) to the owner, who may grant a lower level or a shorter period inside the one asked;
 * - a direct share (UC-84b) to the viewer, taken as it is;
 * - the viewer's pending change (UC-84d) to the owner, granted as asked.
 */
export function AcceptShareDialog({
  share,
  onClose,
  onDone,
}: {
  share: Share
  onClose: () => void
  onDone: (outcome: ShareOutcome) => void
}) {
  const today = vietnamToday()
  const change = share.status === 'Effective' ? share.pendingChange : null
  const narrowable = !change && share.initiatedBy === 'Viewer'
  const asked = { from: share.startDate, to: share.endDate }
  const [level, setLevel] = useState<ShareLevel>(share.level)
  // A request asked from a day already past starts today at the earliest.
  const [range, setRange] = useState<DayRange>({ from: asked.from < today ? today : asked.from, to: asked.to })
  const { pending, error, run } = useSubmit(onDone)
  const problem = narrowable ? rangeError(range.from, range.to, { minStart: today, today, within: asked }) : null
  const close = () => !pending && onClose()

  function submit() {
    if (problem) return
    const values: ShareValues | undefined = narrowable ? { level, startDate: range.from, endDate: range.to } : undefined
    void run(() => acceptShare(share.id, values))
  }

  // A pending change is always the viewer's (UC-84c AF-1), whichever side started the share.
  const sender = change || share.initiatedBy === 'Viewer' ? share.viewer : share.owner
  return (
    <Dialog
      title={change ? 'Đồng ý thay đổi' : share.initiatedBy === 'Viewer' ? 'Cho xem lịch' : 'Nhận chia sẻ lịch'}
      onClose={close}
      footer={
        <Footer
          pending={pending}
          disabled={!!problem}
          label="Đồng ý"
          pendingLabel="Đang lưu…"
          icon="check"
          onCancel={close}
          onSubmit={submit}
        />
      }
    >
      <div className="form-stack">
        <Person name={sender.fullName} email={sender.email} />
        {change ? (
          <dl className="share-facts">
            <dt>Mức xem</dt>
            <dd>
              {LEVEL_LABEL[share.level]}
              {change.level !== share.level && (
                <>
                  {' '}
                  → <strong>{LEVEL_LABEL[change.level]}</strong>
                </>
              )}
            </dd>
            <dt>Đến ngày</dt>
            <dd>
              {dayText(share.endDate)}
              {change.endDate !== share.endDate && (
                <>
                  {' '}
                  → <strong>{dayText(change.endDate)}</strong>
                </>
              )}
            </dd>
          </dl>
        ) : narrowable ? (
          <>
            <p className="field-hint">
              Đã xin: {LEVEL_LABEL[share.level]}, {periodText(share.startDate, share.endDate)}. Bạn có thể cho xem ít
              hơn.
            </p>
            {share.level === 'Level2' && <LevelPick value={level} onChange={setLevel} />}
            <RangePick value={range} onChange={setRange} error={problem} hint="Chỉ chọn trong khoảng đã xin." />
          </>
        ) : (
          <dl className="share-facts">
            <dt>Mức xem</dt>
            <dd>{LEVEL_LABEL[share.level]}</dd>
            <dt>Thời gian</dt>
            <dd>{periodText(share.startDate, share.endDate)}</dd>
          </dl>
        )}
        {error && (
          <p className="alert alert-error" role="alert">
            {error}
          </p>
        )}
      </div>
    </Dialog>
  )
}

/**
 * UC-84d — the viewer asks for more (a higher level, a later end; the owner answers), or the owner changes the share at
 * once (a lower level, other dates from today). The start of an Effective share stays put for the viewer.
 */
export function ChangeShareDialog({
  share,
  as,
  onClose,
  onDone,
}: {
  share: Share
  as: 'viewer' | 'owner'
  onClose: () => void
  onDone: (outcome: ShareOutcome) => void
}) {
  const today = vietnamToday()
  const [level, setLevel] = useState<ShareLevel>(share.level)
  const [range, setRange] = useState<DayRange>({ from: share.startDate, to: share.endDate })
  const { pending, error, run } = useSubmit(onDone)
  const viewer = as === 'viewer'

  // The viewer only raises or extends; the owner only lowers, and moves the start to today at the earliest.
  const levels: ShareLevel[] = viewer ? ['Level1', 'Level2'] : share.level === 'Level2' ? ['Level1', 'Level2'] : []
  const rules: RangeRules = viewer
    ? { minStart: share.startDate, fixedStart: share.startDate, today }
    : { minStart: share.startDate < today ? share.startDate : today, today }
  let problem = rangeError(range.from, range.to, rules)
  if (!problem && viewer && level === share.level && range.to <= share.endDate)
    problem = 'Chọn mức xem cao hơn hoặc ngày kết thúc muộn hơn.'
  if (!problem && viewer && level === 'Level1' && share.level === 'Level2') problem = 'Mức xem chỉ nâng lên được.'
  if (!problem && !viewer && range.from !== share.startDate && range.from < today)
    problem = 'Ngày bắt đầu mới không được trước hôm nay.'
  const unchanged = level === share.level && range.from === share.startDate && range.to === share.endDate
  const close = () => !pending && onClose()

  function submit() {
    if (problem || unchanged) return
    const values: ShareValues = {}
    if (level !== share.level) values.level = level
    if (range.from !== share.startDate) values.startDate = range.from
    if (range.to !== share.endDate) values.endDate = range.to
    void run(() => changeShare(share.id, values))
  }

  const other = viewer ? share.owner : share.viewer
  return (
    <Dialog
      title={viewer ? 'Xin thay đổi' : 'Đổi chia sẻ'}
      description={
        viewer
          ? `${other.fullName} đồng ý thì thay đổi mới có hiệu lực; đến lúc đó chia sẻ vẫn như cũ.`
          : `Có hiệu lực ngay và ${other.fullName} được báo.`
      }
      onClose={close}
      footer={
        <Footer
          pending={pending}
          disabled={!!problem || unchanged}
          label={viewer ? 'Gửi yêu cầu' : 'Lưu thay đổi'}
          pendingLabel={viewer ? 'Đang gửi…' : 'Đang lưu…'}
          icon={viewer ? 'send' : 'check'}
          onCancel={close}
          onSubmit={submit}
        />
      }
    >
      <div className="form-stack">
        <p className="field-hint">
          Đang chia sẻ: {LEVEL_LABEL[share.level]}, {periodText(share.startDate, share.endDate)}.
        </p>
        {levels.length > 0 && (viewer ? share.level === 'Level1' : true) && (
          <LevelPick value={level} onChange={setLevel} levels={levels} />
        )}
        <RangePick
          value={range}
          onChange={setRange}
          error={unchanged ? null : problem}
          fixedStart={viewer ? share.startDate : undefined}
          hint={viewer ? 'Ngày bắt đầu giữ nguyên; chỉ kéo dài được ngày kết thúc.' : undefined}
        />
        {error && (
          <p className="alert alert-error" role="alert">
            {error}
          </p>
        )}
      </div>
    </Dialog>
  )
}
