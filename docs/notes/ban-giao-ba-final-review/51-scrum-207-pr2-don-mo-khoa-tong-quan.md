# PR thứ hai SCRUM-207 — dọn chỗ còn nhắc mở khoá tài khoản và màn Tổng quan

**Người ghi:** KhánhPG/BA, 15/11/2026. **Nguồn:** leader chốt qua TrustCabinet Bot tối 15/11/2026 — làm luôn trong SCRUM-207 nhưng **tách thành PR thứ hai** để PR #367 (note 50) sửa 2 chỗ là merge được, không phải chờ. Mấy câu hỏi PO sáng nay đã chốt trong note 45 và 47 nên không trả lời lại.

**Điều kiện làm:** sau khi PR #367 vào `main`; nhánh `docs/scrum-207-pr2-don-mo-khoa-tong-quan` tách xếp chồng trên nhánh #367 (tránh xung đột nhị phân DOCX Report 3), PR mở với base là nhánh #367 rồi chuyển base về `main` sau merge.

## 1. Report 1 và Report 2 (DOCX là nguồn, rồi sync lại)

- `docs/08-reports/source/Report_1_Project_Introduction.docx` và `Report_2_Project_Management_Plan.docx`: bỏ FE-14 và mọi chỗ nhắc mở khoá tài khoản khỏi kế hoạch/phạm vi.
- Chạy lại đồng bộ Report 1/2 theo `docs/08-reports/README.md` (cùng quy trình như Report 3: sửa DOCX, không sửa tay file Markdown sinh ra).

## 2. Sơ đồ nghiệp vụ BF-01

- `docs/09-bussinessflow/README.md` dòng BF-01 và `docs/02-design/04-luong-nghiep-vu-chinh.md` (§System Admin + câu “Admin mở khoá bằng UC-09”): bỏ nhánh khoá và mở khoá, chỉ giữ giới hạn đăng nhập theo phút (MSG414, SCRUM-132).

## 3. Xoá sequence UC-09 và chỗ nhắc trong README sơ đồ lớp

- Xoá `docs/06-sequencediagram/user-management/UC09_UnlockUserAccount.txt` + PNG cùng tên.
- Xoá tab gộp `uc09_unlockuseraccount` trong `docs/06-sequencediagram/TrustCabinet_Sequence_Diagrams.drawio` (kèm `index-uc09_unlockuseraccount`).
- Sửa chỗ nhắc: `docs/06-sequencediagram/README.md` (bảng user-management còn UC-09), `docs/06-sequencediagram/user-management/README.md` (dòng UC-05 → UC-09 + hàng UC-09), `docs/07-classdiagram/README.md` dòng UC-09/FE-02.
- UC-09/UC-69 không tái dùng số hiệu (tiền lệ Change Log Report 3 giữ dòng lịch sử).

## 4. Report 3 còn sót (vào PR thứ hai, không vào #367)

- Câu `Feature priority ... FE-14 to FE-16 are Should-have` → `FE-15 to FE-16 are Should-have` (dòng khoảng 820 bản Markdown sinh ra; sửa trong DOCX rồi sync).
- Hai tiêu đề FE-14 trống trong bản trích `05-use-case-specifications.md` (dòng 200 và 2008, không còn UC nào dưới đó): script sync không tự xoá mục FE mồ côi nên xoá trực tiếp 2 heading rỗng rồi chạy `--check` (`UP TO DATE`).
- Review bổ sung 15/11 (5 chỗ đúng là lỗi, đã sửa): NFR `Account lockout` → `Sign-in rate limiting` theo code (5 lần/email + 60 lần/IP mỗi 60 giây, 429 MSG414, không khoá tài khoản); mô tả actor System Admin bỏ `unlock`; NFR `Audit logging` bỏ `lockout and unlock`; NFR `Configurability` bỏ `lockout threshold and duration`; ma trận phân quyền xoá hàng `Dashboard` + footnote ¹² bỏ `own dashboard`. NFR mới nhắc MSG414 nên thêm dòng MSG414 vào bảng V.2 (dòng 478) cho `check_docx` qua.

## 5. Không đụng

- Wireframe cũ (`docs/wireframe/*v5–v12*.html` còn metadata UC-09): để nguyên — snapshot lịch sử, không nằm trong Report nộp hội đồng.
- ADR đã chấp nhận (ADR-0040/0041): không sửa.
