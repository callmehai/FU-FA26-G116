# ERD mục tiêu

- [trustcabinet-erd-muc-tieu.drawio](trustcabinet-erd-muc-tieu.drawio) — source editable.
- [trustcabinet-erd-muc-tieu.drawio.png](trustcabinet-erd-muc-tieu.drawio.png) — ảnh xuất.

Đây là **nguồn chính** cho mô hình dữ liệu đích: dạng bảng, có thuộc tính, dùng trong Report 3 §I.3.2
và [02-erd.md](../../02-design/02-erd.md). Nội dung và thứ tự nguồn sự thật do `02-erd.md` quy định;
thay đổi source thì cập nhật ảnh để giữ hai bản đồng bộ. Góc nhìn Chen của cùng mô hình nằm ở
[../chen/](../chen/README.md).

Bố cục (22/10/2026): Meeting ở giữa, User bên dưới, các bảng xếp quanh như bản 11/10/2026; tài liệu
(`MeetingDocumentAgendaItem`, `MeetingDocument`, `Document`, `Folder`, `StoredFile`) ở bên phải. Cột kiểu
của enum chỉ ghi `enum`; danh sách giá trị xem [02-erd.md](../../02-design/02-erd.md) và Report 3 §3.2.

Từ 04/11/2026 (SCRUM-129, BA Final Review): 38 bảng — 35 thực thể của mô hình đích cộng ba bảng nối
`MeetingDocumentAgendaItem`, `VoteEligibleVoter`, `VoteDocument`. Ba bảng thêm cùng đợt — `SpeechClip`,
`VoiceConsent`, `VoiceSample` — nằm ở dải dưới cùng; `DocumentReadConfirmation` (bảng nối thứ tư) vẫn chưa vẽ.
