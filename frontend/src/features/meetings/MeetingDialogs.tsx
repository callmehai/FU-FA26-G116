import { useCallback, useRef } from 'react'
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { createMeeting, updateMeeting } from '../../api/meetings'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { routes } from '../../lib/routes'
import type { Meeting } from '../../types/meeting'
import { RESCHEDULABLE_STATUSES } from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'
import { useAuth } from '../auth/useAuth'
import { MeetingForm } from './MeetingForm'
import { CREATE_PARAM, EDIT_PARAM, type MeetingDialogState } from './meetingDialogLink'

/**
 * Closes a dialog that lives in the URL: Back when the app pushed the entry (so history does not
 * stack), otherwise drop the parameter in place. Guarded, since Back lands asynchronously and a
 * second Escape before it would step back twice.
 */
function useCloseParam(param: string) {
  const [, setParams] = useSearchParams()
  const { state, key } = useLocation()
  const navigate = useNavigate()
  const closedAt = useRef<string | null>(null)
  return useCallback(() => {
    if (closedAt.current === key) return
    closedAt.current = key
    if ((state as MeetingDialogState | null)?.meetingDialogPushed) {
      navigate(-1)
      return
    }
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        next.delete(param)
        return next
      },
      { replace: true },
    )
  }, [key, state, navigate, setParams, param])
}

/**
 * UC-14 Create Meeting, as a dialog over whatever page carries `?create=` (AI-eCabinet's
 * CreateMeetingHost). Mounted once in the shell. The meeting is saved as a draft with who attends
 * (SCRUM-20); the detail page then takes the agenda, documents and the request for approval.
 */
export function CreateMeetingHost() {
  const { can } = useAuth()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const close = useCloseParam(CREATE_PARAM)
  const day = params.get(CREATE_PARAM)
  if (day === null || !can('MeetingCreator')) return null

  return (
    <MeetingForm
      // A new opening starts from a fresh form.
      key={day}
      meeting={null}
      day={day}
      title="Tạo cuộc họp"
      submitLabel="Tạo cuộc họp"
      pendingLabel="Đang tạo…"
      onSubmit={(body) => createMeeting({ ...body, participants: body.participants ?? [] })}
      onSaved={(response) => {
        toast.show(response.message)
        // Replace: Back from the new meeting should not reopen an already-submitted form.
        navigate(routes.meeting(response.meeting.id), { replace: true })
      }}
      onError={handleAccessLost}
      onClose={close}
    />
  )
}

/**
 * UC-15 Update Meeting, as a dialog on the meeting's page (`?edit=1`). The page decides whether to
 * offer it (the creator, Draft or Rejected; Published only to reschedule, AF-1) and passes the
 * meeting it already shows. The backend re-checks ownership and status on save.
 */
export function EditMeetingDialog({
  meeting,
  onSaved,
  onStale,
}: {
  meeting: Meeting
  onSaved: (meeting: Meeting) => void
  /** The meeting changed underneath (409) or is gone (404): the page re-reads it. */
  onStale: () => void
}) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const close = useCloseParam(EDIT_PARAM)
  const reschedule = RESCHEDULABLE_STATUSES.includes(meeting.status)

  return (
    <MeetingForm
      meeting={meeting}
      title={reschedule ? 'Dời lịch cuộc họp' : 'Chỉnh sửa cuộc họp'}
      notice={
        meeting.status === 'Rejected' && (
          <p className="alert alert-info">
            Cuộc họp đang ở trạng thái Bị từ chối. Lưu thay đổi không tự gửi duyệt lại — sau khi lưu, bấm “Gửi duyệt
            lại” ở trang chi tiết.
          </p>
        )
      }
      submitLabel={reschedule ? 'Lưu và thông báo' : 'Lưu thay đổi'}
      pendingLabel="Đang lưu…"
      onSubmit={(body) => updateMeeting(meeting.id, body)}
      onSaved={(response) => {
        toast.show(response.message)
        onSaved(response.meeting)
        close()
      }}
      onError={(err) => {
        if (handleAccessLost(err)) return true
        // The meeting left Draft/Rejected (MSG33), or is no longer this user's: say why, close
        // the form, and show what is true now.
        if (isApiError(err, 409) || isApiError(err, 404)) {
          toast.show(
            isApiError(err, 404) ? 'Cuộc họp không còn tồn tại hoặc bạn không còn quyền chỉnh sửa.' : errorMessage(err),
            'error',
          )
          close()
          onStale()
          return true
        }
        return false
      }}
      onClose={close}
    />
  )
}
