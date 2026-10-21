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
