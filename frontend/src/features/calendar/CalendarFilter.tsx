import { useEffect, useRef, useState } from 'react'
import { Icon } from '../../components/Icon'
import { SENSITIVITIES, SENSITIVITY_LABEL, type Sensitivity } from '../../types/meeting'
import {
  DEFAULT_FILTER,
  GROUPS,
  GROUP_LABEL,
  GROUP_OF,
  GROUP_TONE,
  activeGroups,
  matchesSensitivity,
  matchesStatus,
  toggleGroup,
  type CalendarFilterValue,
  type StatusGroup,
} from './filter'
import type { ScheduleEntry } from './useSchedule'

const PANEL_ID = 'calendar-filter'

/**
 * "Bộ lọc" on the calendar (SCRUM-47), as AI-eCabinet's: status groups, opening on the meetings
 * that will happen (Đã duyệt, Đã công bố, Đang diễn ra), and one level. Each chip filters at once;
 * a chip with nothing in the range is dimmed, and the last status stays on. "Đặt lại" goes back to
 * the default; "Xong" only closes. It narrows what the server already allowed the caller to see —
 * the server filters by visibility (BR-29), so this is a view, never a permission boundary.
 */
export function CalendarFilter({
  value,
  entries,
  onChange,
}: {
  value: CalendarFilterValue
  /** The meetings in the range on screen, before filtering; null while they load. */
  entries: ScheduleEntry[] | null
  onChange: (next: CalendarFilterValue) => void
}) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const active = activeGroups(value)

  useEffect(() => {
    if (!open) return
    function onPointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false)
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape') return
      setOpen(false)
      buttonRef.current?.focus()
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  // Each chip counts what the other group lets through, so the number is what a click would add.
  const byGroup = new Map<StatusGroup, number>()
  const bySensitivity = new Map<Sensitivity, number>()
  for (const entry of entries ?? []) {
    const { status, sensitivity } = entry.meeting
    const group = GROUP_OF[status]
    if (matchesSensitivity(entry, value)) byGroup.set(group, (byGroup.get(group) ?? 0) + 1)
    if (matchesStatus(entry, value)) bySensitivity.set(sensitivity, (bySensitivity.get(sensitivity) ?? 0) + 1)
  }

  return (
    <div className="cal-filter" ref={rootRef}>
      <button
        ref={buttonRef}
        type="button"
        className={active > 0 ? 'btn btn-outline cal-filter-on' : 'btn btn-outline'}
        aria-expanded={open}
        aria-controls={open ? PANEL_ID : undefined}
        // The label hides on narrow windows (global.css), so the name lives here.
        aria-label={active > 0 ? `Bộ lọc, ${active} bộ lọc khác mặc định` : 'Bộ lọc'}
        data-tooltip="Bộ lọc"
        onClick={() => setOpen(!open)}
      >
        <Icon name="filter" size={18} />
        <span className="cal-filter-label">Bộ lọc</span>
        {active > 0 && (
          <span className="cal-filter-count" aria-hidden="true">
            {active}
          </span>
        )}
      </button>

      {open && (
        <div className="cal-filter-panel" id={PANEL_ID} role="group" aria-label="Bộ lọc lịch">
          <fieldset className="cal-filter-group">
            <legend>Trạng thái</legend>
            <div className="cal-filter-chips">
              {GROUPS.map((group) => {
                const on = value.groups.includes(group)
                const count = byGroup.get(group) ?? 0
                return (
                  <button
                    key={group}
                    type="button"
                    className="cal-pick"
                    aria-pressed={on}
                    disabled={!!entries && count === 0 && !on}
                    data-tooltip={group === 'ended' ? 'Gồm cả cuộc họp nháp' : undefined}
                    onClick={() => onChange({ ...value, groups: toggleGroup(value.groups, group) })}
                  >
                    <span className={`cal-dot cal-dot-${GROUP_TONE[group]}`} aria-hidden="true" />
                    {GROUP_LABEL[group]}
                    {entries && <span className="cal-pick-count">{count}</span>}
                  </button>
                )
              })}
            </div>
          </fieldset>

          <fieldset className="cal-filter-group">
            <legend>Mức độ</legend>
            <div className="cal-filter-chips">
              {SENSITIVITIES.map((sensitivity) => {
                const on = value.sensitivity === sensitivity
                const count = bySensitivity.get(sensitivity) ?? 0
                return (
                  <button
                    key={sensitivity}
                    type="button"
                    className="cal-pick"
                    aria-pressed={on}
                    disabled={!!entries && count === 0 && !on}
                    onClick={() => onChange({ ...value, sensitivity: on ? null : sensitivity })}
                  >
                    {SENSITIVITY_LABEL[sensitivity]}
                    {entries && <span className="cal-pick-count">{count}</span>}
                  </button>
                )
              })}
            </div>
          </fieldset>

          <div className="cal-filter-foot">
            <button
              type="button"
              className="btn btn-outline"
              disabled={active === 0}
              onClick={() => onChange(DEFAULT_FILTER)}
            >
              <Icon name="undo" size={16} />
              Đặt lại
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                setOpen(false)
                buttonRef.current?.focus()
              }}
            >
              <Icon name="check" size={16} />
              Xong
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
