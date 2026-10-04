import { useEffect, useRef, useState } from 'react'
import { transitionMeeting } from '../../../api/meetings'
import { Dialog } from '../../../components/Dialog'
import { Select, type SelectOption } from '../../../components/Select'
import { errorMessage, isApiError } from '../../../lib/errors'
import type { Meeting, MeetingApprover, MeetingTransitionResponse } from '../../../types/meeting'
import { readinessProblems } from './readiness'
import type { WorkflowOutcome } from './workflowOutcome'
import { workflowFailure } from './workflowOutcome'

/**
 * UC-19 Submit for approval: the organizer picks who reviews this cycle. The approver is chosen
 * here, not on the form — a draft has none (SCRUM-19). A rejected meeting goes through the same
 * dialog (UC-19 AF-1) and may pick a different approver. The server re-checks readiness and the
 * approver's eligibility in the same transaction, so both can still fail after the list was read.
 */
export function SubmitForApprovalDialog({
  meeting,
  approvers,
  onDone,
  onClose,
}: {
  meeting: Meeting
  approvers: { list: MeetingApprover[] | null; error: string | null; reload: () => void }
  onDone: (outcome: WorkflowOutcome) => void
  onClose: () => void
}) {
  const [approverId, setApproverId] = useState('')
  const [approverError, setApproverError] = useState<string | null>(null)
  const [problems, setProblems] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const fieldRef = useRef<HTMLDivElement>(null)

  // Who may approve can change while the page is open (someone deactivated, a role edited): read
  // the list again every time the dialog opens.
  const { reload: reloadApprovers } = approvers
  useEffect(() => {
    reloadApprovers()
  }, [reloadApprovers])

  const options: SelectOption<string>[] = (approvers.list ?? []).map((a) => ({ value: a.id, label: a.fullName }))
  const noneEligible = approvers.list?.length === 0

  async function submit() {
    if (!approverId) {
      setApproverError('Vui lòng chọn người duyệt.')
      fieldRef.current?.querySelector<HTMLElement>('.select-trigger')?.focus()
      return
    }
    setPending(true)
    setError(null)
    setProblems([])
    try {
      const response: MeetingTransitionResponse = await transitionMeeting(meeting.id, {
        action: 'SubmitForApproval',
        assignedApproverId: approverId,
      })
      onDone({ kind: 'done', response })
    } catch (err) {
      setPending(false)
      if (isApiError(err, 400, 'MSG81')) {
        // Something went missing since the bar last checked; show exactly what.
        setProblems(readinessProblems(err.body?.errors))
        setError(errorMessage(err))
        return
      }
      if (isApiError(err, 400, 'MSG82')) {
        // Deactivated or demoted while the dialog was open: refresh who can still be picked.
        setApproverError(errorMessage(err))
        setApproverId('')
        approvers.reload()
        return
      }
      const failure = workflowFailure(err)
      if (failure) onDone(failure)
      else setError(errorMessage(err))
    }
  }

  const close = () => {
    if (!pending) onClose()
  }

  return (
    <Dialog
      title={meeting.status === 'Rejected' ? 'Gửi duyệt lại cuộc họp' : 'Gửi duyệt cuộc họp'}
      description={meeting.title}
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-ghost" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button type="button" className="btn btn-primary" onClick={submit} disabled={pending || noneEligible}>
            {pending ? 'Đang gửi…' : meeting.status === 'Rejected' ? 'Gửi duyệt lại' : 'Gửi duyệt'}
          </button>
        </>
      }
    >
      <div className="field" ref={fieldRef}>
        <span className="field-required">Người duyệt</span>
        {approvers.list ? (
          noneEligible ? (
            <p className="alert alert-warning">
              Chưa có tài khoản nào được phân quyền duyệt cuộc họp. Liên hệ quản trị viên để phân quyền.
            </p>
          ) : (
            <Select
              value={approverId}
              options={options}
              onChange={(value) => {
                setApproverId(value)
                setApproverError(null)
              }}
              label="Chọn người duyệt"
            />
          )
        ) : approvers.error ? (
          <p className="field-error">
            {approvers.error}{' '}
            <button type="button" className="link-button" onClick={approvers.reload}>
              Thử lại
            </button>
          </p>
        ) : (
          <p className="field-hint">Đang tải danh sách người duyệt…</p>
        )}
        {approverError ? (
          <small className="field-error">{approverError}</small>
        ) : (
          !noneEligible && (
            <small className="field-hint">Người duyệt nhận thông báo và quyết định duyệt hoặc từ chối.</small>
          )
        )}
      </div>

      {!noneEligible && (
        <p className="dialog-text dialog-note">Trong lúc chờ duyệt, thông tin cuộc họp không sửa được.</p>
      )}

      {error && (
        <div className="alert alert-error dialog-error" role="alert">
          <p>{error}</p>
          {problems.length > 0 && (
            <ul className="readiness-list">
              {problems.map((problem) => (
                <li key={problem}>{problem}</li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Dialog>
  )
}
