import { Link, useNavigate } from 'react-router-dom'
import { createMeeting } from '../../api/meetings'
import { PageHeader } from '../../components/PageHeader'
import { useToast } from '../../components/toastContext'
import { routes } from '../../lib/routes'
import { useAccessLost } from '../auth/useAccessLost'
import { MeetingForm } from './MeetingForm'

/**
 * UC-11 Create Meeting (SCRUM-17). The meeting is saved as a draft; attendees, agenda and the
 * request for approval come afterwards, from the detail page.
 */
export function CreateMeetingPage() {
  const navigate = useNavigate()
  const toast = useToast()
  const handleAccessLost = useAccessLost()

  return (
    <>
      <nav className="breadcrumb" aria-label="Đường dẫn">
        <Link to={routes.meetings}>Cuộc họp</Link>
        <span aria-hidden="true">›</span>
        <span>Tạo cuộc họp</span>
      </nav>

      <PageHeader
        title="Tạo cuộc họp"
        description="Cuộc họp được lưu ở trạng thái Nháp. Bạn vẫn sửa được cho tới khi gửi duyệt."
      />

      <div className="form-page">
        <MeetingForm
          meeting={null}
          submitLabel="Tạo cuộc họp"
          pendingLabel="Đang tạo…"
          onSubmit={createMeeting}
          onSaved={(response) => {
            toast.show(response.message)
            // Replace: going back from the new meeting should not reopen an already-submitted form.
            navigate(routes.meeting(response.meeting.id), { replace: true })
          }}
          onError={handleAccessLost}
          onCancel={() => navigate(routes.meetings)}
        />
      </div>
    </>
  )
}
