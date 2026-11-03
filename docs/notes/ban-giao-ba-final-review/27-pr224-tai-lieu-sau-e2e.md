# PR #224 — Tài liệu cuộc họp sau test E2E 02/11/2026: việc BA Final Review

**Người ghi:** LocHD (BE), 02/11/2026. **Nguồn:** [PR #224](https://github.com/callmehai/FU-FA26-G116/pull/224), kết quả test E2E
production 02/11/2026 (163 kiểm tra, 5 lỗi) trên SCRUM-21, 22, 23, 58, 107.

> **Tiến độ 02/11/2026: tài liệu đã sửa theo code trong chính PR #224.** BA chỉ còn rà lại và xác nhận 4 điểm ở §3.
> PR #224 đã merge vào `main` (02/11/2026, phần code). Phần sửa tài liệu này nằm ở PR tiếp theo, PR đó đã merge nhánh của PR #221 để sửa
> Report 3 DOCX trên bản của #221, nên **#221 phải merge trước hoặc cùng lúc**.

## 1. Code đã đổi

- `GET /api/meetings/{id}/documents` trả thêm `votes`: biểu quyết có tài liệu mà người gọi xem được (nháp chỉ Chủ toạ/Thư ký,
  BR-253), mỗi tài liệu kèm `readConfirmedAt` của chính người gọi; mỗi biểu quyết có `canConfirmRead` (người bỏ phiếu, biểu quyết
  Open, cuộc họp InProgress). FE: nhóm "Biểu quyết" ở tab Tài liệu và panel phòng họp; "Chưa đọc" chỉ hiện khi `canConfirmRead`.
- Khách đã xác minh góp ý và trả lời (UC-35, BR-181); bị thu hồi lời mời hay cuộc họp kết thúc thì `MSG156`.
- Server kiểm cấu trúc PDF và tự đếm trang bằng PdfPig trong tiến trình con
  ([ADR-0044](../../05-decisions/0044-server-kiem-cau-truc-va-dem-trang-pdf.md)); FE không đếm trang, không gửi `pageCounts`.
- `MSG382` (410, tệp đã mất khỏi kho) và `MSG383` (FE, trình duyệt không vẽ được tệp), đều không có nút Thử lại.

## 2. Tài liệu đã sửa trong PR

- Report 3: UC-32a AF-1/AF-3, UC-32c (POST-3, bước 4, EX-5, EX-6), UC-33a AF-1/EX-3, UC-34b EX-4, UC-35 EX-5; màn 7.3, 7.4, 7.5,
  8.6 theo code; §V.2 thêm MSG382, MSG383; Change Log. Extract `05-use-case-specifications.md` sinh lại.
- Sequence UC-32a, UC-32c, UC-33a AF-1, UC-36, UC-76a: `.txt`, PNG, tab draw.io gộp và `drawio-png/`.
- `docs/03-api/11`, `14`, `15`, `16`, danh mục mã (MSG380/382/383 ở bảng nguồn UC), ADR-0044, note 23 (dòng "không góp ý").

## 3. Còn để BA/PO xác nhận

| # | Điểm | Đề xuất |
|---|---|---|
| 1 | SCRUM-21 ghi "không cấp mã mới", PR cấp `MSG382`, `MSG383` vì hai tình huống trước đó báo "thử lại" là sai | Giữ mã mới |
| 2 | SCRUM-107: số trang do server đếm, không còn do FE đếm như mô tả ticket | Giữ (ADR-0044) |
| 3 | Tài liệu của biểu quyết không gắn mục vẫn hiện ở "Tài liệu chung", kể cả với biểu quyết nháp (BR-280 tính theo mục nghị sự). AI-eCabinet thì **không** tính tài liệu chỉ gắn biểu quyết là chung ([note 22](22-pr201-gan-tai-lieu-tu-dong-nghi-su.md) điểm 7b) | PO chọn. Code và Report 3 UC-32a AF-1 hiện theo BR-280; theo AI-eCabinet thì phải ẩn chúng khỏi nhóm chung, nhưng khi đó Thành viên không thấy tài liệu của biểu quyết nháp ở đâu cả |
| 4 | Khách gặp cuộc họp đã kết thúc nhận `MSG156`, không phải `MSG251` như Thành viên (BR-181 bản của #221) | Giữ; UC-35 EX-5 đã ghi |

Wireframe của các màn 7.3–7.5 và 8.6 chưa vẽ lại (ảnh trong Report 3 vẫn là bản v8); chữ trong đặc tả màn đã theo code.
