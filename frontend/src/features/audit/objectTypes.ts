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
  AiProviderSetting: 'Cấu hình AI',
}

export const objectTypeLabel = (type: string) => OBJECT_TYPE_LABEL[type] ?? type
