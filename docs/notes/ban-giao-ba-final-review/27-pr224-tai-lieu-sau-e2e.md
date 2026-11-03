# PR #224 — Tài liệu cuộc họp sau test E2E 02/11/2026: thông báo cho BA

**Người ghi:** LocHD (BE), 02/11/2026. **Nguồn:** [PR #224](https://github.com/callmehai/FU-FA26-G116/pull/224), kết quả test E2E
production 02/11/2026 (163 kiểm tra, 5 lỗi) trên SCRUM-21, 22, 23, 58, 107.

> **Tiến độ 03/11/2026: tài liệu đã khớp code, BA không cần sửa gì.** Note này chỉ để BA biết đã sửa gì (§1, §2) và những chỗ
> code khác mô tả ticket (§3, docs đã ghi theo code). Riêng ảnh wireframe là việc cập nhật sau, không thuộc note này.
> PR #224 đã merge vào `main` (02/11/2026, phần code). Phần sửa tài liệu này nằm ở PR #235, sửa Report 3 DOCX trên bản đã có
> #221 (#221 đã merge vào `main`).

## 1. Code đã đổi

- `GET /api/meetings/{id}/documents` trả thêm `votes`: biểu quyết có tài liệu mà người gọi xem được (nháp chỉ Chủ toạ/Thư ký,
  BR-253), mỗi tài liệu kèm `readConfirmedAt` của chính người gọi; mỗi biểu quyết có `canConfirmRead` (người bỏ phiếu, biểu quyết
  Open, cuộc họp InProgress). FE: nhóm "Biểu quyết" ở tab Tài liệu và panel phòng họp; "Chưa đọc" chỉ hiện khi `canConfirmRead`.
- Khách đã xác minh góp ý và trả lời (UC-35, BR-181); bị thu hồi lời mời hay cuộc họp kết thúc thì `MSG156`.
- Server kiểm cấu trúc PDF và tự đếm trang bằng PdfPig trong tiến trình con
  ([ADR-0044](../../05-decisions/0044-server-kiem-cau-truc-va-dem-trang-pdf.md)); FE không đếm trang, không gửi `pageCounts`.
- `MSG382` (410, tệp đã mất khỏi kho) và `MSG383` (FE, trình duyệt không vẽ được tệp), đều không có nút Thử lại.
- Sửa sau review PR #224 (PR #235, 03/11/2026): chờ quá 30 giây mà hai tiến trình kiểm PDF vẫn bận thì lần tải trả mã "thử
  lại" sẵn có (`503 MSG166`, kho cá nhân `503 MSG178`), không phải `MSG164`; tiến trình con chết hay quá giờ có ghi log; tab
  Tài liệu không còn đọc lại danh sách sau mỗi lá phiếu (ADR-0044 §6, API 11).

## 2. Tài liệu đã sửa trong PR

- Report 3: I.3.2 `StoredFile.PageCount` (server đếm), UC-36 EX-1 và BR-215 (PDF cần mở được không mật khẩu, có ít nhất một
  trang), UC-32a AF-1/AF-3, UC-32c (POST-3, bước 4, EX-5, EX-6), UC-33a AF-1/EX-3, UC-34b EX-4, UC-35 EX-5; màn 7.3, 7.4, 7.5,
  8.6 theo code; §V.2 thêm MSG382, MSG383; Change Log. Extract `05-use-case-specifications.md` sinh lại.
- Sequence UC-32a, UC-32c, UC-33a AF-1, UC-36, UC-76a: `.txt`, PNG, tab draw.io gộp và `drawio-png/`.
- Sequence UC-33b (nhãn `attachDocuments`, `drawio-png/` xuất lại ở scale 1); class diagram FE-07.
- `docs/03-api/11`, `14`, `15`, `16`, danh mục mã (MSG380/382/383 ở bảng nguồn UC; MSG166/MSG178 thêm trường hợp kiểm PDF
  quá lượt), ADR-0044 (§6 tiến trình con, chờ tối đa 30 giây, log), note 23 (dòng "không góp ý").

## 3. Code khác mô tả ticket (docs đã ghi theo code)

Docs đã viết đúng như code đang chạy; không có việc gì cho BA. Nếu PO muốn đổi điểm nào thì đó là việc mới cho
BE/FE, kèm sửa docs.

| # | Điểm | Docs hiện ghi |
|---|---|---|
| 1 | SCRUM-21 ghi "không cấp mã mới", PR cấp `MSG382`, `MSG383` vì hai tình huống trước đó báo "thử lại" là sai | Có `MSG382`, `MSG383` (Report 3 §V.2, UC-32c EX-5/EX-6, UC-34b EX-4; danh mục mã) |
| 2 | SCRUM-107: số trang do server đếm, không còn do FE đếm như mô tả ticket | Server đếm (ADR-0044; Report 3 UC-33a, `StoredFile.PageCount`) |
| 3 | Tài liệu của biểu quyết không gắn mục vẫn hiện ở "Tài liệu chung", kể cả với biểu quyết nháp (BR-280 tính theo mục nghị sự). AI-eCabinet thì **không** tính tài liệu chỉ gắn biểu quyết là chung ([note 22](22-pr201-gan-tai-lieu-tu-dong-nghi-su.md) điểm 7b) | Hiện ở "Tài liệu chung" theo BR-280 (Report 3 UC-32a AF-1, màn 7.3; API 11). Muốn theo AI-eCabinet thì là việc mới; khi đó Thành viên không thấy tài liệu của biểu quyết nháp ở đâu cả |
| 4 | Khách gặp cuộc họp đã kết thúc nhận `MSG156`, không phải `MSG251` như Thành viên (BR-181 bản của #221) | `MSG156` cho Khách (Report 3 UC-35 EX-5; API 11) |

Ảnh wireframe các màn 7.3–7.5 và 8.6 trong Report 3 vẫn là bản v8, sẽ cập nhật sau cùng đợt wireframe; chữ trong đặc tả màn
đã theo code.
