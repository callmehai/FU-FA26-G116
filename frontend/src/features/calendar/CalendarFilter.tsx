import { useEffect, useRef, useState } from 'react'
import { Icon } from '../../components/Icon'
import {
  MEETING_STATUS_LABEL,
  SENSITIVITIES,
  SENSITIVITY_LABEL,
  type MeetingStatus,
  type Sensitivity,
} from '../../types/meeting'
import { STATUS_TONE } from '../meetings/statusTone'
import {
  NO_FILTER,
  STATUSES,
  activeGroups,
  matchesSensitivity,
  matchesStatus,
  toggle,
  type CalendarFilterValue,
} from './filter'
import type { ScheduleEntry } from './useSchedule'

const PANEL_ID = 'calendar-filter'

/**
 * "Bộ lọc" on the calendar (SCRUM-47): a non-modal panel under the button. Each chip filters at
 * once; "Xong" only closes. It narrows what the server already allowed the caller to see — the
 * server filters by visibility (BR-29), so this is a view, never a permission boundary.
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
  const byStatus = new Map<MeetingStatus, number>()
  const bySensitivity = new Map<Sensitivity, number>()
  for (const entry of entries ?? []) {
    const { status, sensitivity } = entry.meeting
    if (matchesSensitivity(entry, value)) byStatus.set(status, (byStatus.get(status) ?? 0) + 1)
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
        aria-label={active > 0 ? `Bộ lọc, ${active} nhóm đang lọc` : 'Bộ lọc'}
        title="Bộ lọc"
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
        <div className="cal-filter-panel" id={PANEL_ID} role="dialog" aria-label="Bộ lọc lịch">
          <fieldset className="cal-filter-group">
            <legend>Trạng thái</legend>
            <div className="cal-filter-chips">
              {STATUSES.map((status) => (
                <button
                  key={status}
                  type="button"
                  className="cal-pick"
                  aria-pressed={value.statuses.includes(status)}
                  onClick={() => onChange({ ...value, statuses: toggle(value.statuses, status, STATUSES) })}
                >
                  <span className={`cal-dot cal-dot-${STATUS_TONE[status]}`} aria-hidden="true" />
                  {MEETING_STATUS_LABEL[status]}
                  {entries && <span className="cal-pick-count">{byStatus.get(status) ?? 0}</span>}
                </button>
              ))}
            </div>
          </fieldset>

          <fieldset className="cal-filter-group">
            <legend>Mức độ</legend>
            <div className="cal-filter-chips">
              {SENSITIVITIES.map((sensitivity) => (
                <button
                  key={sensitivity}
                  type="button"
                  className="cal-pick"
                  aria-pressed={value.sensitivities.includes(sensitivity)}
                  onClick={() =>
                    onChange({ ...value, sensitivities: toggle(value.sensitivities, sensitivity, SENSITIVITIES) })
                  }
                >
                  {SENSITIVITY_LABEL[sensitivity]}
                  {entries && <span className="cal-pick-count">{bySensitivity.get(sensitivity) ?? 0}</span>}
                </button>
              ))}
            </div>
            <p className="cal-filter-hint">Không chọn gì là hiện mọi cuộc họp bạn được xem.</p>
          </fieldset>

          <div className="cal-filter-foot">
            <button type="button" className="btn btn-ghost" disabled={active === 0} onClick={() => onChange(NO_FILTER)}>
              Bỏ lọc
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                setOpen(false)
                buttonRef.current?.focus()
              }}
            >
              Xong
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
