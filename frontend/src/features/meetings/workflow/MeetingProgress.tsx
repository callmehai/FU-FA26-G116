import { useId, useLayoutEffect, useRef, useState } from 'react'
import { Icon } from '../../../components/Icon'
import { formatDateTime } from '../../../lib/format'
import type { Meeting, MeetingHistoryItem, MeetingStatus } from '../../../types/meeting'
import { historyActionLabel } from './historyLabels'

const FLOW: MeetingStatus[] = ['Draft', 'WaitingApproval', 'Approved', 'Published', 'InProgress', 'Ended']
/** The approval steps, shown only to those who take part in it. */
const APPROVAL = new Set<MeetingStatus>(['Draft', 'WaitingApproval', 'Approved'])

const STEP_LABEL: Partial<Record<MeetingStatus, string>> = {
  Draft: 'Soạn thảo',
  WaitingApproval: 'Trình duyệt',
  Rejected: 'Trả lại',
  Approved: 'Phê duyệt',
  Published: 'Công bố',
  InProgress: 'Họp',
  Ended: 'Kết thúc',
  Cancelled: 'Đã huỷ',
}

/** Where the strip's open/folded choice survives a reload. A per-browser convenience only. */
const OPEN_KEY = 'tc.progressOpen'

interface Step {
  status: MeetingStatus
  state: 'done' | 'now' | 'stop' | 'todo'
  event?: MeetingHistoryItem
}

/**
 * Where the meeting stands. A rejection stops it at the approval step; a cancellation stops it at
 * the step it was called off from. Each reached step carries the last history row that reached it.
 */
function stepsOf(meeting: Meeting, history: MeetingHistoryItem[], showApproval: boolean): Step[] {
  const flow = showApproval ? FLOW : FLOW.filter((s) => !APPROVAL.has(s))
  const cancelledFrom = meeting.status === 'Cancelled' ? history.findLast((h) => h.resultingStatus === 'Cancelled')?.fromStatus : null
  const stopAt =
    meeting.status === 'Rejected'
      ? 'WaitingApproval'
      : meeting.status === 'Cancelled'
        ? (cancelledFrom ?? 'Draft')
        : null
  const at = Math.max(0, flow.indexOf(stopAt ?? meeting.status))
  return flow.map((status, i): Step => {
    const state = i < at ? 'done' : i === at ? (stopAt ? 'stop' : 'now') : 'todo'
    const shown = state === 'stop' ? meeting.status : status
    const event = state === 'todo' ? undefined : history.findLast((h) => h.resultingStatus === shown)
    return { status: shown, state, event }
  })
}

function readOpen() {
  try {
    return localStorage.getItem(OPEN_KEY) !== '0'
  } catch {
    return true
  }
}

/**
 * The meeting's lifecycle as one strip of steps under its title (AI-eCabinet's MeetingProgress):
 * each reached step says, on hover or focus, what happened, when and by whom; the current one stands out. It
 * folds to one line naming the current step.
 */
export function MeetingProgress({
  meeting,
  history,
  showApproval,
}: {
  meeting: Meeting
  history: MeetingHistoryItem[]
  showApproval: boolean
}) {
  const steps = stepsOf(meeting, history, showApproval)
  const [open, setOpenState] = useState(readOpen)
  const listId = useId()
  const at = steps.findIndex((s) => s.state === 'now' || s.state === 'stop')

  function setOpen(next: boolean) {
    setOpenState(next)
    try {
      localStorage.setItem(OPEN_KEY, next ? '1' : '0')
    } catch {
      // Storage blocked (private window): the choice just does not outlive the tab.
    }
  }

  // Where the strip is wider than the screen (a phone), it opens scrolled to the current step.
  const listRef = useRef<HTMLOListElement>(null)
  useLayoutEffect(() => {
    const list = listRef.current
    const step = list?.children[at] as HTMLElement | undefined
    if (list && step && list.scrollWidth > list.clientWidth)
      list.scrollLeft = step.offsetLeft - (list.clientWidth - step.offsetWidth) / 2
  }, [open, at])

  return (
    <div className="progress">
      <div className="progress-head">
        <p className="progress-now">
          <span className="progress-label">Tiến trình cuộc họp</span>
          {at >= 0 && (
            <>
              <span className="progress-label">{' · '}</span>
              <b className={steps[at].state === 'stop' ? 'progress-stop' : undefined}>
                Giai đoạn {at + 1}: {STEP_LABEL[steps[at].status]}
              </b>
            </>
          )}
        </p>
        <button
          type="button"
          className="btn btn-outline"
          aria-expanded={open}
          aria-controls={listId}
          onClick={() => setOpen(!open)}
        >
          {/* Both labels share one cell, so the button keeps its width when it flips. */}
          <span className="progress-toggle">
            <span aria-hidden={open}>Xem tiến trình</span>
            <span aria-hidden={!open}>Ẩn tiến trình</span>
          </span>
          <span className={open ? 'progress-caret progress-caret-open' : 'progress-caret'}>
            <Icon name="chevronDown" size={16} />
          </span>
        </button>
      </div>
      {open && (
        <ol ref={listRef} id={listId} className="progress-steps" aria-label="Tiến trình cuộc họp">
          {steps.map((s) => {
            const when = s.event && `${historyActionLabel(s.event)}: ${formatDateTime(s.event.occurredAt)} · ${s.event.actorName}`
            return (
              <li
                key={s.status}
                className={`progress-step progress-step-${s.state}`}
                aria-current={s.state === 'now' || s.state === 'stop' ? 'step' : undefined}
                tabIndex={when ? 0 : undefined}
                data-tooltip={when}
              >
                {s.state === 'done' && <Icon name="check" size={12} />}
                {s.state === 'stop' && <Icon name="x" size={12} />}
                {STEP_LABEL[s.status]}
                {when && <span className="visually-hidden">, {when}</span>}
              </li>
            )
          })}
        </ol>
      )}
    </div>
  )
}
