import { useCallback, useEffect, useRef, useState } from 'react'
import { getParticipantContext, listParticipants, removeParticipant, revokeGuest } from '../../api/participants'
import { Badge, type BadgeTone } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { Pagination } from '../../components/Pagination'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { useSearchBox } from '../../lib/useSearchBox'
import {
  MEETING_ROLE_LABEL,
  type MeetingRole,
  type ParticipantContext,
  type ParticipantPage,
  type ParticipantRow,
} from '../../types/participant'
import { AttendanceCell, AttendanceSummary } from '../attendance/AttendanceCell'
import { useAttendance } from '../attendance/useAttendance'
import { useAccessLost } from '../auth/useAccessLost'
import { useAuth } from '../auth/useAuth'
import { useMeetingEvents } from '../meetings/useMeetingEvents'
import { isRefusal, type DialogOutcome } from './outcome'
import { AddParticipantsDialog, ChangeRoleDialog, InviteGuestDialog } from './ParticipantDialogs'
import { Person } from './Person'
import { RsvpChip } from './Rsvp'

const PAGE_SIZES = [20, 50, 100] as const

const ROLE_TONE: Record<MeetingRole, BadgeTone> = { Chair: 'brand', Secretary: 'blue', Member: 'slate' }

type Loaded = { context: ParticipantContext; roster: ParticipantPage }
type State =
  | { kind: 'loading' }
  | { kind: 'loaded'; data: Loaded; stale: boolean }
  | { kind: 'error'; message: string; data: Loaded | null }
  | { kind: 'gone' }

type Open =
  | { kind: 'add' }
  | { kind: 'guest' }
  | { kind: 'role'; row: ParticipantRow & { role: MeetingRole }; chairOption: boolean; handOver: boolean }
  | { kind: 'remove'; row: ParticipantRow }
  | null

/** What a row's buttons open; the panel adds what the role dialog may offer. */
type RowOpen = { kind: 'role'; row: ParticipantRow & { role: MeetingRole } } | { kind: 'remove'; row: ParticipantRow }

/**
 * The meeting's roster (SCRUM-20, UC-27–30): everyone may read it; the Chair and Secretaries add,
 * invite guests, change roles, hand over the chair and remove people, while the status allows it.
 * What the caller may do comes from the server's participant context, never from guessing.
 *
 * Once the meeting has started, each row also carries its attendance (SCRUM-26), as AI-eCabinet's
 * roster does: the Secretary marks "Có mặt" or "Vắng" right in the row, everyone else reads it.
 *
 * A panel (ADR-0011): it loads its own data and listens on `/hubs/meetings` itself, so a change
 * made by anyone — including an invitee's answer — re-reads it. `onChanged` lets the page re-read
 * what it shows too (the Chair and Secretaries in "Thông tin chung").
 */
export function ParticipantsPanel({ meetingId, onChanged }: { meetingId: string; onChanged?: () => void }) {
  const toast = useToast()
  const { user } = useAuth()
  const handleAccessLost = useAccessLost()
  const [query, setQuery] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState<number>(PAGE_SIZES[0])
  const [search, setSearch] = useSearchBox(query, (value) => {
    setQuery(value.trim())
    setPage(1)
  })
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [open, setOpen] = useState<Open>(null)
  const generation = useRef(0)
  const status = state.kind === 'loaded' || state.kind === 'error' ? state.data?.context.status : undefined
  // Attendance exists from the start of the meeting on; before that every row would say "Chưa điểm danh".
  const showAttendance = status === 'InProgress' || status === 'Ended'
  const attendance = useAttendance(meetingId, showAttendance)

  const load = useCallback(async () => {
    const current = ++generation.current
    setState((s) => (s.kind === 'loaded' ? { ...s, stale: true } : s))
    try {
      const [context, roster] = await Promise.all([
        getParticipantContext(meetingId),
        listParticipants(meetingId, query, page, pageSize),
      ])
      if (current !== generation.current) return
      // A page past the end (people were removed meanwhile): step back to the last one.
      if (roster.items.length === 0 && page > 1) return setPage(Math.max(1, Math.ceil(roster.total / pageSize)))
      setState({ kind: 'loaded', data: { context, roster }, stale: false })
    } catch (err) {
      if (current !== generation.current || handleAccessLost(err)) return
      if (isApiError(err, 404)) return setState({ kind: 'gone' })
      setState((s) => ({
        kind: 'error',
        message: errorMessage(err),
        data: s.kind === 'loaded' || s.kind === 'error' ? s.data : null,
      }))
    }
  }, [meetingId, query, page, pageSize, handleAccessLost])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  useMeetingEvents(meetingId, () => void load())

  /** Every dialog ends here: say what happened, then show what is true now. */
  const finish = useCallback(
    (outcome: DialogOutcome) => {
      setOpen(null)
      if (outcome.kind === 'saved') toast.show(outcome.response.message)
      else if (!handleAccessLost(outcome.error)) toast.show(errorMessage(outcome.error), 'error')
      // Take the new revision from the answer at once: the reload below (and the hub's) takes a
      // round trip, and a change made in that window would otherwise go out stale and get 409.
      const revision = outcome.kind === 'saved' ? outcome.response.revision : undefined
      if (revision)
        setState((s) =>
          s.kind === 'loaded' ? { ...s, data: { ...s.data, roster: { ...s.data.roster, revision } } } : s,
        )
      void load()
      onChanged?.()
    },
    [toast, handleAccessLost, load, onChanged],
  )

  if (state.kind === 'gone') {
    return (
      <div className="card card-pending tab-pending">
        <p>Bạn không còn xem được thành phần của cuộc họp này.</p>
      </div>
    )
  }

  const data = state.kind === 'loading' ? null : state.data
  if (!data) {
    return state.kind === 'error' ? (
      <div className="alert alert-error" role="alert">
        <p>{state.message}</p>
        <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
          <Icon name="refresh" size={16} />
          Thử lại
        </button>
      </div>
    ) : (
      <PanelSkeleton />
    )
  }

  const { context, roster } = data
  const manage = context.permissions.canManageParticipants
  const live = context.status === 'Published' || context.status === 'InProgress'
  // Settled when the role dialog opens: a reload behind it (someone else's change) must not swap its
  // options or its request under the user. The server rechecks and answers MSG259/MSG260.
  const openRow = (open: RowOpen) =>
    setOpen(
      open.kind === 'role'
        ? {
            ...open,
            chairOption: context.permissions.canReplaceChair && canTakeChair(open.row, live),
            handOver: live && context.myRole === 'Chair',
          }
        : open,
    )
  const totalPages = Math.max(1, Math.ceil(roster.total / roster.pageSize))
  const attendanceData =
    attendance.state.kind === 'loaded' || attendance.state.kind === 'error' ? attendance.state.data : null
  const attendanceOf = new Map(attendanceData?.rows.map((row) => [row.id, row]))
  const canRecord = attendanceData?.permissions.canRecordAttendance ?? false
  const columns = 2 + (showAttendance ? 1 : 0) + (manage ? 1 : 0)
  const attendanceCell = (id: string) => {
    const row = attendanceOf.get(id)
    return row ? (
      <AttendanceCell
        row={row}
        canRecord={canRecord}
        pending={attendance.pending.get(id)}
        onRecord={attendance.record}
      />
    ) : null
  }

  return (
    <section className="list-panel participants-panel" aria-label="Thành phần tham dự">
      <div className="toolbar">
        <label className="search search-flex">
          <Icon name="search" size={16} />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo tên hoặc email"
            aria-label="Tìm thành phần"
            maxLength={200}
          />
        </label>
        {showAttendance && attendanceData && <AttendanceSummary totals={attendanceData.totals} />}
        {manage && (
          <div className="toolbar-end row-actions participants-actions">
            {/* Two kinds of invitee, two icons: someone with an account, or a guest by email. */}
            <button
              type="button"
              className="btn btn-outline btn-icon"
              onClick={() => setOpen({ kind: 'guest' })}
              aria-label="Mời khách ngoài hệ thống"
              data-tooltip="Mời khách ngoài hệ thống (qua email)"
            >
              <Icon name="mail" size={18} />
            </button>
            <button
              type="button"
              className="btn btn-primary btn-icon btn-cta"
              onClick={() => setOpen({ kind: 'add' })}
              aria-label="Thêm người dự họp"
              data-tooltip="Thêm người dự họp"
            >
              <Icon name="userPlus" size={18} />
            </button>
          </div>
        )}
      </div>

      {state.kind === 'error' && (
        <div className="alert alert-error participants-alert" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}

      {showAttendance && attendance.state.kind === 'error' && (
        <div className="alert alert-error participants-alert" role="alert">
          <p>{attendance.state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void attendance.reload()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}

      <div className="table-card">
        <table className={state.kind === 'loaded' && state.stale ? 'data-table data-table-stale' : 'data-table'}>
          <thead>
            <tr>
              <th>Thành phần</th>
              <th className="col-role">Vai trò</th>
              {showAttendance && <th className="col-attendance">Điểm danh</th>}
              {manage && <th className="col-action">Thao tác</th>}
            </tr>
          </thead>
          <tbody aria-busy={state.kind === 'loaded' && state.stale}>
            {roster.items.length === 0 ? (
              <tr>
                <td colSpan={columns}>
                  <div className="cell-empty">
                    <p className="empty-title">Không có thành phần nào khớp “{query}”.</p>
                    <button
                      type="button"
                      className="btn btn-outline btn-sm"
                      onClick={() => {
                        setSearch('')
                        setQuery('')
                        setPage(1)
                      }}
                    >
                      <Icon name="x" size={16} />
                      Xoá tìm kiếm
                    </button>
                  </div>
                </td>
              </tr>
            ) : (
              roster.items.map((row) => (
                <tr key={row.id} className={row.isActive === false ? 'row-inactive' : undefined}>
                  <td className="cell-title">
                    <span className="participant-cell">
                      <Person
                        name={row.displayName}
                        email={row.email}
                        self={row.userId !== null && row.userId === user?.id}
                      />
                      <span className="chip-row">
                        <span className="role-inline">
                          <RoleBadge role={row.role} />
                        </span>
                        {row.isActive === false && <Badge tone="slate">Tài khoản đã khoá</Badge>}
                        {row.role !== 'Chair' && <RsvpChip response={row.response} />}
                      </span>
                      {showAttendance && <span className="attendance-inline">{attendanceCell(row.id)}</span>}
                    </span>
                  </td>
                  <td className="col-role">
                    <RoleBadge role={row.role} />
                  </td>
                  {showAttendance && <td className="col-attendance">{attendanceCell(row.id)}</td>}
                  {manage && (
                    <td className="col-action">
                      <RowActions row={row} onOpen={openRow} />
                    </td>
                  )}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <Pagination
        page={roster.page}
        pageSize={roster.pageSize}
        totalCount={roster.total}
        totalPages={totalPages}
        pageSizes={PAGE_SIZES}
        itemLabel="thành phần"
        onPageChange={setPage}
        onPageSizeChange={(size) => {
          setPageSize(size)
          setPage(1)
        }}
      />

      {open?.kind === 'add' && (
        <AddParticipantsDialog
          meetingId={meetingId}
          revision={roster.revision}
          onClose={() => setOpen(null)}
          onDone={finish}
        />
      )}
      {open?.kind === 'guest' && (
        <InviteGuestDialog
          meetingId={meetingId}
          revision={roster.revision}
          onClose={() => setOpen(null)}
          onDone={finish}
        />
      )}
      {open?.kind === 'role' && (
        <ChangeRoleDialog
          meetingId={meetingId}
          revision={roster.revision}
          row={open.row}
          chairOption={open.chairOption}
          handOver={open.handOver}
          onClose={() => setOpen(null)}
          onDone={finish}
        />
      )}
      {open?.kind === 'remove' && (
        <ConfirmDialog
          title={open.row.kind === 'Guest' ? 'Thu hồi lời mời khách?' : 'Gỡ khỏi cuộc họp?'}
          tone="danger"
          confirmLabel={open.row.kind === 'Guest' ? 'Thu hồi lời mời' : 'Gỡ khỏi cuộc họp'}
          pendingLabel="Đang gỡ…"
          onConfirm={async () => {
            const row = open.row
            try {
              const response = await (row.kind === 'Guest'
                ? revokeGuest(meetingId, roster.revision, row.id)
                : removeParticipant(meetingId, roster.revision, row.id))
              finish({ kind: 'saved', response })
            } catch (err) {
              if (!isRefusal(err)) throw err
              finish({ kind: 'refused', error: err })
            }
          }}
          onClose={() => setOpen(null)}
        >
          {open.row.kind === 'Guest'
            ? `${open.row.displayName} không còn vào được cuộc họp bằng lời mời đã nhận.`
            : `${open.row.displayName} không còn xem được cuộc họp này. Nếu cuộc họp đã công bố, họ nhận thông báo đã được gỡ.`}
        </ConfirmDialog>
      )}
    </section>
  )
}

/** A role chip; a guest has no role. */
function RoleBadge({ role }: { role: MeetingRole | null }) {
  return role ? <Badge tone={ROLE_TONE[role]}>{MEETING_ROLE_LABEL[role]}</Badge> : <Badge tone="outline">Khách</Badge>
}

/**
 * Whether this person may take the chair, as the server checks it: an active account, and once the
 * meeting is published only a Member — not a Secretary (MSG259).
 */
function canTakeChair(row: ParticipantRow, live: boolean) {
  return row.isActive !== false && (!live || row.role === 'Member')
}

/**
 * The Chair has no row actions: a meeting always keeps one, so the chair moves by making someone
 * else Chair. A guest has no role to change, only an invitation to withdraw.
 */
function RowActions({ row, onOpen }: { row: ParticipantRow; onOpen: (open: RowOpen) => void }) {
  if (row.role === 'Chair') return null
  const role = row.role
  return (
    <span className="row-actions">
      {role && (
        <button
          type="button"
          className="icon-button"
          aria-label={`Đổi vai trò của ${row.displayName}`}
          data-tooltip="Đổi vai trò"
          onClick={() => onOpen({ kind: 'role', row: { ...row, role } })}
        >
          <Icon name="pencil" size={16} />
        </button>
      )}
      <button
        type="button"
        className="icon-button"
        aria-label={row.kind === 'Guest' ? `Thu hồi lời mời của ${row.displayName}` : `Gỡ ${row.displayName}`}
        data-tooltip={row.kind === 'Guest' ? 'Thu hồi lời mời' : 'Gỡ khỏi cuộc họp'}
        onClick={() => onOpen({ kind: 'remove', row })}
      >
        <Icon name="trash" size={16} />
      </button>
    </span>
  )
}

/** Shaped like the loaded panel, so nothing jumps when the roster arrives. */
function PanelSkeleton() {
  return (
    <div className="list-panel" aria-busy="true" aria-label="Đang tải thành phần tham dự">
      <div className="toolbar">
        <span className="skeleton participants-skeleton-search" />
      </div>
      <table className="data-table">
        <tbody>
          {Array.from({ length: 4 }, (_, i) => (
            <tr key={i} className="row-skeleton">
              <td>
                <span className="skeleton" />
              </td>
              <td>
                <span className="skeleton" />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
