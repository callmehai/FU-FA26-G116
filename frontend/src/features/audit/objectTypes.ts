/** `AuditLogEntry.ObjectType` values the backend writes; an unknown one shows as recorded. */
const OBJECT_TYPE_LABEL: Record<string, string> = {
  User: 'Tài khoản',
  Role: 'Vai trò',
  Meeting: 'Cuộc họp',
  MeetingDocument: 'Tài liệu',
  MeetingMinutes: 'Biên bản',
  VotingSession: 'Biểu quyết',
  Recording: 'Bản ghi',
  SpeechClip: 'Câu nói',
  TranscriptSpeaker: 'Người nói',
  VoiceConsent: 'Đồng ý Voice ID',
  VoiceSample: 'Mẫu giọng',
  GuestSession: 'Phiên của Khách',
  // SCRUM-38: an export of the meeting report (report.exported); it has no object id.
  Report: 'Báo cáo',
  // SCRUM-142: the thirteen calendar_share.* events.
  CalendarShare: 'Chia sẻ lịch',
  // The clean-up jobs' summary rows (one per run that removed something); none has an object id.
  RecordingStorage: 'Kho âm thanh',
  StoredFile: 'Kho tài liệu',
  Notification: 'Thông báo',
  GuestVerificationChallenge: 'Mã xác minh Khách',
  EmailMessage: 'Email',
}

export const objectTypeLabel = (type: string) => OBJECT_TYPE_LABEL[type] ?? type
