# ERD dạng Chen

- [trustcabinet-erd-chen.drawio](trustcabinet-erd-chen.drawio) — sơ đồ editable.
- [trustcabinet-erd-chen.drawio.png](trustcabinet-erd-chen.drawio.png) — ảnh xuất hiện có.

Đây là góc nhìn Chen của **cùng mô hình đích** với [ERD mục tiêu](../muc-tieu/README.md): 36 thực thể
hình chữ nhật, 71 quan hệ hình thoi, không ghi thuộc tính. Từ 22/11/2026 hình khớp ERD dạng bảng: thêm
`CalendarShare`, `AiProviderSetting` và mọi cặp bảng có khoá ngoại mà bản trước chưa vẽ (quan hệ của Khách với
`SpeakingRequest`, `AgendaItem`, `DocumentComment`, `VoteChoice`, `TranscriptSpeaker`, `GuestSession`; xác nhận đã
đọc tài liệu; `Meeting`–`VoiceSample`, `CallSession`–`Recording`, `AgendaItem`–`MeetingNote`/`SpeakingRequest`,
`MeetingStatusHistory`–`Notification`); `MeetingDocument`–`AgendaItem` vẽ lại thành nhiều–nhiều (ADR-0020). Bốn bảng
nối thành quan hệ nhiều–nhiều, hai bảng kỹ thuật `MeetingInvitationDispatch`, `GuestVerificationChallenge` không vẽ. Tài liệu mô tả mô hình đích và phạm vi dữ
liệu là [02-erd.md](../../02-design/02-erd.md); khi thay đổi source, tuân theo
[quy tắc draw.io](../../../DRAWIO_EDITING_RULES.md) và cập nhật ảnh xuất.
