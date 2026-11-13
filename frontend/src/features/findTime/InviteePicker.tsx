import { useEffect, useId, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react'
import { listParticipantOptions } from '../../api/participants'
import { Badge } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import { errorMessage } from '../../lib/errors'
import type { ShareLevel } from '../../types/calendarSharing'
import type { ParticipantOption } from '../../types/participant'
import { Person } from '../participants/Person'
import { LEVEL_LABEL } from '../calendarSharing/shareFormat'
import { MAX_PEOPLE } from './slotRules'

export interface Invitee {
  id: string
  fullName: string
  email: string
}

/** What the caller knows of an invitee's calendar before searching: shared (at a level), asked for, or nothing. */
export type ShareState = { kind: 'shared'; level: ShareLevel } | { kind: 'pending' } | { kind: 'none' }

const SHOWN = 8
const SEARCH_DELAY_MS = 300

/**
 * UC-85a step 1 — "Người dự chính", AI-eCabinet's attendee box: search active accounts (participant-options, the same
 * list as creating a meeting), at most 10 besides oneself (D14). Each person's row says at once whether their calendar
 * is shared with the caller and at which level; someone not shared gets "Yêu cầu chia sẻ lịch" (UC-84a). The people
 * already sharing with the caller can be added in one click. "Tính cả lịch của tôi" is off by default (BR-416).
 */
export function InviteePicker({
  value,
  onChange,
  me,
  includeMe,
  onIncludeMe,
  shareOf,
  quick,
  onRequest,
  error,
  rowErrors,
}: {
  value: Invitee[]
  onChange: (next: Invitee[]) => void
  me: string
  includeMe: boolean
  onIncludeMe: (next: boolean) => void
  shareOf: (id: string) => ShareState | null
  /** People sharing with the caller who are not picked yet. */
  quick: Invitee[]
  onRequest: (person: Invitee) => void
  error?: string
  rowErrors?: Record<string, string>
}) {
  const listId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const menuRef = useRef<HTMLUListElement>(null)
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [highlighted, setHighlighted] = useState(0)
  const [result, setResult] = useState<
    | { kind: 'idle' }
    | { kind: 'loading' }
    | { kind: 'loaded'; items: ParticipantOption[] }
    | { kind: 'error'; message: string }
  >({ kind: 'idle' })

  useEffect(() => {
    if (!open) return
    let cancelled = false
    const timer = window.setTimeout(async () => {
      setResult((r) => (r.kind === 'loaded' ? r : { kind: 'loading' }))
      try {
        // One more than shown, since the caller is left out of the list.
        const page = await listParticipantOptions(query, 1, SHOWN + 1 + value.length)
        if (!cancelled) setResult({ kind: 'loaded', items: page.items.filter((p) => p.id !== me) })
      } catch (err) {
        if (!cancelled) setResult({ kind: 'error', message: errorMessage(err) })
      }
    }, SEARCH_DELAY_MS)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [open, query, value.length, me])

  // In the top layer under the box, as RosterField's menu.
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
      menu.style.top = `${box.bottom + 4}px`
      menu.style.bottom = 'auto'
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

  const picked = new Set(value.map((p) => p.id))
  const matches = result.kind === 'loaded' ? result.items : []
  const full = value.length >= MAX_PEOPLE

  function toggle(option: Invitee) {
    if (picked.has(option.id)) onChange(value.filter((p) => p.id !== option.id))
    else if (!full) {
      onChange([...value, { id: option.id, fullName: option.fullName, email: option.email }])
      // Ready for the next name, rather than appending to the last one.
      setQuery('')
    }
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      setOpen(true)
      if (matches.length === 0) return
      const step = event.key === 'ArrowDown' ? 1 : -1
      setHighlighted((h) => (h + step + matches.length) % matches.length)
    } else if (event.key === 'Enter') {
      event.preventDefault()
      if (open && matches[highlighted]) toggle(matches[highlighted])
    } else if (event.key === 'Escape' && open) {
      event.preventDefault()
      setOpen(false)
    }
  }

  const activeId = open && matches[highlighted] ? `${listId}-${matches[highlighted].id}` : undefined

  return (
    <section className="card find-people" aria-label="Người dự chính">
      <div className="field" data-field="userIds">
        <span className="field-label-row">
          <span className="field-required">Người dự chính</span>
          <small className="field-counter">
            {value.length}/{MAX_PEOPLE}
          </small>
        </span>
        <div className="search roster-search">
          <Icon name="search" size={16} />
          <input
            ref={inputRef}
            role="combobox"
            aria-label="Tìm người dự"
            aria-expanded={open}
            aria-controls={listId}
            aria-activedescendant={activeId}
            aria-autocomplete="list"
            aria-invalid={!!error}
            value={query}
            placeholder="Tìm người theo tên hoặc email"
            onChange={(e) => {
              setQuery(e.target.value)
              setHighlighted(0)
              setOpen(true)
              setResult({ kind: 'loading' })
            }}
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
              {full && <li className="roster-menu-note">Đã đủ {MAX_PEOPLE} người. Bỏ bớt để thêm người khác.</li>}
              {result.kind === 'error' ? (
                <li className="roster-menu-note roster-menu-error">{result.message}</li>
              ) : result.kind !== 'loaded' ? (
                <li className="roster-menu-note">Đang tìm…</li>
              ) : matches.length === 0 ? (
                <li className="roster-menu-note">{query.trim() ? 'Không có ai khớp.' : 'Chưa có ai để mời.'}</li>
              ) : (
                matches.slice(0, SHOWN + value.length).map((option, index) => (
                  <li
                    key={option.id}
                    id={`${listId}-${option.id}`}
                    role="option"
                    aria-selected={picked.has(option.id)}
                    aria-disabled={full && !picked.has(option.id)}
                    className={index === highlighted ? 'roster-option roster-option-active' : 'roster-option'}
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
        {error && <small className="field-error">{error}</small>}
      </div>

      {quick.length > 0 && !full && (
        <div className="find-quick">
          <span className="find-quick-label">Người đã chia sẻ lịch với bạn:</span>
          {quick.map((person) => (
            <button key={person.id} type="button" className="chip-toggle" onClick={() => toggle(person)}>
              <Icon name="plus" size={14} />
              {person.fullName}
            </button>
          ))}
        </div>
      )}

      {value.length > 0 && (
        <ul className="roster-list find-people-list" aria-label="Người dự đã chọn">
          {value.map((person) => {
            const state = shareOf(person.id)
            return (
              <li key={person.id} className={rowErrors?.[person.id] ? 'roster-row roster-row-invalid' : 'roster-row'}>
                <div className="roster-row-main">
                  <Person name={person.fullName} email={person.email} />
                  {rowErrors?.[person.id] && <small className="field-error">{rowErrors[person.id]}</small>}
                </div>
                <div className="find-person-state">
                  {state?.kind === 'shared' && <Badge tone="blue">Xem được: {LEVEL_LABEL[state.level]}</Badge>}
                  {state?.kind === 'pending' && <Badge tone="slate">Đang chờ đồng ý</Badge>}
                  {state?.kind === 'none' && (
                    <>
                      <Badge tone="amber">Chưa được chia sẻ lịch</Badge>
                      <button type="button" className="btn btn-outline btn-sm" onClick={() => onRequest(person)}>
                        <Icon name="send" size={14} />
                        Yêu cầu chia sẻ lịch
                      </button>
                    </>
                  )}
                </div>
                <button
                  type="button"
                  className="icon-button icon-button-sm"
                  aria-label={`Bỏ ${person.fullName}`}
                  onClick={() => toggle(person)}
                >
                  <Icon name="x" size={16} />
                </button>
              </li>
            )
          })}
        </ul>
      )}

      <label className="toolbar-check">
        <input type="checkbox" checked={includeMe} onChange={(e) => onIncludeMe(e.target.checked)} />
        Tính cả lịch của tôi
      </label>
    </section>
  )
}
