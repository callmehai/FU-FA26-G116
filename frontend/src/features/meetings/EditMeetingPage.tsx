import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { updateMeeting } from '../../api/meetings'
import { BackLink } from '../../components/BackLink'
import { NotFound } from '../../components/NotFound'
import { PageHeader } from '../../components/PageHeader'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { useForwardedBackState } from '../../lib/backTarget'
import { routes } from '../../lib/routes'
import { EDITABLE_STATUSES, MEETING_STATUS_LABEL, RESCHEDULABLE_STATUSES } from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'
import { useAuth } from '../auth/useAuth'
import { MeetingForm } from './MeetingForm'
import { useMeeting } from './useMeeting'

/**
 * UC-15 Update Meeting. The form is filled from a fresh GET rather than from whatever screen linked
 * here, and only the meeting's creator may edit it: fully in Draft or Rejected, and in Published
 * only to reschedule (AF-1).
 */
export function EditMeetingPage() {
  const { meetingId } = useParams()
  const { user } = useAuth()
  const navigate = useNavigate()
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const { state, reload, retry } = useMeeting(meetingId)
  /** Set when a save hit 409: the meeting left Draft/Rejected while the form was open. */
  const [conflict, setConflict] = useState<string | null>(null)
  // Where the detail page's back link leads (the calendar, a filtered list) survives the edit.
  const forwardBack = useForwardedBackState()

  if (state.kind === 'notFound') return <NotFound />

  if (state.kind === 'loading' || state.kind === 'error') {
    return (
      <>
        <BackLink to={routes.meetings}>Cuộc họp</BackLink>
        {state.kind === 'loading' ? (
          <p className="page-loading">Đang tải thông tin cuộc họp…</p>
        ) : (
          <div className="alert alert-error page-alert" role="alert">
            <p>{state.message}</p>
            <button type="button" className="btn btn-outline btn-sm" onClick={retry}>
              Thử lại
            </button>
          </div>
        )}
      </>
    )
  }

  const { meeting } = state
  // PUT answers 404 to anyone but the creator; say the same here instead of showing a form that
  // cannot be saved.
  if (meeting.createdById !== user?.id) return <NotFound />

  const reschedule = RESCHEDULABLE_STATUSES.includes(meeting.status)
  const editable = EDITABLE_STATUSES.includes(meeting.status) || reschedule
  const backToMeeting = () => navigate(routes.meeting(meeting.id), { state: forwardBack })

  return (
    <>
      <BackLink to={routes.meeting(meeting.id)} state={forwardBack}>
        {meeting.title}
      </BackLink>

      <PageHeader title={reschedule ? 'Dời lịch cuộc họp' : 'Chỉnh sửa cuộc họp'} description={meeting.title} />

      <div className="form-page">
        {!editable ? (
          <div className="alert alert-warning page-alert" role="alert">
            <p>
              {conflict ?? 'Cuộc họp này không thể chỉnh sửa được nữa.'} Trạng thái hiện tại:{' '}
              <b>{MEETING_STATUS_LABEL[meeting.status]}</b>. Chỉ sửa được khi cuộc họp ở trạng thái Nháp hoặc Bị từ
              chối; khi Đã công bố chỉ dời được lịch.
            </p>
            <button type="button" className="btn btn-outline btn-sm" onClick={backToMeeting}>
              Về trang cuộc họp
            </button>
          </div>
        ) : (
          <>
            {meeting.status === 'Rejected' && (
              <p className="alert alert-info page-alert">
                Cuộc họp đang ở trạng thái Bị từ chối. Lưu thay đổi không tự gửi duyệt lại — sau khi lưu, bấm “Gửi duyệt
                lại” ở trang chi tiết.
              </p>
            )}

            <MeetingForm
              meeting={meeting}
              submitLabel={reschedule ? 'Lưu và thông báo' : 'Lưu thay đổi'}
              pendingLabel="Đang lưu…"
              onSubmit={(body) => updateMeeting(meeting.id, body)}
              onSaved={(response) => {
                toast.show(response.message)
                navigate(routes.meeting(meeting.id), { replace: true, state: forwardBack })
              }}
              onError={(err) => {
                if (handleAccessLost(err)) return true
                if (isApiError(err, 409, 'MSG33')) {
                  // Contract: show the reason, then reload the current state before allowing more edits.
                  setConflict(errorMessage(err))
                  void reload()
                  return true
                }
                if (isApiError(err, 404)) {
                  toast.show('Cuộc họp không còn tồn tại hoặc bạn không còn quyền chỉnh sửa.', 'error')
                  navigate(routes.meetings, { replace: true })
                  return true
                }
                return false
              }}
              onCancel={backToMeeting}
            />
          </>
        )}
      </div>
    </>
  )
}
