import { DisconnectReason, MediaDeviceFailure, type Participant, type RoomOptions, VideoPresets } from 'livekit-client'
import type { CallParticipantMetadata, MeetingRole } from '../../../types/call'

/**
 * Module-level on purpose: a new options object per render would give the Room a new
 * configuration. adaptiveStream and dynacast are off by default in livekit-client; both matter
 * once a room has more than a handful of people.
 */
export const ROOM_OPTIONS: RoomOptions = {
  adaptiveStream: true,
  dynacast: true,
  publishDefaults: { simulcast: true },
  videoCaptureDefaults: { resolution: VideoPresets.h720.resolution },
}

/** The meeting role the backend put in the participant's metadata, if any. */
export function meetingRoleOf(participant: Participant): MeetingRole | undefined {
  if (!participant.metadata) return undefined
  try {
    return (JSON.parse(participant.metadata) as CallParticipantMetadata).meetingRole
  } catch {
    return undefined
  }
}

/** Why the room ended for this tab, in words the user can act on. */
export function disconnectMessage(reason: DisconnectReason | undefined): { text: string; canRejoin: boolean } {
  switch (reason) {
    case DisconnectReason.CLIENT_INITIATED:
      return { text: 'Bạn đã rời phòng họp.', canRejoin: true }
    case DisconnectReason.DUPLICATE_IDENTITY:
      return { text: 'Bạn đã vào phòng này từ tab hoặc thiết bị khác. Tab này đã rời phòng.', canRejoin: true }
    case DisconnectReason.PARTICIPANT_REMOVED:
      return { text: 'Bạn không còn trong danh sách tham dự phòng họp này.', canRejoin: false }
    case DisconnectReason.ROOM_DELETED:
    case DisconnectReason.ROOM_CLOSED:
      return { text: 'Cuộc họp đã kết thúc, phòng họp đã đóng.', canRejoin: false }
    default:
      return { text: 'Mất kết nối tới phòng họp.', canRejoin: true }
  }
}

/** What went wrong with a microphone or camera, and whether the user can still listen. */
export function deviceErrorMessage(error: unknown, device: 'micro' | 'camera'): string {
  switch (MediaDeviceFailure.getFailure(error)) {
    case MediaDeviceFailure.PermissionDenied:
      return `Trình duyệt đang chặn ${device}. Bấm biểu tượng ổ khoá cạnh thanh địa chỉ, chọn Cho phép rồi tải lại trang. Bạn vẫn có thể vào phòng để nghe.`
    case MediaDeviceFailure.NotFound:
      return `Không tìm thấy ${device} trên máy này.`
    case MediaDeviceFailure.DeviceInUse:
      return `${device === 'micro' ? 'Micro' : 'Camera'} đang được ứng dụng khác dùng (Zoom, Teams…). Đóng ứng dụng đó rồi thử lại.`
    default:
      return `Không mở được ${device}. Bạn vẫn có thể vào phòng để nghe.`
  }
}
