import { MEETING_STATUS_LABEL, type MeetingHistoryItem } from '../../../types/meeting'

/**
 * What a history row records, as the event it was ("Gửi duyệt") rather than the state it left the
 * meeting in ("Chờ duyệt") — the state is the badge's colour.
 */
export function historyActionLabel(item: MeetingHistoryItem): string {
  switch (item.action) {
    case 'Create':
      return 'Tạo cuộc họp'
    case 'SubmitForApproval':
      return item.fromStatus === 'Rejected' ? 'Gửi duyệt lại' : 'Gửi duyệt'
    case 'Approve':
      return 'Duyệt'
    case 'Reject':
      return 'Từ chối'
    case 'ReturnToDraft':
      // Withdrawing a pending review and reopening a rejected meeting are different decisions. Rows of
      // the second kind predate direct resubmission; the backend still accepts the action.
      return item.fromStatus === 'WaitingApproval' ? 'Rút về nháp' : 'Đưa về nháp'
    case 'Publish':
      return 'Công bố'
    case 'TransferOrganizer':
      return 'Chuyển người tổ chức'
    case 'Reschedule':
      return 'Dời lịch'
    default:
      // Rows written before SCRUM-19 only know where the meeting ended up.
      return MEETING_STATUS_LABEL[item.resultingStatus] ?? item.resultingStatus
  }
}

/** The newest row of one kind, e.g. the submit that opened the current review. */
export function latestOf(items: MeetingHistoryItem[], action: MeetingHistoryItem['action']) {
  for (let i = items.length - 1; i >= 0; i--) if (items[i].action === action) return items[i]
  return undefined
}
