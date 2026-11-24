import { DisconnectReason, MediaDeviceFailure, type Participant, type RoomOptions, VideoPresets } from 'livekit-client'
import type { CallParticipantMetadata, MeetingRole } from '../../../types/call'
import type { CallEnd } from '../../recordings/hybridMic'
import type { GuestMeeting } from '../../../types/guest'
import type { Meeting } from '../../../types/meeting'

/**
 * ADR-0072: in a hybrid meeting the Secretary sits in the room, so their call mic is the room mic. One switch records
 * the room as the room take and sends the same sound to the people online; their own per-mic capture stays off.
 */
export function sendsRoomMic(meeting: Meeting | GuestMeeting, role: MeetingRole) {
  return 'mode' in meeting && meeting.mode === 'Hybrid' && role === 'Secretary'
}

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

/**
 * What the room says on its own, with no server response to show. Each line has its code in the message
 * catalogue (docs/03-api/00-danh-muc-ma-thong-bao.md), so the tester and BA can find it there. The lobby's
 * three "room not open" lines are the same situations the join endpoint refuses with, so they keep its codes.
 */
export const roomMessage = {
  notStarted: 'Phòng họp chỉ mở khi cuộc họp đang diễn ra.', // MSG117
  inPersonOnly: 'Cuộc họp này họp trực tiếp nên không có phòng họp trực tuyến.', // MSG280
  notConfigured: 'Phòng họp trực tuyến chưa sẵn sàng. Vui lòng liên hệ quản trị viên.', // MSG281
  left: 'Bạn đã rời phòng họp.', // MSG328
  otherTab: 'Bạn đã vào phòng này từ tab hoặc thiết bị khác. Tab này đã rời phòng.', // MSG329
  removed: 'Bạn không còn trong danh sách tham dự phòng họp này.', // MSG330
  closed: 'Cuộc họp đã kết thúc, phòng họp đã đóng.', // MSG331
  cancelled: 'Cuộc họp đã huỷ nên phòng họp không mở.', // MSG350
  lost: 'Mất kết nối tới phòng họp.', // MSG332
  unreachable: 'Không kết nối được tới phòng họp. Vui lòng thử lại.', // MSG480
  reconnecting: 'Mất kết nối, đang kết nối lại…', // MSG333
  deviceBlocked: (device: Device) =>
    `Trình duyệt đang chặn ${device}. Bấm biểu tượng ổ khoá cạnh thanh địa chỉ, chọn Cho phép rồi tải lại trang.${stillJoin(device)}`, // MSG334
  deviceMissing: (device: Device) => `Không tìm thấy ${device} trên máy này.`, // MSG335
  deviceInUse: (device: Device) =>
    `${device.charAt(0).toUpperCase()}${device.slice(1)} đang được ứng dụng khác dùng (Zoom, Teams…). Đóng ứng dụng đó rồi thử lại.`, // MSG336
  deviceFailed: (device: Device) =>
    `Không mở được ${device}. Kiểm tra quyền ở biểu tượng ổ khoá cạnh thanh địa chỉ.${stillJoin(device)}`, // MSG337
  audioBlocked: 'Trình duyệt đang chặn phát âm thanh.', // MSG338
  mutedByChair: 'Chủ toạ đã tắt micro của bạn. Bạn có thể bật lại khi cần phát biểu.', // MSG339
  shareTakenOver: (name: string) => `${name} bắt đầu trình bày, phần chia sẻ của bạn đã dừng.`, // MSG340
  shareFailed: 'Không chia sẻ được màn hình. Vui lòng thử lại.', // MSG341
  muted: (name: string) => `Đã tắt micro của ${name}.`, // MSG342
  chatFailed: 'Không gửi được tin nhắn. Vui lòng thử lại.', // MSG343
}

/** One device, or both when they failed the same way: the lobby then says it once. */
type Device = 'micro' | 'camera' | 'micro và camera'

/** Without a microphone a person can still listen; a camera alone does not stop that, so it is not said. */
function stillJoin(device: Device) {
  return device === 'camera' ? '' : ' Bạn vẫn có thể vào phòng để nghe.'
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
/** Why the call ended for this tab, as the room mic needs it (ADR-0085). */
export function callEnd(reason: DisconnectReason | undefined): CallEnd {
  switch (reason) {
    case DisconnectReason.CLIENT_INITIATED:
      return 'left'
    case DisconnectReason.DUPLICATE_IDENTITY:
      return 'movedAway'
    case DisconnectReason.PARTICIPANT_REMOVED:
      return 'removed'
    case DisconnectReason.ROOM_DELETED:
    case DisconnectReason.ROOM_CLOSED:
      return 'closed'
    default:
      return 'lost'
  }
}

export function disconnectMessage(reason: DisconnectReason | undefined): { text: string; canRejoin: boolean } {
  switch (reason) {
    case DisconnectReason.CLIENT_INITIATED:
      return { text: roomMessage.left, canRejoin: true }
    case DisconnectReason.DUPLICATE_IDENTITY:
      return { text: roomMessage.otherTab, canRejoin: true }
    case DisconnectReason.PARTICIPANT_REMOVED:
      return { text: roomMessage.removed, canRejoin: false }
    case DisconnectReason.ROOM_DELETED:
    case DisconnectReason.ROOM_CLOSED:
      return { text: roomMessage.closed, canRejoin: false }
    default:
      return { text: roomMessage.lost, canRejoin: true }
  }
}

/** How opening a microphone or camera failed; anything unrecognised is `Other`. */
export function deviceFailure(error: unknown): MediaDeviceFailure {
  return MediaDeviceFailure.getFailure(error) ?? MediaDeviceFailure.Other
}

/** What went wrong with a microphone or camera, and whether the user can still listen. */
export function deviceFailureMessage(failure: MediaDeviceFailure, device: Device): string {
  switch (failure) {
    case MediaDeviceFailure.PermissionDenied:
      return roomMessage.deviceBlocked(device)
    case MediaDeviceFailure.NotFound:
      return roomMessage.deviceMissing(device)
    case MediaDeviceFailure.DeviceInUse:
      return roomMessage.deviceInUse(device)
    default:
      return roomMessage.deviceFailed(device)
  }
}

export function deviceErrorMessage(error: unknown, device: Device): string {
  return deviceFailureMessage(deviceFailure(error), device)
}

/** The lobby's one notice for both devices: a single sentence when they failed alike, else one for each. */
export function devicesNotice(mic: MediaDeviceFailure | null, camera: MediaDeviceFailure | null): string | null {
  if (mic && camera && mic === camera) return deviceFailureMessage(mic, 'micro và camera')
  const lines = [mic && deviceFailureMessage(mic, 'micro'), camera && deviceFailureMessage(camera, 'camera')]
  return lines.filter(Boolean).join(' ') || null
}

export type DeviceOption = { id: string; label: string; isDefault: boolean }

/**
 * A picker leaves out the suffixes that say nothing about which device it is — "(Built-in)",
 * "(Virtual)", "(Aggregate)", a USB id like "(1C1C:B782)" — keeping any that tell two apart, such as
 * "(Jabra Evolve 65)". The menu shows the whole name.
 */
export function shortDeviceName(label: string) {
  return label.replace(/\s*\((?:built-in|virtual|aggregate|[0-9a-f]{4}:[0-9a-f]{4})\)$/i, '') || label
}

/**
 * Chrome lists the system default twice: once as "default" ("Mặc định - Micrô MacBook Air") and
 * once as the device itself. Like Meet, show the device once and mark it as the default. The
 * "communications" entry (Windows) is the same kind of alias.
 */
export function deviceOptions(devices: MediaDeviceInfo[], title: string): DeviceOption[] {
  const alias = devices.find((d) => d.deviceId === 'default')
  const real = devices.filter((d) => d.deviceId && d.deviceId !== 'default' && d.deviceId !== 'communications')
  if (real.length === 0) {
    return alias ? [{ id: alias.deviceId, label: alias.label || title, isDefault: true }] : []
  }
  const aliasName = alias?.label.includes(' - ') ? alias.label.slice(alias.label.indexOf(' - ') + 3) : alias?.label
  const defaultDevice =
    alias && (real.find((d) => d.groupId === alias.groupId && d.label === aliasName) ?? real.find((d) => d.label === aliasName))
  const options = real.map((d, i) => ({ id: d.deviceId, label: d.label || `${title} ${i + 1}`, isDefault: d === defaultDevice }))
  // A default we cannot match to a device stays its own entry, so following the system still works.
  return alias && !defaultDevice ? [{ id: alias.deviceId, label: alias.label || title, isDefault: true }, ...options] : options
}

/** Why a picker has nothing to offer, in the words both the tray and the pre-join screen use. */
export function noDeviceReason(kind: MediaDeviceKind, title: string) {
  return kind === 'audiooutput'
    ? 'Trình duyệt này không cho chọn loa'
    : `Chưa có quyền dùng ${title.toLowerCase()}, hoặc máy không có ${title.toLowerCase()}`
}
