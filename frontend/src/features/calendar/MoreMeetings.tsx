import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../components/Icon'
import { formatClock } from '../../lib/format'
import { routes } from '../../lib/routes'
import { MEETING_STATUS_LABEL } from '../../types/meeting'
import { STATUS_TONE } from '../meetings/statusTone'
import { CalendarPopover } from './CalendarPopover'
import { OverviewContent } from './MeetingOverview'
import type { ScheduleEntry } from './useSchedule'

/**
 * The meetings behind a "+N", as AI-eCabinet's calendar opens them: a short list beside the "+N" (a bottom sheet on a
 * phone), each row tinted like its tile. Picking one opens its overview beside the list on a wide screen, or in the
 * list's place, with a way back, where the two do not fit (below 900px). A running call room keeps its "Vào phòng
 * họp" on the row, so joining is one click from here too.
 */
export function MoreMeetings({
  entries,
  title,
  anchor,
  keyboard,
  onClose,
  onAnswered,
}: {
  entries: ScheduleEntry[]
  /** "3 cuộc họp khác", or the day and its count when the list is a whole day's. */
  title: string
  anchor: HTMLElement
  keyboard: boolean
  onClose: () => void
  onAnswered: (response: 'Accepted' | 'Declined') => void
}) {
  const [pickedId, setPickedId] = useState<string | null>(null)
  const picked = entries.find((e) => e.meeting.id === pickedId) ?? null
  const rows = useRef(new Map<string, HTMLButtonElement>())
  const returnTo = useRef<string | null>(null)

  // Back from the overview puts focus on the row it came from, which the narrow layout had hidden.
  useEffect(() => {
    if (pickedId !== null || !returnTo.current) return
    rows.current.get(returnTo.current)?.focus()
    returnTo.current = null
  }, [pickedId])

  return (
    <CalendarPopover
      anchor={anchor}
      keyboard={keyboard}
      label={title}
      className="more-popover"
      // The list stays where it opened while an overview shows beside it or in its place.
      keepPlace={picked !== null}
      onClose={onClose}
    >
      <div className={picked ? 'more-panel more-panel-picked' : 'more-panel'}>
        <div className="more-list">
          <p className="more-title">{title}</p>
          <ul>
            {entries.map((entry) => (
              <MoreRow
                key={entry.meeting.id}
                entry={entry}
                picked={entry.meeting.id === pickedId}
                rowRef={(el) => {
                  if (el) rows.current.set(entry.meeting.id, el)
                  else rows.current.delete(entry.meeting.id)
                }}
                onPick={() => setPickedId(entry.meeting.id)}
              />
            ))}
          </ul>
        </div>
        {picked && (
          <div className="more-overview">
            <OverviewContent
              key={picked.meeting.id}
              item={picked.meeting}
              onClose={onClose}
              onAnswered={(response) => {
                if (response === 'Declined') setPickedId(null)
                onAnswered(response)
              }}
              onBack={() => {
                returnTo.current = picked.meeting.id
                setPickedId(null)
              }}
            />
          </div>
        )}
      </div>
    </CalendarPopover>
  )
}

function MoreRow({
  entry,
  picked,
  rowRef,
  onPick,
}: {
  entry: ScheduleEntry
  picked: boolean
  rowRef: (el: HTMLButtonElement | null) => void
  onPick: () => void
}) {
  const { meeting } = entry
  const time = `${formatClock(entry.start.toISOString())} – ${formatClock(entry.end.toISOString())}`
  const goIn = meeting.status === 'InProgress' && meeting.mode !== 'InPerson'
  const cls = [
    'more-row',
    `cal-event-${STATUS_TONE[meeting.status]}`,
    meeting.status === 'Cancelled' ? 'cal-cancelled' : '',
    goIn ? 'more-row-go' : '',
  ]
  return (
    <li>
      <button
        ref={rowRef}
        type="button"
        className={cls.filter(Boolean).join(' ')}
        aria-current={picked}
        aria-label={`${meeting.title}, ${time}, ${MEETING_STATUS_LABEL[meeting.status]}`}
        onClick={onPick}
      >
        <span className="more-row-time">
          {time}
          {meeting.sensitivity === 'Secret' && (
            <span className="cal-event-secret">
              <Icon name="lock" size={11} />
              Mật
            </span>
          )}
        </span>
        <span className="more-row-title">{meeting.title}</span>
      </button>
      {/* A sibling laid over the row's foot: a link cannot sit inside a button. */}
      {goIn && (
        <Link className="cal-go more-go" to={routes.meetingRoom(meeting.id)}>
          Vào phòng họp
          <Icon name="arrowRight" size={13} />
        </Link>
      )}
    </li>
  )
}
