import { useEffect, useId, useState, type FormEvent } from 'react'
import {
  addParticipants,
  changeAssignments,
  handOverChair,
  inviteGuest,
  listEditorOptions,
  listParticipants,
} from '../../api/participants'
import { ApiError } from '../../api/client'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { errorMessage } from '../../lib/errors'
import {
  MEETING_ROLE_LABEL,
  type MeetingRole,
  type ParticipantOption,
  type ParticipantRow,
} from '../../types/participant'
import { isRefusal, type DialogOutcome } from './outcome'
import { Person } from './Person'

/** Participant endpoints list each field's messages; the first one is enough under the field. */
function fieldError(err: unknown, key: string): string | undefined {
  if (!(err instanceof ApiError)) return undefined
  const raw = (err.body?.errors as Record<string, unknown> | undefined)?.[key]
  return raw === undefined ? undefined : Array.isArray(raw) ? String(raw[0]) : String(raw)
}

const SEARCH_DELAY_MS = 300

/** What each meeting role may do, in a line under its name — the question a role picker answers. */
const ROLE_DESCRIPTION: Record<MeetingRole, string> = {
  Chair: 'Điều hành cuộc họp, chuyển mục nghị sự, tắt micro người dự.',
  Secretary: 'Soạn chương trình nghị sự, quản lý thành phần.',
  Member: 'Xem cuộc họp và vào phòng họp.',
}

/**
 * Pick one meeting role: a radio per row with what the role may do under its name, as AI-eCabinet
 * does. `notes` replaces a row's line, for a consequence the user must read before choosing it.
 */
function RolePicker<T extends MeetingRole>({
  value,
  roles,
  onChange,
  notes,
}: {
  value: T
  roles: readonly T[]
  onChange: (role: T) => void
  notes?: Partial<Record<T, string>>
}) {
  const name = useId()
  return (
    <fieldset className="choice-list">
      <legend>Vai trò</legend>
      {roles.map((role) => (
        // The radio is named by the role alone; its line is a description, not part of the name.
        <label key={role} className="choice-row">
          <input
            type="radio"
            name={name}
            checked={value === role}
            onChange={() => onChange(role)}
            aria-labelledby={`${name}-${role}`}
            aria-describedby={`${name}-${role}-desc`}
          />
          <span className="choice-text">
            <span id={`${name}-${role}`} className="choice-label">
              {MEETING_ROLE_LABEL[role]}
            </span>
            <span id={`${name}-${role}-desc`} className="choice-desc">
              {notes?.[role] ?? ROLE_DESCRIPTION[role]}
            </span>
          </span>
        </label>
      ))}
    </fieldset>
  )
}

/**
 * Add internal people (POST …/participants). Search people not on the roster yet, tick several, give
 * them one role. A new Chair is not added here: the Chair is handed over from an existing row.
 */
export function AddParticipantsDialog({
  meetingId,
  revision,
  onClose,
  onDone,
}: {
  meetingId: string
  revision: string
  onClose: () => void
  onDone: (outcome: DialogOutcome) => void
}) {
  const [query, setQuery] = useState('')
  const [options, setOptions] = useState<{ items: ParticipantOption[]; total: number } | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<Map<string, ParticipantOption>>(new Map())
  const [role, setRole] = useState<'Secretary' | 'Member'>('Member')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    const timer = window.setTimeout(async () => {
      try {
        const page = await listEditorOptions(meetingId, query, 1, 20)
        if (!cancelled) {
          setOptions({ items: page.items, total: page.total })
          setLoadError(null)
        }
      } catch (err) {
        if (cancelled) return
        if (isRefusal(err)) onDone({ kind: 'refused', error: err })
        else setLoadError(errorMessage(err))
      }
    }, SEARCH_DELAY_MS)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [meetingId, query, onDone])

  function toggle(option: ParticipantOption) {
    setSelected((current) => {
      const next = new Map(current)
      if (next.has(option.id)) next.delete(option.id)
      else next.set(option.id, option)
      return next
    })
  }

  async function submit() {
    setPending(true)
    setError(null)
    try {
      onDone({ kind: 'saved', response: await addParticipants(meetingId, revision, [...selected.keys()], role) })
    } catch (err) {
      setPending(false)
      if (isRefusal(err)) onDone({ kind: 'refused', error: err })
      else setError(fieldError(err, 'userIds') ?? errorMessage(err))
    }
  }

  const close = () => !pending && onClose()

  return (
    <Dialog
      title="Thêm thành phần tham dự"
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-outline" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button type="button" className="btn btn-primary" onClick={submit} disabled={pending || selected.size === 0}>
            <Icon name="userPlus" size={16} />
            {pending ? 'Đang thêm…' : selected.size > 0 ? `Thêm ${selected.size} người` : 'Thêm'}
          </button>
        </>
      }
    >
      <label className="field pick-search">
        <span>Người dự họp</span>
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Tìm theo tên hoặc email"
          maxLength={200}
        />
      </label>
      <div className="pick-list" aria-busy={options === null}>
        {loadError ? (
          <p className="pick-note pick-note-error">{loadError}</p>
        ) : options === null ? (
          <p className="pick-note">Đang tải…</p>
        ) : options.items.length === 0 ? (
          <p className="pick-note">
            {query.trim()
              ? 'Không có ai khớp, hoặc người đó đã có trong danh sách.'
              : 'Mọi người đều đã có trong danh sách.'}
          </p>
        ) : (
          options.items.map((option) => (
            <label key={option.id} className="pick-row">
              <input type="checkbox" checked={selected.has(option.id)} onChange={() => toggle(option)} />
              <Person name={option.fullName} email={option.email} />
            </label>
          ))
        )}
      </div>
      {options && options.total > options.items.length && (
        <p className="field-hint">
          Đang hiện {options.items.length} trong {options.total} người. Gõ tên cụ thể hơn để tìm người khác.
        </p>
      )}
      <div className="pick-role">
        <RolePicker value={role} roles={['Secretary', 'Member'] as const} onChange={setRole} />
      </div>
      {error && (
        <p className="alert alert-error dialog-error" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  )
}

/** Invite a guest by name and email (POST …/guest-invitations). Guests have no internal role. */
export function InviteGuestDialog({
  meetingId,
  revision,
  onClose,
  onDone,
}: {
  meetingId: string
  revision: string
  onClose: () => void
  onDone: (outcome: DialogOutcome) => void
}) {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [errors, setErrors] = useState<{ invitedName?: string; email?: string; form?: string }>({})
  const [pending, setPending] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    const local: typeof errors = {}
    if (!name.trim()) local.invitedName = 'Nhập tên khách.'
    if (!/^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$/.test(email.trim())) local.email = 'Nhập email hợp lệ.'
    setErrors(local)
    if (local.invitedName || local.email) return
    setPending(true)
    try {
      onDone({ kind: 'saved', response: await inviteGuest(meetingId, revision, name.trim(), email.trim()) })
    } catch (err) {
      setPending(false)
      if (isRefusal(err)) return onDone({ kind: 'refused', error: err })
      const invitedName = fieldError(err, 'invitedName')
      const emailError = fieldError(err, 'email')
      setErrors({ invitedName, email: emailError, form: invitedName || emailError ? undefined : errorMessage(err) })
    }
  }

  const close = () => !pending && onClose()

  return (
    <Dialog
      title="Mời khách ngoài hệ thống"
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-outline" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button type="submit" form="invite-guest" className="btn btn-primary" disabled={pending}>
            <Icon name="mail" size={16} />
            {pending ? 'Đang mời…' : 'Mời'}
          </button>
        </>
      }
    >
      <form id="invite-guest" className="dialog-form" onSubmit={submit} noValidate>
        <label className="field">
          <span className="field-required">Tên khách</span>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={200}
            aria-invalid={!!errors.invitedName}
          />
          {errors.invitedName && <small className="field-error">{errors.invitedName}</small>}
        </label>
        <label className="field">
          <span className="field-required">Email</span>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            maxLength={256}
            aria-invalid={!!errors.email}
          />
          {errors.email && <small className="field-error">{errors.email}</small>}
        </label>
        {errors.form && (
          <p className="alert alert-error dialog-error" role="alert">
            {errors.form}
          </p>
        )}
      </form>
    </Dialog>
  )
}

/**
 * Change one internal participant's role. Making them Chair moves the chair in one step: the current
 * Chair becomes "Thành viên", so the meeting never has zero or two Chairs. This is the only place the
 * chair moves. The Chair of a published meeting hands it over with POST …/participants/chair-handover
 * (UC-73); before publication, and a Secretary replacing a deactivated Chair, use PUT …/assignments
 * with the confirmation the backend requires.
 */
export function ChangeRoleDialog({
  meetingId,
  revision,
  row,
  chairOption,
  handOver,
  onClose,
  onDone,
}: {
  meetingId: string
  revision: string
  row: ParticipantRow & { role: MeetingRole }
  /** Offer "Chủ tọa": the caller may move the chair now, and this person may take it. */
  chairOption: boolean
  /** The caller is the Chair of a published meeting, handing the role over themselves. */
  handOver: boolean
  onClose: () => void
  onDone: (outcome: DialogOutcome) => void
}) {
  const [role, setRole] = useState<MeetingRole>(row.role)
  const [chair, setChair] = useState<ParticipantRow | null | undefined>(undefined)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const roles = chairOption ? (['Chair', 'Secretary', 'Member'] as const) : (['Secretary', 'Member'] as const)
  const promote = role === 'Chair'

  // Replacing the Chair through PUT needs the current Chair's assignment, which may sit on another page.
  // Read it as the dialog opens, so the "Chủ tọa" row can name who steps down before anyone picks it.
  useEffect(() => {
    if (!chairOption || handOver || chair !== undefined) return
    let cancelled = false
    findChair(meetingId).then(
      (found) => !cancelled && setChair(found),
      (err) => !cancelled && (isRefusal(err) ? onDone({ kind: 'refused', error: err }) : setError(errorMessage(err))),
    )
    return () => {
      cancelled = true
    }
  }, [chairOption, handOver, chair, meetingId, onDone])

  async function submit() {
    setPending(true)
    setError(null)
    try {
      const response =
        promote && handOver
          ? await handOverChair(meetingId, revision, row.id)
          : await changeAssignments(
              meetingId,
              revision,
              promote && chair
                ? {
                    changes: [
                      { participantId: chair.id, role: 'Member' },
                      { participantId: row.id, role: 'Chair' },
                    ],
                    chairReplacement: {
                      outgoingParticipantId: chair.id,
                      incomingParticipantId: row.id,
                      confirmed: true,
                    },
                  }
                : { changes: [{ participantId: row.id, role }] },
            )
      onDone({ kind: 'saved', response })
    } catch (err) {
      setPending(false)
      if (isRefusal(err)) onDone({ kind: 'refused', error: err })
      else setError(errorMessage(err))
    }
  }

  const close = () => !pending && onClose()

  return (
    <Dialog
      title="Đổi vai trò"
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-outline" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={submit}
            disabled={pending || role === row.role || (promote && !handOver && !chair)}
          >
            <Icon name="check" size={16} />
            {pending ? 'Đang lưu…' : promote ? 'Giao Chủ tọa' : 'Lưu'}
          </button>
        </>
      }
    >
      <div className="dialog-form">
        <Person name={row.displayName} email={row.email} />
        {/* Who steps down sits in the "Chủ tọa" line itself, so choosing it changes no height. */}
        <RolePicker
          value={role}
          roles={roles}
          onChange={setRole}
          notes={{
            Chair:
              chair === null
                ? 'Không tìm thấy Chủ tọa hiện tại. Hãy tải lại trang.'
                : `${ROLE_DESCRIPTION.Chair} ${handOver ? 'Bạn' : (chair?.displayName ?? 'Chủ tọa hiện tại')} sẽ thành Thành viên.`,
          }}
        />
        {error && (
          <p className="alert alert-error dialog-error" role="alert">
            {error}
          </p>
        )}
      </div>
    </Dialog>
  )
}

/** The current Chair's row, read page by page: the roster is sorted by name, not by role. */
async function findChair(meetingId: string): Promise<ParticipantRow | null> {
  for (let page = 1; ; page++) {
    const result = await listParticipants(meetingId, '', page, 100)
    const chair = result.items.find((r) => r.role === 'Chair')
    if (chair) return chair
    if (page * result.pageSize >= result.total) return null
  }
}
