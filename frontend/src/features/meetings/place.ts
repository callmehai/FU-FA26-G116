import { MEETING_MODE_LABEL, type MeetingMode } from '../../types/meeting'

/** "Kết hợp · Phòng 3.2"; an online meeting happens at its link, so it shows only the mode. */
export function placeLabel({ mode, location }: { mode: MeetingMode; location: string }) {
  if (mode === 'Online') return MEETING_MODE_LABEL.Online
  return location ? `${MEETING_MODE_LABEL[mode]} · ${location}` : MEETING_MODE_LABEL[mode]
}
