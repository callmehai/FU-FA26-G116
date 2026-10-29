# Voice ID và tách người nói — SCRUM-31, SCRUM-35, SCRUM-102, SCRUM-103

> **Đổi 29/10/2026 (ADR-0042, PR #193):** giờ lưu cả bản ghi giọng mẫu mã hoá, chỉ chính chủ nghe; đồng ý lên 1.1. Xem [note 16 §2.5](16-ghi-am-voice-id-28-29-10-2026.md#25-voice-id-scrum-35-adr-0042).

**Ngày ghi:** 24/10/2026
**Nguồn:** tin nhắn agent của leader gửi Khánh
**Khi nào sửa:** khi ticket sang **BA Final Review**

Đổi phạm vi theo nhận xét hội đồng review 17/10/2026: **làm Voice ID + tách người nói** (trước ghi "không làm voiceprint"). Chốt ngày 23/10/2026.

README và ADR mới Hải sửa trong PR. Phần Khánh: đặc tả UC-43–51 (SCRUM-102, SCRUM-103) và ERD mục 7. Chi tiết nghiệp vụ: mô tả SCRUM-31, SCRUM-35.

## Đã chốt

- Tự chạy model trên server nhóm, giọng nói không gửi ra dịch vụ ngoài.
- Người dùng tự nguyện đăng ký giọng mẫu, đồng ý riêng (không gộp với đồng ý ghi âm); chỉ lưu vector đã mã hoá; rút đồng ý là xoá ngay.
- Hệ thống chỉ **gợi ý** tên người nói, thư ký xác nhận; không tự gán.
- Họp online: mỗi trình duyệt tự ghi micro của chính người dùng nên biết chắc ai nói.
- Căn cứ pháp lý: Luật BVDLCN 91/2025/QH15 (hiệu lực 01/01/2026) + NĐ 356/2025/NĐ-CP. Chỗ nào đang dẫn NĐ 13/2023 thì đổi theo.

## Cần sửa

- Đặc tả UC-43–51 (SCRUM-102, SCRUM-103).
- ERD mục 7: thêm bảng đồng ý Voice ID và giọng mẫu.
