import type { Meeting, MeetingStatus } from '../../types/meeting'

/**
 * TEMPORARY: hard-coded rows standing in for `GET /api/meetings`.
 *
 * They exist so the list, filters and detail screens can be reviewed before the backend has a
 * database. Delete this file once the real endpoint lands — nothing here should outlive it.
 */
export const SAMPLE_MEETINGS: Meeting[] = [
  {
    id: 'a1f2c3d4-0001-4a1b-9c2d-1e2f3a4b5c60',
    title: 'Giao ban tuần 38',
    status: 'Published',
    sensitivity: 'Internal',
    mode: 'Hybrid',
    location: 'Phòng họp A — Tầng 4',
    startsAt: '2026-09-21T02:00:00Z',
    endsAt: '2026-09-21T03:30:00Z',
    createdAt: '2026-09-16T08:12:00Z',
  },
  {
    id: 'a1f2c3d4-0002-4a1b-9c2d-1e2f3a4b5c61',
    title: 'Phiên họp Hội đồng quản trị quý III',
    status: 'WaitingApproval',
    sensitivity: 'Secret',
    mode: 'InPerson',
    location: 'Phòng họp Hội đồng',
    startsAt: '2026-09-24T07:00:00Z',
    endsAt: '2026-09-24T10:00:00Z',
    createdAt: '2026-09-17T03:40:00Z',
  },
  {
    id: 'a1f2c3d4-0003-4a1b-9c2d-1e2f3a4b5c62',
    title: 'Rà soát kế hoạch chuyển đổi số',
    status: 'InProgress',
    sensitivity: 'Confidential',
    mode: 'Online',
    location: 'Trực tuyến',
    startsAt: '2026-09-18T01:30:00Z',
    endsAt: '2026-09-18T03:00:00Z',
    createdAt: '2026-09-15T09:05:00Z',
  },
  {
    id: 'a1f2c3d4-0004-4a1b-9c2d-1e2f3a4b5c63',
    title: 'Lấy ý kiến dự thảo quy chế chi tiêu nội bộ',
    status: 'Draft',
    sensitivity: 'Normal',
    mode: 'Online',
    location: 'Trực tuyến',
    startsAt: '2026-09-29T02:00:00Z',
    endsAt: '2026-09-29T03:00:00Z',
    createdAt: '2026-09-17T11:20:00Z',
  },
  {
    id: 'a1f2c3d4-0005-4a1b-9c2d-1e2f3a4b5c64',
    title: 'Tổng kết công tác quý II',
    status: 'MinutesIssued',
    sensitivity: 'Internal',
    mode: 'InPerson',
    location: 'Hội trường lớn',
    startsAt: '2026-09-08T07:00:00Z',
    endsAt: '2026-09-08T09:30:00Z',
    createdAt: '2026-09-01T02:00:00Z',
  },
]

export interface HistoryEntry {
  status: MeetingStatus
  /** What happened, as opposed to what the meeting now is. */
  label: string
  actor: string
  at: string
}

/** TEMPORARY: status history for the detail screen, same lifetime as SAMPLE_MEETINGS. */
export const SAMPLE_HISTORY: HistoryEntry[] = [
  { status: 'Draft', label: 'Đã tạo nháp', actor: 'Nguyễn Thư Ký', at: '2026-09-16T08:12:00Z' },
  { status: 'WaitingApproval', label: 'Đã gửi duyệt', actor: 'Nguyễn Thư Ký', at: '2026-09-16T08:40:00Z' },
  { status: 'Approved', label: 'Đã duyệt cuộc họp', actor: 'Trần Chủ Toạ', at: '2026-09-16T09:15:00Z' },
  { status: 'Published', label: 'Đã công bố', actor: 'Nguyễn Thư Ký', at: '2026-09-16T09:20:00Z' },
]
