import { useEffect, useId, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react'
import { listParticipantOptions } from '../../api/participants'
import { Icon } from '../../components/Icon'
import { Select } from '../../components/Select'
import { errorMessage } from '../../lib/errors'
import { MEETING_ROLES, MEETING_ROLE_LABEL, type MeetingRole, type ParticipantOption } from '../../types/participant'
import { Person } from './Person'
import type { RosterEntry } from './roster'

/** Matches shown under the search box: enough to pick from, short enough to scan. */
const SHOWN = 8
const SEARCH_DELAY_MS = 300

const ROLE_OPTIONS = MEETING_ROLES.map((value) => ({ value, label: MEETING_ROLE_LABEL[value] }))

/**
 * Who attends a new meeting, and in what role (SCRUM-20). Search active accounts, pick, then set
 * each one's role. There is exactly one Chair: making someone Chair hands the previous one
 * "Thành viên", as AI-eCabinet does. The organizer is not added on their own — they need not attend.
 *
 * As in AI-eCabinet, many are added from one search: the matches stay open with whoever is in
 * ticked (a second press takes them out), and the picked list keeps its height, scrolling to whoever
 * was just added — only the list scrolls, never the dialog under the pointer.
 */
export function RosterField({
  value,
  me,
  onChange,
  error,
  rowErrors,
}: {
  value: RosterEntry[]
  /** The signed-in user's id, marked "(Tôi)" in the list. */
  me?: string
  onChange: (next: RosterEntry[]) => void
  error?: string
  /** Server errors for one picked person, keyed by user id. */
  rowErrors?: Record<string, string>
}) {
  const listId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const menuRef = useRef<HTMLUListElement>(null)
  const pickedRef = useRef<HTMLUListElement>(null)
  const count = useRef(value.length)
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [highlighted, setHighlighted] = useState(0)
  const [result, setResult] = useState<
    | { kind: 'idle' }
    | { kind: 'loading' }
    | { kind: 'loaded'; items: ParticipantOption[] }
    | { kind: 'error'; message: string }
  >({ kind: 'idle' })

  // Search after typing pauses; an empty box lists people alphabetically, so an opened menu is never blank.
  useEffect(() => {
    if (!open) return
    let cancelled = false
    const timer = window.setTimeout(async () => {
      setResult((r) => (r.kind === 'loaded' ? r : { kind: 'loading' }))
      try {
        const page = await listParticipantOptions(query, 1, SHOWN + Math.min(value.length, 20))
        if (!cancelled) setResult({ kind: 'loaded', items: page.items })
      } catch (err) {
        if (!cancelled) setResult({ kind: 'error', message: errorMessage(err) })
      }
    }, SEARCH_DELAY_MS)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [open, query, value.length])

  // In the top layer, pinned under the search box while the dialog body scrolls, as Select does.
  useLayoutEffect(() => {
    const menu = menuRef.current
    const input = inputRef.current
    if (!open || !menu || !input) return
    menu.showPopover?.()
    function place() {
      if (!menu || !input) return
      const box = input.getBoundingClientRect()
      menu.style.left = `${box.left}px`
      menu.style.width = `${box.width}px`
      // Under the box, unless there is more room above it (the box low in a short window).
      const below = window.innerHeight - box.bottom
      if (below >= 300 || below >= box.top) {
        menu.style.top = `${box.bottom + 4}px`
        menu.style.bottom = 'auto'
      } else {
        menu.style.top = 'auto'
        menu.style.bottom = `${window.innerHeight - box.top + 4}px`
      }
    }
    place()
    window.addEventListener('scroll', place, true)
    window.addEventListener('resize', place)
    return () => {
      window.removeEventListener('scroll', place, true)
      window.removeEventListener('resize', place)
      if (menu.matches(':popover-open')) menu.hidePopover()
    }
  }, [open])

  // The picked list scrolls to whoever was just added; removing someone leaves it where it is.
  useEffect(() => {
    const list = pickedRef.current
    if (list && value.length > count.current) list.scrollTop = list.scrollHeight
    count.current = value.length
  }, [value.length])

  const picked = new Set(value.map((p) => p.userId))
  const matches = result.kind === 'loaded' ? result.items : []

  /** In, or out again if already in; the matches stay open for the next pick. */
  function toggle(option: ParticipantOption) {
    onChange(
      picked.has(option.id)
        ? value.filter((p) => p.userId !== option.id)
        : [...value, { userId: option.id, fullName: option.fullName, email: option.email, role: 'Member' }],
    )
  }

  function setRole(userId: string, role: MeetingRole) {
    onChange(
      value.map((p) =>
        p.userId === userId ? { ...p, role } : role === 'Chair' && p.role === 'Chair' ? { ...p, role: 'Member' } : p,
      ),
    )
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      setOpen(true)
      if (matches.length === 0) return
      const step = event.key === 'ArrowDown' ? 1 : -1
      setHighlighted((h) => (h + step + matches.length) % matches.length)
    } else if (event.key === 'Enter') {
      // Never submits the form from the search box.
      event.preventDefault()
      if (open && matches[highlighted]) toggle(matches[highlighted])
    } else if (event.key === 'Escape' && open) {
      event.preventDefault()
      setOpen(false)
    }
  }

  const activeId = open && matches[highlighted] ? `${listId}-${matches[highlighted].id}` : undefined

  return (
    <div className="field span-2" data-field="participants">
      <span className="field-required">Người dự họp</span>

      <div className="search roster-search">
        <Icon name="search" size={16} />
        <input
          ref={inputRef}
          role="combobox"
          aria-label="Tìm người dự họp"
          aria-expanded={open}
          aria-controls={listId}
          aria-activedescendant={activeId}
          aria-autocomplete="list"
          aria-invalid={!!error}
          value={query}
          placeholder="Tìm theo tên hoặc email để thêm người dự họp"
          onChange={(e) => {
            setQuery(e.target.value)
            setHighlighted(0)
            setOpen(true)
            // The previous word's matches would be clickable until the new ones arrive.
            setResult({ kind: 'loading' })
          }}
          // Not on focus: a failed submit focuses this box, and the menu would cover the rows' errors.
          onClick={() => setOpen(true)}
          onBlur={() => setOpen(false)}
          onKeyDown={onKeyDown}
        />
        {open && (
          <ul
            ref={menuRef}
            id={listId}
            className="roster-menu"
            role="listbox"
            aria-multiselectable="true"
            aria-label="Người có thể mời"
            popover="manual"
          >
            {result.kind === 'error' ? (
              <li className="roster-menu-note roster-menu-error">{result.message}</li>
            ) : result.kind !== 'loaded' ? (
              <li className="roster-menu-note">Đang tìm…</li>
            ) : matches.length === 0 ? (
              <li className="roster-menu-note">
                {query.trim() ? 'Không có ai khớp.' : 'Chưa có ai để mời.'}
              </li>
            ) : (
              matches.map((option, index) => (
                <li
                  key={option.id}
                  id={`${listId}-${option.id}`}
                  role="option"
                  aria-selected={picked.has(option.id)}
                  className={index === highlighted ? 'roster-option roster-option-active' : 'roster-option'}
                  // Keeps focus in the box, so blur does not close the menu before the click lands.
                  onMouseDown={(e) => e.preventDefault()}
                  onMouseEnter={() => setHighlighted(index)}
                  onClick={() => toggle(option)}
                >
                  <span className="roster-option-person">
                    <Person name={option.fullName} email={option.email} />
                  </span>
                  {picked.has(option.id) && <Icon name="check" size={16} />}
                </li>
              ))
            )}
          </ul>
        )}
      </div>

      {value.length > 0 ? (
        <ul ref={pickedRef} className="roster-list" aria-label="Người đã chọn">
          {value.map((p) => (
            <li
              key={p.userId}
              className={rowErrors?.[p.userId] ? 'roster-row roster-row-invalid' : 'roster-row'}
              aria-invalid={!!rowErrors?.[p.userId]}
            >
              <div className="roster-row-main">
                <Person name={p.userId === me ? `${p.fullName} (Tôi)` : p.fullName} email={p.email} />
                {rowErrors?.[p.userId] && <small className="field-error">{rowErrors[p.userId]}</small>}
              </div>
              <div className="roster-role">
                <Select
                  value={p.role}
                  options={ROLE_OPTIONS}
                  onChange={(role) => setRole(p.userId, role)}
                  label={`Vai trò của ${p.fullName}`}
                />
              </div>
              <button
                type="button"
                className="icon-button"
                aria-label={`Bỏ ${p.fullName}`}
                data-tooltip="Bỏ khỏi danh sách"
                onClick={() => onChange(value.filter((x) => x.userId !== p.userId))}
              >
                <Icon name="x" size={16} />
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="roster-empty">Chưa chọn ai.</p>
      )}

      {error && <small className="field-error">{error}</small>}
    </div>
  )
}
