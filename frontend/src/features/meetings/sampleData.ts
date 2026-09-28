import type { MeetingListItem } from '../../types/meeting'

/**
 * TEMPORARY: hard-coded rows standing in for `GET /api/meetings`.
 *
 * They exist so the list, filters and detail screens can be reviewed before the backend has a
 * database. Delete this file once the real endpoint lands — nothing here should outlive it.
 */
export const SAMPLE_MEETINGS: MeetingListItem[] = [
  {
    id: 'a1f2c3d4-0001-4a1b-9c2d-1e2f3a4b5c60',
    title: 'Giao ban tuần 38',
    status: 'Published',
    sensitivity: 'Internal',
    startsAt: '2026-09-21T02:00:00Z',
    createdAt: '2026-09-16T08:12:00Z',
  },
  {
    id: 'a1f2c3d4-0002-4a1b-9c2d-1e2f3a4b5c61',
    title: 'Phiên họp Hội đồng quản trị quý III',
    status: 'WaitingApproval',
    sensitivity: 'Secret',
    startsAt: '2026-09-24T07:00:00Z',
    createdAt: '2026-09-17T03:40:00Z',
  },
  {
    id: 'a1f2c3d4-0003-4a1b-9c2d-1e2f3a4b5c62',
    title: 'Rà soát kế hoạch chuyển đổi số',
    status: 'InProgress',
    sensitivity: 'Confidential',
    startsAt: '2026-09-18T01:30:00Z',
    createdAt: '2026-09-15T09:05:00Z',
  },
  {
    id: 'a1f2c3d4-0004-4a1b-9c2d-1e2f3a4b5c63',
    title: 'Lấy ý kiến dự thảo quy chế chi tiêu nội bộ',
    status: 'Draft',
    sensitivity: 'Normal',
    startsAt: '2026-09-29T02:00:00Z',
    createdAt: '2026-09-17T11:20:00Z',
  },
  {
    id: 'a1f2c3d4-0005-4a1b-9c2d-1e2f3a4b5c64',
    title: 'Tổng kết công tác quý II',
    status: 'MinutesIssued',
    sensitivity: 'Internal',
    startsAt: '2026-09-08T07:00:00Z',
    createdAt: '2026-09-01T02:00:00Z',
  },
]
