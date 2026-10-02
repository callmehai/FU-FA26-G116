import { useEffect, useState, type ReactNode } from 'react'
import { transitionMeeting } from '../../../api/meetings'
import { ConfirmDialog } from '../../../components/ConfirmDialog'
import { Icon, type IconName } from '../../../components/Icon'
import { formatDateTime } from '../../../lib/format'
import type { Meeting, MeetingApprover, MeetingHistoryItem } from '../../../types/meeting'
import { latestOf } from './historyLabels'
import { RejectMeetingDialog } from './RejectMeetingDialog'
import { SubmitForApprovalDialog } from './SubmitForApprovalDialog'
import { useSubmissionReadiness, type ReadinessState } from './useSubmissionReadiness'
import { workflowFailure, type WorkflowOutcome } from './workflowOutcome'

/** Who is looking, as far as the approval workflow is concerned. Everyone else sees no bar. */
export type WorkflowRole = 'organizer' | 'approver'

type Tone = 'slate' | 'amber' | 'red' | 'emerald' | 'warning'
type OpenDialog = 'submit' | 'approve' | 'reject' | 'returnToDraft' | null

/**
 * The approval workflow of one meeting (SCRUM-19, UC-19 … UC-20), as a bar under the page header:
 * where the meeting stands, who it is waiting on, and the next step for the person looking —
 * with that step's button right there. It only appears for the organizer and the current approver,
 * and only until the meeting is published.
 *
 * What the caller may do is decided by the server (the meeting's organizer and current approver,
 * and the caller's capabilities); the buttons only mirror it, and every action is re-checked.
 */
export function MeetingWorkflowBar({
  meeting,
  role,
  history,
  approvers,
  nameOf,
  onOutcome,
}: {
  meeting: Meeting
  role: WorkflowRole
  /** The loaded timeline, for who did what and when. May still be empty while it loads. */
  history: MeetingHistoryItem[]
  /** The organizer's view of who may approve; null for the approver, who cannot list them. */
  approvers: { list: MeetingApprover[] | null; error: string | null; reload: () => void } | null
  nameOf: (id: string) => string | null
  onOutcome: (outcome: WorkflowOutcome) => void
}) {
  const [dialog, setDialog] = useState<OpenDialog>(null)
  const checkReadiness = role === 'organizer' && meeting.status === 'Draft'
  const { readiness, retry } = useSubmissionReadiness(meeting.id, checkReadiness, meeting.updatedAt)

  // The readiness check is the first to learn that the draft was submitted elsewhere or is gone.
  useEffect(() => {
    if (readiness.kind !== 'error') return
    const failure = workflowFailure(readiness.failure)
    if (failure) onOutcome(failure)
  }, [readiness, onOutcome])

  const finish = (outcome: WorkflowOutcome) => {
    setDialog(null)
    onOutcome(outcome)
  }

  /** For the confirm-only actions: an ending failure closes the dialog, anything else stays in it. */
  async function confirm(action: 'Approve' | 'ReturnToDraft') {
    try {
      const response = await transitionMeeting(meeting.id, { action })
      onOutcome({ kind: 'done', response })
    } catch (err) {
      const failure = workflowFailure(err)
      if (!failure) throw err
      onOutcome(failure)
    }
  }

  const view =
    role === 'organizer'
      ? organizerView(meeting, history, approvers?.list ?? null, nameOf, readiness, retry, setDialog)
      : approverView(meeting, history, setDialog)
  if (!view) return null

  return (
    <>
      <section className={`workflow-bar workflow-${view.tone}`} aria-labelledby="workflow-title">
        <span className="workflow-icon" aria-hidden="true">
          <Icon name={view.icon} size={18} />
        </span>
        <div className="workflow-body">
          <h2 id="workflow-title">{view.title}</h2>
          {view.body}
        </div>
        {view.actions && <div className="workflow-actions">{view.actions}</div>}
      </section>

      {dialog === 'submit' && approvers && (
        <SubmitForApprovalDialog
          meeting={meeting}
          approvers={approvers}
          onDone={finish}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog === 'reject' && <RejectMeetingDialog meeting={meeting} onDone={finish} onClose={() => setDialog(null)} />}
      {dialog === 'approve' && (
        <ConfirmDialog
          title="Duyệt cuộc họp"
          confirmLabel="Duyệt"
          pendingLabel="Đang duyệt…"
          onConfirm={() => confirm('Approve')}
          onClose={() => setDialog(null)}
        >
          Duyệt “{meeting.title}”? Sau khi duyệt, người tổ chức có thể công bố cuộc họp tới thành phần tham dự.
        </ConfirmDialog>
      )}
      {dialog === 'returnToDraft' &&
        (meeting.status === 'WaitingApproval' ? (
          <ConfirmDialog
            title="Rút về nháp"
            confirmLabel="Rút về nháp"
            pendingLabel="Đang rút…"
            onConfirm={() => confirm('ReturnToDraft')}
            onClose={() => setDialog(null)}
          >
            Cuộc họp trở về trạng thái Nháp. Sau đó bạn chọn người duyệt khác và gửi duyệt lại.
          </ConfirmDialog>
        ) : (
          <ConfirmDialog
            title="Đưa về nháp"
            confirmLabel="Đưa về nháp"
            pendingLabel="Đang chuyển…"
            onConfirm={() => confirm('ReturnToDraft')}
            onClose={() => setDialog(null)}
          >
            Cuộc họp trở về trạng thái Nháp. Bạn sửa thông tin nếu cần, rồi gửi duyệt lại cho cùng người hoặc người
            duyệt khác. Lý do từ chối vẫn được giữ trong lịch sử.
          </ConfirmDialog>
        ))}
    </>
  )
}

interface View {
  tone: Tone
  icon: IconName
  title: string
  body: ReactNode
  actions?: ReactNode
}

function organizerView(
  meeting: Meeting,
  history: MeetingHistoryItem[],
  approverList: MeetingApprover[] | null,
  nameOf: (id: string) => string | null,
  readiness: ReadinessState,
  retryReadiness: () => void,
  open: (dialog: OpenDialog) => void,
): View | null {
  switch (meeting.status) {
    case 'Draft': {
      const lastReject = latestOf(history, 'Reject')
      const lastSubmit = latestOf(history, 'SubmitForApproval')
      // Back in draft after a rejection: the reason is what the organizer is fixing.
      const rejectedBefore = lastReject && (!lastSubmit || lastReject.occurredAt > lastSubmit.occurredAt)
      const canSubmit = readiness.kind === 'ready' || readiness.kind === 'error'
      return {
        tone: 'slate',
        icon: 'fileText',
        title: 'Bản nháp — chưa gửi duyệt',
        body: (
          <>
            <ReadinessText readiness={readiness} onRetry={retryReadiness} />
            {rejectedBefore && lastReject.rejectionReason && (
              <RejectionNote item={lastReject} lead="Lần duyệt trước bị từ chối" />
            )}
          </>
        ),
        actions: (
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => open('submit')}
            disabled={!canSubmit}
            title={canSubmit ? undefined : 'Hoàn thiện thông tin trước khi gửi duyệt'}
          >
            <Icon name="send" size={16} />
            Gửi duyệt
          </button>
        ),
      }
    }
    case 'WaitingApproval': {
      const submit = latestOf(history, 'SubmitForApproval')
      const approverId = meeting.assignedApproverId
      // The list holds every account that may still approve, so someone missing from it was
      // deactivated or lost the capability — the one case the organizer may withdraw (MSG64 else).
      const stuck = approverList !== null && !approverList.some((a) => a.id === approverId)
      if (stuck) {
        return {
          tone: 'warning',
          icon: 'clock',
          title: 'Người duyệt không còn quyền duyệt',
          body: (
            <p>
              Tài khoản được chọn để duyệt đã bị vô hiệu hoá hoặc không còn quyền duyệt, nên cuộc họp không thể được
              duyệt. Rút về nháp để chọn người duyệt khác.
            </p>
          ),
          actions: (
            <button type="button" className="btn btn-primary" onClick={() => open('returnToDraft')}>
              Rút về nháp
            </button>
          ),
        }
      }
      const name = approverId ? nameOf(approverId) : null
      return {
        tone: 'amber',
        icon: 'clock',
        title: name ? `Đang chờ ${name} duyệt` : 'Đang chờ duyệt',
        body: (
          <p>
            {submit ? `Bạn gửi duyệt lúc ${formatDateTime(submit.occurredAt)}. ` : ''}
            Thông tin cuộc họp không sửa được trong lúc chờ.
          </p>
        ),
      }
    }
    case 'Rejected': {
      const reject = latestOf(history, 'Reject')
      return {
        tone: 'red',
        icon: 'x',
        title: 'Cuộc họp bị từ chối',
        body: (
          <>
            {reject && <RejectionNote item={reject} />}
            <p>Sửa thông tin nếu cần, rồi đưa cuộc họp về nháp để gửi duyệt lại.</p>
          </>
        ),
        actions: (
          <button type="button" className="btn btn-primary" onClick={() => open('returnToDraft')}>
            Đưa về nháp
          </button>
        ),
      }
    }
    case 'Approved': {
      const approve = latestOf(history, 'Approve')
      return {
        tone: 'emerald',
        icon: 'checkCircle',
        title: 'Cuộc họp đã được duyệt',
        body: approve ? (
          <p>
            {approve.actorName} đã duyệt lúc {formatDateTime(approve.occurredAt)}.
          </p>
        ) : null,
      }
    }
    default:
      return null
  }
}

function approverView(
  meeting: Meeting,
  history: MeetingHistoryItem[],
  open: (dialog: OpenDialog) => void,
): View | null {
  switch (meeting.status) {
    case 'WaitingApproval': {
      const submit = latestOf(history, 'SubmitForApproval')
      return {
        tone: 'amber',
        icon: 'clock',
        title: 'Cuộc họp đang chờ bạn duyệt',
        body: (
          <p>
            {submit ? `${submit.actorName} gửi cho bạn lúc ${formatDateTime(submit.occurredAt)}. ` : ''}
            Xem kỹ thông tin cuộc họp trước khi quyết định.
          </p>
        ),
        actions: (
          <>
            <button type="button" className="btn btn-outline" onClick={() => open('reject')}>
              Từ chối
            </button>
            <button type="button" className="btn btn-primary" onClick={() => open('approve')}>
              <Icon name="check" size={16} />
              Duyệt
            </button>
          </>
        ),
      }
    }
    case 'Rejected': {
      const reject = latestOf(history, 'Reject')
      return {
        tone: 'slate',
        icon: 'x',
        title: 'Bạn đã từ chối cuộc họp này',
        body: (
          <>
            {reject && <RejectionNote item={reject} byYou />}
            <p>Người tổ chức có thể sửa và gửi duyệt lại.</p>
          </>
        ),
      }
    }
    case 'Approved': {
      const approve = latestOf(history, 'Approve')
      return {
        tone: 'emerald',
        icon: 'checkCircle',
        title: 'Bạn đã duyệt cuộc họp này',
        body: (
          <p>
            {approve ? `Duyệt lúc ${formatDateTime(approve.occurredAt)}. ` : ''}
            Người tổ chức sẽ công bố cuộc họp tới thành phần tham dự.
          </p>
        ),
      }
    }
    default:
      return null
  }
}

function ReadinessText({ readiness, onRetry }: { readiness: ReadinessState; onRetry: () => void }) {
  switch (readiness.kind) {
    case 'ready':
      return <p>Thông tin cuộc họp đã đủ. Gửi cho người duyệt khi bạn sẵn sàng.</p>
    case 'notReady':
      return (
        <>
          <p>Cần hoàn thiện trước khi gửi duyệt:</p>
          <ul className="readiness-list">
            {readiness.problems.map((problem) => (
              <li key={problem}>{problem}</li>
            ))}
          </ul>
        </>
      )
    case 'error':
      return (
        <p>
          Chưa kiểm tra được thông tin gửi duyệt: {readiness.message}{' '}
          <button type="button" className="link-button" onClick={onRetry}>
            Thử lại
          </button>
        </p>
      )
    default:
      return <p>Đang kiểm tra thông tin trước khi gửi duyệt…</p>
  }
}

/** Who rejected, when, and why — the why only when the server shows it to this caller. */
function RejectionNote({ item, lead, byYou = false }: { item: MeetingHistoryItem; lead?: string; byYou?: boolean }) {
  const when = formatDateTime(item.occurredAt)
  return (
    <>
      <p>
        {lead ? `${lead}: ` : ''}
        {byYou ? `Từ chối lúc ${when}.` : `${item.actorName} từ chối lúc ${when}.`}
      </p>
      {item.rejectionReason && <blockquote className="workflow-reason">{item.rejectionReason}</blockquote>}
    </>
  )
}
