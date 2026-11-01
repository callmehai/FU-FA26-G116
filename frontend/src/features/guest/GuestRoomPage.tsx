import { LobbyMessage, RoomLifecycle, RoomPage } from '../calls/room/MeetingRoomPage'
import { useGuest } from './guestContext'
import { useGuestCall } from './useGuestCall'

/**
 * The call room for a verified guest (SCRUM-59): the members' lobby and room, on the guest's token. The meeting comes
 * from the guest session — a guest may not read the meeting itself — and the guest page around this decides whether
 * the guest is still in: once the session, the invitation or the meeting is over, it replaces the room.
 */
export function GuestRoomPage() {
  const meeting = useGuest()!.session.meeting
  const { call, error, retry } = useGuestCall(meeting.id)

  if (!call && error) {
    return (
      <LobbyMessage meetingId={meeting.id} title="Không tải được phòng họp" text={error}>
        <button type="button" className="btn btn-primary" onClick={retry}>
          Thử lại
        </button>
      </LobbyMessage>
    )
  }
  if (!call) {
    return (
      <RoomPage>
        <p className="page-loading prejoin-loading">Đang tải phòng họp…</p>
      </RoomPage>
    )
  }
  return <RoomLifecycle meeting={meeting} call={call} />
}
