import { useCallback, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { castBallot, closeVote, deleteVote, detachVoteDocument, openVote, recordVoteConclusion } from '../../api/votes'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { SegmentedControl } from '../../components/SegmentedControl'
import { useToast } from '../../components/toastContext'
import { formatClock, formatDateTime } from '../../lib/format'
import { routes } from '../../lib/routes'
import {
  VOTE_CONCLUSION_LABEL,
  VOTE_ROLE_LABEL,
  type VoteConclusionValue,
  type VoteDetail,
  type VoteDocument,
  type VoteMutationResponse,
  type VoteSummary,
} from '../../types/vote'
import type { Meeting } from '../../types/meeting'
import { AddDocumentsDialog, type DocumentSource } from '../documents/AddDocumentsDialog'
import { AttachMenu } from '../documents/AttachMenu'
import { useMeetingDocuments } from '../documents/useMeetingDocuments'
import { useVoteDetail, useVotes } from './useVotes'
import { VoteDialog } from './VoteDialog'

const NOTE_MAX = 2000

type Open =
  | { kind: 'edit'; voteId: string | null }
  | { kind: 'delete'; vote: VoteSummary }
  | { kind: 'open'; vote: VoteSummary }
  | { kind: 'close'; vote: VoteSummary }
  | { kind: 'attach'; vote: VoteSummary; source: DocumentSource }
  | { kind: 'detach'; vote: VoteSummary; doc: VoteDocument }

/**
 * The meeting's votes (SCRUM-28, UC-52a–UC-57), written once for the detail tab and the call room's
 * side panel (ADR-0011). It reads its own data and listens on `/hubs/meetings` itself; what the caller
 * may do comes from the server's `permissions` and `actions`. The Chair or the Secretary prepares
 * drafts; the Chair opens one at a time while the meeting runs, closes it and records the conclusion;
 * everyone on the list fixed at opening casts one ballot. One line per vote; a vote waiting on the
 * caller starts opened, the rest open on demand. On the page, the Chair and the Secretary attach documents
 * to a draft from its paperclip and detach them (UC-76a, UC-76b, SCRUM-96), which needs the meeting itself.
 */
export function VotingPanel({
  meetingId,
  variant,
  meeting,
}: {
  meetingId: string
  variant: 'page' | 'room'
  /** The page's meeting, for attaching documents; the room attaches none, as for agenda items (BR-316). */
  meeting?: Meeting
}) {
  const toast = useToast()
  const { state, reload, run } = useVotes(meetingId)
  const [open, setOpen] = useState<Open | null>(null)
  const [toggled, setToggled] = useState<ReadonlyMap<string, boolean>>(new Map())

  const saved = useCallback(
    (response: VoteMutationResponse) => {
      toast.show(response.message)
      void reload()
    },
    [toast, reload],
  )
  const stale = useCallback(() => void reload(), [reload])

  const room = variant === 'room'
  const wrap = (body: ReactNode) =>
    room ? (
      <div className="room-panel-body vote-room">
        <h2 className="room-panel-title">Biểu quyết</h2>
        {body}
      </div>
    ) : (
      body
    )

  if (state.kind === 'gone') {
    return wrap(
      <div className="card card-pending tab-pending">
        <p>Bạn không còn xem được biểu quyết của cuộc họp này.</p>
      </div>,
    )
  }
  const data = state.kind === 'loading' ? null : state.data
  if (!data) {
    return wrap(
      state.kind === 'error' ? (
        <div className="alert alert-error" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void reload()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      ) : (
        <VoteSkeleton />
      ),
    )
  }

  const { votes, permissions } = data
  // A vote that waits on the caller — open, or closed without the Chair's conclusion — starts opened, also
  // when it gets there while the panel is on screen; one the caller opened or closed by hand stays so.
  const waitsOnMe = (vote: VoteSummary) => vote.status === 'Open' || vote.actions.canRecordConclusion
  const isOpen = (vote: VoteSummary) => toggled.get(vote.id) ?? waitsOnMe(vote)
  const toggle = (vote: VoteSummary) => setToggled((map) => new Map(map).set(vote.id, !isOpen(vote)))
  // What the caller just did stays in view, though the vote no longer waits on them.
  const keepOpen = (id: string) => setToggled((map) => new Map(map).set(id, true))

  return wrap(
    <section className={room ? 'vote-panel vote-panel-room' : 'list-panel vote-panel'} aria-label="Biểu quyết">
      {permissions.canManageDraft && (
        <div className="toolbar vote-toolbar">
          <button
            type="button"
            className="btn btn-outline btn-sm toolbar-end"
            onClick={() => setOpen({ kind: 'edit', voteId: null })}
          >
            <Icon name="plus" size={16} />
            Tạo biểu quyết
          </button>
        </div>
      )}

      {state.kind === 'error' && (
        <div className="alert alert-error vote-alert" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void reload()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}

      {votes.length === 0 ? (
        // MSG207: shown by the FE (UC-52a AF-1).
        <div className="agenda-empty vote-empty">
          <Icon name="vote" size={24} />
          <p>Không có biểu quyết nào để hiển thị.</p>
          {permissions.canManageDraft && <small>Tạo biểu quyết để chuẩn bị trước, mở khi đang họp.</small>}
        </div>
      ) : (
        <ol className="vote-list">
          {votes.map((vote) => (
            <VoteCard
              key={vote.id}
              meetingId={meetingId}
              room={room}
              vote={vote}
              stamp={data.serverTime}
              expanded={isOpen(vote)}
              onToggle={() => toggle(vote)}
              onEdit={() => setOpen({ kind: 'edit', voteId: vote.id })}
              onDelete={() => setOpen({ kind: 'delete', vote })}
              onOpen={() => setOpen({ kind: 'open', vote })}
              onClose={() => setOpen({ kind: 'close', vote })}
              onCast={(optionId) => {
                keepOpen(vote.id)
                return run(() => castBallot(meetingId, vote.id, optionId))
              }}
              onConclude={(value, note) => {
                keepOpen(vote.id)
                return run(() => recordVoteConclusion(meetingId, vote.id, value, note))
              }}
              onAttach={
                meeting && !room && vote.actions.canAttachDocuments
                  ? (source) => setOpen({ kind: 'attach', vote, source })
                  : undefined
              }
              onDetach={
                meeting && !room
                  ? (doc) => {
                      keepOpen(vote.id)
                      setOpen({ kind: 'detach', vote, doc })
                    }
                  : undefined
              }
            />
          ))}
        </ol>
      )}

      {open?.kind === 'attach' && meeting && (
        <VoteAttachDialog
          meeting={meeting}
          vote={open.vote}
          source={open.source}
          onClose={() => setOpen(null)}
          onAdded={(message) => {
            keepOpen(open.vote.id)
            setOpen(null)
            toast.show(message)
            void reload()
          }}
          onStale={(message) => {
            setOpen(null)
            toast.show(message, 'error')
            void reload()
          }}
        />
      )}
      {open?.kind === 'detach' && (
        <ConfirmDialog
          title="Gỡ tài liệu khỏi biểu quyết"
          tone="danger"
          confirmLabel="Gỡ"
          pendingLabel="Đang gỡ…"
          onConfirm={async () => {
            try {
              saved(await detachVoteDocument(meetingId, open.vote.id, open.doc.id))
            } catch (err) {
              stale()
              throw err
            }
          }}
          onClose={() => setOpen(null)}
        >
          Gỡ “{open.doc.fileName}” khỏi biểu quyết? Tài liệu vẫn ở trong cuộc họp.
        </ConfirmDialog>
      )}

      {open?.kind === 'edit' && (
        <VoteDialog
          meetingId={meetingId}
          voteId={open.voteId}
          canOpen={permissions.canOpen}
          onSaved={saved}
          onStale={stale}
          onClose={() => setOpen(null)}
        />
      )}
      {open?.kind === 'delete' && (
        <ConfirmDialog
          title="Xoá biểu quyết nháp"
          tone="danger"
          confirmLabel="Xoá"
          pendingLabel="Đang xoá…"
          onConfirm={async () => {
            // A failure stays in the dialog (it would cover a toast); the list re-reads behind it.
            try {
              saved(await deleteVote(meetingId, open.vote.id))
            } catch (err) {
              stale()
              throw err
            }
          }}
          onClose={() => setOpen(null)}
        >
          Xoá nháp “{open.vote.title}”?
        </ConfirmDialog>
      )}
      {open?.kind === 'open' && (
        <ConfirmDialog
          title="Mở biểu quyết"
          confirmLabel="Mở biểu quyết"
          pendingLabel="Đang mở…"
          onConfirm={async () => {
            try {
              saved(await openVote(meetingId, open.vote.id))
            } catch (err) {
              stale()
              throw err
            }
          }}
          onClose={() => setOpen(null)}
        >
          Mở “{open.vote.title}”? Người bỏ phiếu ({open.vote.eligibleRoles.map((r) => VOTE_ROLE_LABEL[r]).join(', ')})
          chốt ngay lúc mở.
        </ConfirmDialog>
      )}
      {open?.kind === 'close' && (
        <ConfirmDialog
          title="Đóng biểu quyết"
          confirmLabel="Đóng phiếu"
          pendingLabel="Đang đóng…"
          onConfirm={async () => {
            try {
              keepOpen(open.vote.id)
              saved(await closeVote(meetingId, open.vote.id))
            } catch (err) {
              stale()
              throw err
            }
          }}
          onClose={() => setOpen(null)}
        >
          Đóng “{open.vote.title}”? Đã bỏ {open.vote.receiptCount}/{open.vote.eligibleCount}; đóng rồi không ai bỏ thêm
          được.
        </ConfirmDialog>
      )}
    </section>,
  )
}

function VoteCard({
  meetingId,
  room,
  vote,
  stamp,
  expanded,
  onToggle,
  onEdit,
  onDelete,
  onOpen,
  onClose,
  onCast,
  onConclude,
  onAttach,
  onDetach,
}: {
  meetingId: string
  room: boolean
  vote: VoteSummary
  /** The list's `serverTime`: the detail re-reads whenever the list did. */
  stamp: string
  expanded: boolean
  onToggle: () => void
  onEdit: () => void
  onDelete: () => void
  onOpen: () => void
  onClose: () => void
  onCast: (optionId: string) => Promise<boolean>
  onConclude: (value: VoteConclusionValue, note: string) => Promise<boolean>
  /** The paperclip of a draft the caller may attach to (UC-76a); absent otherwise. */
  onAttach?: (source: DocumentSource) => void
  /** Detach one document (UC-76b); offered only where the server says documents may change. */
  onDetach?: (doc: VoteDocument) => void
}) {
  const { actions } = vote
  const bodyId = `vote-body-${vote.id}`
  const classes = ['vote-item']
  if (vote.status === 'Open') classes.push('vote-item-open')
  if (vote.status === 'Draft') classes.push('vote-item-draft')

  return (
    <li className={classes.join(' ')}>
      <div className="vote-head">
        <button type="button" className="vote-title" aria-expanded={expanded} aria-controls={bodyId} onClick={onToggle}>
          <Icon name={expanded ? 'chevronDown' : 'chevronRight'} size={16} />
          <span>{vote.title}</span>
        </button>

        {(actions.canOpen || actions.canEdit || actions.canDelete || actions.canClose || onAttach) && (
          <div className="row-actions vote-actions">
            {actions.canOpen && (
              <button type="button" className="btn btn-primary btn-sm" onClick={onOpen}>
                <Icon name="play" size={16} />
                Mở
              </button>
            )}
            {actions.canClose && (
              <button type="button" className="btn btn-outline btn-sm" onClick={onClose}>
                <Icon name="stop" size={16} />
                Đóng phiếu
              </button>
            )}
            {onAttach && <AttachMenu title={vote.title} onPick={onAttach} />}
            {actions.canEdit && (
              <button
                type="button"
                className="icon-button icon-button-sm"
                onClick={onEdit}
                aria-label={`Sửa: ${vote.title}`}
                data-tooltip="Sửa"
              >
                <Icon name="pencil" size={16} />
              </button>
            )}
            {actions.canDelete && (
              <button
                type="button"
                className="icon-button icon-button-sm icon-button-danger"
                onClick={onDelete}
                aria-label={`Xoá: ${vote.title}`}
                data-tooltip="Xoá"
              >
                <Icon name="trash" size={16} />
              </button>
            )}
          </div>
        )}

        <span className="vote-break" aria-hidden="true" />
        <div className="vote-meta">
          <VoteStatusChip vote={vote} />
          <span
            className="vote-fact"
            tabIndex={0}
            data-tooltip={
              vote.isAnonymous ? 'Không lưu ai chọn gì, chỉ có tổng số phiếu' : 'Sau khi đóng, xem được ai chọn gì'
            }
          >
            <Icon name={vote.isAnonymous ? 'lock' : 'globe'} size={14} />
            {vote.isAnonymous ? 'Kín' : 'Công khai'}
          </span>
          {vote.status !== 'Draft' && (
            <span className="vote-fact vote-turnout-text" tabIndex={0} data-tooltip="Đã bỏ phiếu / người đủ điều kiện">
              <Icon name="users" size={14} />
              {vote.receiptCount}/{vote.eligibleCount}
            </span>
          )}
          {vote.hasVoted && vote.status === 'Open' && (
            <span className="vote-fact vote-voted">
              <Icon name="check" size={14} />
              Đã bỏ phiếu
            </span>
          )}
          {vote.conclusion && <ConclusionChip value={vote.conclusion.value} />}
          {vote.documentCount > 0 && (
            <span className="vote-fact" tabIndex={0} data-tooltip="Tài liệu của biểu quyết">
              <Icon name="paperclip" size={14} />
              {vote.documentCount}
            </span>
          )}
        </div>
      </div>

      {vote.status === 'Open' && (
        <Turnout cast={vote.receiptCount} eligible={vote.eligibleCount} label={`Đã bỏ phiếu: ${vote.title}`} />
      )}

      {expanded && (
        <VoteBody
          id={bodyId}
          meetingId={meetingId}
          room={room}
          vote={vote}
          stamp={stamp}
          onCast={onCast}
          onConclude={onConclude}
          onDetach={onDetach}
        />
      )}
    </li>
  )
}

/** The open vote's turnout, as a bar; the numbers are the chip's. */
function Turnout({ cast, eligible, label }: { cast: number; eligible: number; label: string }) {
  return (
    <div className="vote-turnout">
      <progress className="vote-bar" max={Math.max(eligible, 1)} value={cast} aria-label={label} />
    </div>
  )
}

function VoteBody({
  id,
  meetingId,
  room,
  vote,
  stamp,
  onCast,
  onConclude,
  onDetach,
}: {
  id: string
  meetingId: string
  room: boolean
  vote: VoteSummary
  stamp: string
  onCast: (optionId: string) => Promise<boolean>
  onConclude: (value: VoteConclusionValue, note: string) => Promise<boolean>
  onDetach?: (doc: VoteDocument) => void
}) {
  const { state, reload } = useVoteDetail(meetingId, vote.id, stamp, true)

  if (state.kind === 'gone') {
    return (
      <div className="vote-body" id={id}>
        <p className="value-empty">Biểu quyết này không còn xem được.</p>
      </div>
    )
  }
  const detail = state.kind === 'loading' ? null : state.data
  if (!detail) {
    return (
      <div className="vote-body" id={id} aria-busy={state.kind === 'loading'}>
        {state.kind === 'error' ? (
          <div className="alert alert-error" role="alert">
            <p>{state.message}</p>
            <button type="button" className="btn btn-outline btn-sm" onClick={() => void reload()}>
              <Icon name="refresh" size={16} />
              Thử lại
            </button>
          </div>
        ) : (
          <span className="skeleton vote-skeleton" />
        )}
      </div>
    )
  }

  return (
    <div className="vote-body" id={id}>
      {detail.description && <p className="vote-text">{detail.description}</p>}
      <p className="vote-roles">
        <span className="vote-roles-label">Được bỏ phiếu</span>
        <span className="chip-row">
          {detail.eligibleRoles.map((role) => (
            <span key={role} className="badge badge-indigo">
              {VOTE_ROLE_LABEL[role]}
            </span>
          ))}
        </span>
        <Times detail={detail} />
      </p>

      {detail.documents.length > 0 && (
        <VoteDocuments
          meetingId={meetingId}
          room={room}
          detail={detail}
          // Only while the documents may still change: a draft the caller manages (BR-288).
          onDetach={detail.actions.canAttachDocuments ? onDetach : undefined}
        />
      )}

      {detail.status === 'Draft' && (
        <ol className="vote-option-list">
          {detail.options.map((option) => (
            <li key={option.id}>{option.label}</li>
          ))}
        </ol>
      )}
      {detail.status === 'Open' && <Ballot key={detail.id} detail={detail} onCast={onCast} />}
      {detail.status === 'Closed' && detail.result && (
        <>
          <Result detail={detail} />
          {detail.conclusion ? (
            <Conclusion detail={detail} />
          ) : detail.actions.canRecordConclusion ? (
            <ConclusionForm onConclude={onConclude} />
          ) : (
            <p className="value-empty vote-pending">Chưa có kết luận.</p>
          )}
        </>
      )}
    </div>
  )
}

/** When it opened and closed, by the clock; the full date on hover. */
function Times({ detail }: { detail: VoteDetail }) {
  if (!detail.openedAt) return null
  const full = `Mở ${formatDateTime(detail.openedAt)}${detail.closedAt ? `\nĐóng ${formatDateTime(detail.closedAt)}` : ''}`
  return (
    <time className="vote-times" dateTime={detail.openedAt} tabIndex={0} data-tooltip={full}>
      <Icon name="clock" size={14} />
      {formatClock(detail.openedAt)}
      {detail.closedAt && ` – ${formatClock(detail.closedAt)}`}
    </time>
  )
}

/**
 * The vote's documents, each opening the viewer (a new tab in the room, so the call keeps running).
 * A voter must have confirmed reading each one before the ballot is accepted (MSG252, UC-74): one not
 * read yet opens with `read=1`, and the viewer offers the confirmation once its last page is reached.
 * The panel re-reads when the window regains focus, so coming back unlocks "Gửi phiếu".
 */
function VoteDocuments({
  meetingId,
  room,
  detail,
  onDetach,
}: {
  meetingId: string
  room: boolean
  detail: VoteDetail
  onDetach?: (doc: VoteDocument) => void
}) {
  const voter = detail.status === 'Open' && detail.isEligible && !detail.hasVoted
  const linkTo = (id: string, read: boolean) => {
    const query = new URLSearchParams()
    if (room) query.set('from', 'room')
    if (read) query.set('read', '1')
    const search = query.toString()
    return `${routes.meetingDocument(meetingId, id)}${search ? `?${search}` : ''}`
  }
  return (
    <ul className="agenda-doc-list vote-docs">
      {detail.documents.map((doc) => (
        <li key={doc.id}>
          <Link to={linkTo(doc.id, voter && !doc.readConfirmedAt)} {...(room && { target: '_blank', rel: 'noopener' })}>
            <Icon name="fileText" size={16} />
            <span>{doc.fileName}</span>
          </Link>
          {voter && (
            <span className={doc.readConfirmedAt ? 'vote-read' : 'vote-read vote-unread'}>
              {doc.readConfirmedAt ? 'Đã đọc' : 'Chưa đọc'}
            </span>
          )}
          {onDetach && (
            <button
              type="button"
              className="icon-button icon-button-sm vote-doc-detach"
              onClick={() => onDetach(doc)}
              aria-label={`Gỡ khỏi biểu quyết: ${doc.fileName}`}
              data-tooltip="Gỡ khỏi biểu quyết"
            >
              <Icon name="unlink" size={16} />
            </button>
          )}
        </li>
      ))}
    </ul>
  )
}

/**
 * UC-76a for one draft, with the meeting's documents for "Trong cuộc họp" and the vote's own (so they are
 * not offered twice) read while it is open. Files from the computer go to the upload queue; the panel hears
 * `voteChanged` when each lands.
 */
function VoteAttachDialog({
  meeting,
  vote,
  source,
  onClose,
  onAdded,
  onStale,
}: {
  meeting: Meeting
  vote: VoteSummary
  source: DocumentSource
  onClose: () => void
  onAdded: (message: string) => void
  onStale: (message: string) => void
}) {
  const { state, reload } = useMeetingDocuments(meeting.id)
  const attached = useVoteDetail(meeting.id, vote.id, 'attach', true).state
  const data = state.kind === 'loaded' || state.kind === 'error' ? state.data : null
  // A document linked to several agenda items is listed once; a failed read is never an empty list.
  const documents = data
    ? [...new Map([...data.agendaItems.flatMap((i) => i.documents), ...data.general].map((d) => [d.id, d])).values()]
    : null
  const loadError =
    state.kind === 'gone'
      ? 'Bạn không còn xem được tài liệu của cuộc họp này.'
      : state.kind === 'error' && !data
        ? state.message
        : null

  return (
    <AddDocumentsDialog
      meeting={meeting}
      vote={{
        id: vote.id,
        title: vote.title,
        documentIds: attached.kind === 'loaded' ? attached.data.documents.map((d) => d.id) : [],
      }}
      meetingDocuments={attached.kind === 'loaded' || attached.kind === 'error' ? documents : null}
      meetingDocumentsError={loadError ? { message: loadError, retry: () => void reload() } : null}
      initialSource={source}
      onClose={onClose}
      onAdded={onAdded}
      // The upload queue's tray follows the files; `voteChanged` re-reads the panel when they land.
      onQueued={onClose}
      onStale={onStale}
    />
  )
}

/** UC-55: one choice, sent once. A secret vote never shows back what was chosen. */
function Ballot({ detail, onCast }: { detail: VoteDetail; onCast: (optionId: string) => Promise<boolean> }) {
  const [choice, setChoice] = useState<string | null>(null)
  const [sending, setSending] = useState(false)

  if (detail.hasVoted) {
    return (
      <p className="vote-state vote-state-done">
        <Icon name="checkCircle" size={16} />
        Bạn đã gửi phiếu.
      </p>
    )
  }
  if (!detail.isEligible) {
    return <p className="vote-state">Bạn không có trong danh sách bỏ phiếu.</p>
  }
  if (!detail.actions.canCast) {
    return <p className="vote-state">Biểu quyết không còn nhận phiếu.</p>
  }

  const unread = detail.documents.some((d) => !d.readConfirmedAt)
  const name = `ballot-${detail.id}`
  return (
    <form
      className="vote-ballot"
      onSubmit={async (event) => {
        event.preventDefault()
        if (!choice) return
        setSending(true)
        await onCast(choice)
        setSending(false)
      }}
    >
      <fieldset className="vote-choices" disabled={sending}>
        <legend className="visually-hidden">Chọn một phương án</legend>
        {detail.options.map((option) => (
          <label key={option.id} className="vote-choice">
            <input
              type="radio"
              name={name}
              value={option.id}
              checked={choice === option.id}
              onChange={() => setChoice(option.id)}
            />
            <span>{option.label}</span>
          </label>
        ))}
      </fieldset>
      <div className="vote-ballot-foot">
        <small className="field-hint">
          {unread ? 'Mở từng tài liệu, đọc tới trang cuối rồi xác nhận.' : 'Gửi rồi không đổi, không rút được.'}
        </small>
        <button type="submit" className="btn btn-primary btn-sm" disabled={!choice || sending || unread}>
          <Icon name="send" size={16} />
          {sending ? 'Đang gửi…' : 'Gửi phiếu'}
        </button>
      </div>
    </form>
  )
}

/** UC-56: the tally per option; percentages of the ballots cast, none when nobody voted (BR-261). */
function Result({ detail }: { detail: VoteDetail }) {
  const result = detail.result!
  const labelOf = new Map(detail.options.map((o) => [o.id, o.label]))
  return (
    <>
      <ul className="vote-result">
        {result.options.map((option) => (
          <li key={option.optionId}>
            <span className="vote-result-label">{option.label}</span>
            <progress
              className="vote-bar"
              max={Math.max(result.ballotCount, 1)}
              value={option.count}
              aria-label={option.label}
            />
            <span className="vote-result-count">
              {option.count}
              {option.percentage !== null && <small> · {option.percentage.toLocaleString('vi-VN')}%</small>}
            </span>
          </li>
        ))}
      </ul>
      {result.voters && (
        <details className="vote-voters">
          <summary>
            Phiếu từng người ({result.voters.filter((v) => v.optionId).length}/{result.voters.length})
          </summary>
          <ul>
            {result.voters.map((voter) => (
              <li key={voter.id}>
                <span>
                  {voter.displayName}
                  {voter.kind === 'Guest' && <small> (khách)</small>}
                </span>
                {voter.optionId ? (
                  <strong>{labelOf.get(voter.optionId)}</strong>
                ) : (
                  <span className="value-empty">Không bỏ phiếu</span>
                )}
              </li>
            ))}
          </ul>
        </details>
      )}
    </>
  )
}

function ConclusionChip({ value }: { value: VoteConclusionValue }) {
  const tone = value === 'Passed' ? 'status-emerald' : value === 'NotPassed' ? 'status-red' : 'status-amber'
  const icon = value === 'Passed' ? 'checkCircle' : value === 'NotPassed' ? 'xCircle' : 'clock'
  return (
    <span className={`status ${tone} vote-conclusion-chip`}>
      <Icon name={icon} size={14} />
      {VOTE_CONCLUSION_LABEL[value]}
    </span>
  )
}

/** UC-57, recorded: read-only for good (BR-262). */
function Conclusion({ detail }: { detail: VoteDetail }) {
  const conclusion = detail.conclusion!
  const who = [conclusion.concludedByName, conclusion.concludedAt && formatDateTime(conclusion.concludedAt)]
    .filter(Boolean)
    .join(' · ')
  return (
    <div className="vote-box">
      <h4>Kết luận</h4>
      <p className="vote-conclusion" tabIndex={0} data-tooltip={who || undefined}>
        <ConclusionChip value={conclusion.value} />
      </p>
      {conclusion.note && <p className="vote-text">{conclusion.note}</p>}
    </div>
  )
}

/** UC-57: the Chair picks the outcome — the system never derives it from the tally (BR-263). */
function ConclusionForm({
  onConclude,
}: {
  onConclude: (value: VoteConclusionValue, note: string) => Promise<boolean>
}) {
  const [value, setValue] = useState<VoteConclusionValue | ''>('')
  const [note, setNote] = useState('')
  const [saving, setSaving] = useState(false)
  return (
    <form
      className="vote-box vote-conclusion-form"
      onSubmit={async (event) => {
        event.preventDefault()
        if (!value) return
        setSaving(true)
        await onConclude(value, note.trim())
        setSaving(false)
      }}
    >
      <h4 className="field-required">Kết luận</h4>
      <SegmentedControl<VoteConclusionValue | ''>
        label="Kết luận"
        value={value}
        options={[
          { value: 'Passed', label: 'Thông qua' },
          { value: 'NotPassed', label: 'Không thông qua', short: 'Không qua' },
          { value: 'Deferred', label: 'Hoãn' },
        ]}
        onChange={setValue}
        disabled={saving}
      />
      <label className="field">
        <span>Ghi chú</span>
        <textarea
          rows={2}
          value={note}
          onChange={(e) => setNote(e.target.value)}
          maxLength={NOTE_MAX}
          disabled={saving}
        />
        <span className="field-foot">
          <small className="field-hint">Ghi rồi không sửa được.</small>
          <small className="field-counter">
            {note.length.toLocaleString('vi-VN')}/{NOTE_MAX.toLocaleString('vi-VN')}
          </small>
        </span>
      </label>
      <div className="vote-form-actions">
        <button type="submit" className="btn btn-primary btn-sm" disabled={!value || saving}>
          {saving ? 'Đang ghi…' : 'Ghi kết luận'}
        </button>
      </div>
    </form>
  )
}

/** Draft, open or closed, as icon and text (ADR-0028); a draft is seen by the Chair and the Secretary only. */
function VoteStatusChip({ vote }: { vote: VoteSummary }) {
  if (vote.status === 'Open') return <span className="status status-live vote-status">Đang mở</span>
  if (vote.status === 'Draft') {
    return (
      <span className="status status-slate vote-status" tabIndex={0} data-tooltip="Chỉ Chủ toạ và Thư ký thấy">
        <Icon name="pencil" size={14} />
        Nháp
      </span>
    )
  }
  return (
    <span className="status status-ended vote-status">
      <Icon name="lock" size={14} />
      Đã đóng
    </span>
  )
}

/** Shaped like the loaded list, so nothing jumps when it arrives. */
function VoteSkeleton() {
  return (
    <ol className="vote-list" aria-busy="true" aria-label="Đang tải biểu quyết">
      {Array.from({ length: 2 }, (_, i) => (
        <li key={i} className="vote-item">
          <div className="vote-head">
            <span className="skeleton vote-skeleton" />
          </div>
        </li>
      ))}
    </ol>
  )
}
