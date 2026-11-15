# QA 15/11 — Đưa Report 3 và tài liệu về khớp code (SCRUM-207)

**Người ghi:** KhánhPG/BA, 15/11/2026. **Nguồn:** đợt QA toàn bộ FE-01..FE-16 ngày 15/11/2026 trên app thật (SCRUM-187, subtask 188–203). Code đúng ở mọi chỗ dưới đây, chỉ tài liệu chưa theo kịp. Mỗi mục ghi ticket QA có chi tiết. SCRUM-207 gom việc cho Khánh.

**Phạm vi:** sửa `Report_3_Software_Requirement_Specification.docx` trong `docs/08-reports/source/` rồi chạy `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`, không sửa tay các file Markdown sinh ra (`Report-3_*.md`, `05-use-case-specifications.md`, `06-business-rules.md`, `uc-map.html` tự sinh theo).

| Ticket QA | Nội dung gom vào SCRUM-207 |
|---|---|
| SCRUM-189 | UC-05/BR-11 phân trang, BR-17/UC-06b Chức danh, UC-08 POST-4, UC-09 bỏ Mở khoá |
| SCRUM-191 | UC-17 thẻ Họp hôm nay (PR #287) |
| SCRUM-192 | UC-24 EX-1, BR-65/BR-34/BR-74 + actor UC-23a–d, MSG66 tiếng Anh |
| SCRUM-190 | Tài liệu API quản lý vai trò mục 7 + số UC |
| SCRUM-193 | Tài liệu điểm danh còn totals |
| SCRUM-195 | UC-79 nút Giơ tay trên điện thoại |
| SCRUM-196 | UC-43a/UC-44e bản ghi đã xoá, BR-348 câu xác nhận xoá, UC-44c Đã tải lên |
| SCRUM-197 | UC-46c Bỏ khỏi biên bản, mô tả SCRUM-152 |
| SCRUM-198 | UC-53 chữ Không ý kiến |
| SCRUM-199 | UC-83 đính chính xong |
| SCRUM-201 | UC-69 bỏ Dashboard + Không gian họp |
| SCRUM-202 | UC-70a AF-2, MSG466 FE đã làm |
| SCRUM-203 | UC-17 Chủ toạ đọc mọi trạng thái (ADR-0021), BR-428/UC-85a câu lịch đã chia sẻ |
| SCRUM-152 | Mô tả ticket còn proxy-consent (comment 10529) |

**Đã kiểm trùng:** chưa có note nào cho SCRUM-207. Các note 44–49 đã vào `main` qua PR #350/#351/#352 và PR #363/#364 (note 47, Change Log gọn, ảnh nén). Nhánh làm: `docs/ba-final-review-note50-scrum-207-qa-15-11` từ tip `experiment/optimize-doc-images-palette @ 7fa4576e`.

## 1. PO bỏ hai UC (15/11)

- **UC-09 Mở khoá tài khoản: bỏ hẳn.** Không khoá tài khoản khi nhập sai mật khẩu, không có thao tác Mở khoá. Giới hạn đăng nhập theo phút (MSG414, SCRUM-132) vẫn giữ. Xoá UC-09 khỏi Report 3 (bảng danh mục, đặc tả §1.4, actor, ma trận phân quyền, màn hình, BR liên quan, sơ đồ use case), danh mục use case và sơ đồ use case.
- **UC-69 Dashboard cá nhân và Không gian họp: bỏ hẳn Report 3 §14.1 và §14.2.** Chỉ giữ bốn thẻ đếm trên danh sách cuộc họp (Họp hôm nay, Cần xử lý, Bản nháp, Đã công bố). Sửa prose Tổng quan sau đăng nhập + Meeting Workspace (§14 còn lại viết lại theo bốn thẻ), màn hình và Change Log.

## 2. Report 3: UC và BR lệch code

- [x] **UC-05, BR-11:** danh sách người dùng cố ý không phân trang (tài liệu API đã ghi). Sửa đặc tả UC-05 + BR-11 bỏ yêu cầu phân trang khi vượt page size, giữ câu không có kết quả (BR-12).
- [x] **BR-17, UC-06b:** hệ thống không có Chức danh và Thông tin liên hệ. Sửa UC-06b bước 3 + BR-17 chỉ còn Họ tên (Full Name) theo code.
- [x] **UC-08 POST-4:** đổi vai trò giữ phiên và báo MSG287 (SCRUM-105). Sửa POST-4 bỏ chữ có thể kết thúc phiên.
- [x] **UC-17 bước 1:** mở sẵn thẻ Họp hôm nay, không phải bộ lọc Đang diễn ra (PR #287, xem note 40). Sửa bước 1 + màn danh sách.
- [x] **UC-17 actor:** Chủ toạ đọc được cuộc họp ở mọi trạng thái (ADR-0021), không chỉ từ khi Đã công bố. Sửa Description + BR-119 liên quan.
- [x] **UC-24 EX-1:** API trả 409 MSG73, hoặc 403 khi người gọi không phải Chủ toạ. Sửa EX-1 bỏ chữ 404.
- [x] **BR-65, BR-34, BR-74 và actor UC-23a–d:** cho cả Chủ toạ soạn chương trình (PR #256, đã ghi ở SCRUM-25). Sửa 3 BR + actor 4 UC từ chỉ Thư ký thành Chủ toạ + Thư ký.
- [x] **UC-53:** một chữ cho phương án thứ ba — app ghi Không ý kiến, UC ghi Abstain, ví dụ API ghi Bỏ phiếu trắng. Chốt Không ý kiến ở UC-53 + tài liệu API biểu quyết + MSG liên quan.
- [x] **UC-43a bước 4, UC-44e POST-2:** tab Bản ghi không hiện bản ghi đã xoá (PR #193, đã ghi ở SCRUM-29). Sửa hai chỗ bỏ chữ hiện bản ghi đã xoá.
- [x] **BR-348:** người xoá bản ghi chỉ nằm trong nhật ký kiểm toán. Chốt một cách nói với câu hộp xác nhận xoá nói giữ ai đã xoá.
- [x] **UC-44c bước 6:** tải tệp lên xong khay tải lên hiện Đã tải lên, không có toast MSG290. Sửa bước 6 bỏ toast.
- [x] **UC-46c:** Bỏ khỏi biên bản không hỏi lý do. Máy tự chọn Lọt tiếng khi câu đã bị đánh dấu lẫn tiếng micro khác, còn lại là Khác; bỏ nhầm thì Khôi phục. Sửa đặc tả bỏ bước hỏi lý do.
- [x] **UC-70a AF-2:** khoảng trống chỉ hiện 0 cuộc họp và câu MSG466, không hiện các bảng toàn số 0. Sửa AF-2.
- [x] **UC-79 bước 1:** trên điện thoại nút Giơ tay nằm trong Thêm (⋯) → Phát biểu, không ở hàng điều khiển. Sửa bước 1.
- [x] **UC-83 bước 4:** đính chính xong màn hình hiện bản đính chính kèm nút Sửa, không tự mở trình soạn. Sửa bước 4.
- [x] **BR-428, UC-85a:** câu Chỉ tính lịch đã được chia sẻ… đã bỏ ngày 13/11 (SCRUM-137). Xoá câu này ở trang Tìm giờ họp.

Không cần sửa: ADR-0040 còn nhắc nút Tạm dừng — ADR đã chấp nhận thì không sửa, ADR-0041 đã thay nó.

## 3. docs/03-api và danh mục mã thông báo

- [x] **MSG466:** đang ghi FE (chưa làm), nhưng FE đã làm (trang Báo cáo). Sửa catalogue + Report 3 V.2 thành FE đã làm.
- [x] **MSG66:** dòng tiếng Anh còn nhắc Planned duration dù thời lượng đã bỏ. Sửa catalogue bỏ cụm này.
- [x] **Tài liệu API quản lý vai trò (`04-quan-ly-vai-tro.md`):** mục 7 ghi sửa quyền thì kết thúc phiên, trái với BR-313; số UC lệch (UC-11 là tạo, UC-12a/b là sửa và xoá). Sửa mục 7 theo BR-313 + đánh lại số UC.
- [x] **Tài liệu điểm danh (`10-diem-danh.md`):** còn bộ đếm totals trong ví dụ GET, trong khi Report 3 đã bỏ. Bỏ totals ở ví dụ, giữ câu FE không hiển thị tổng (đã ghi ở SCRUM-26).

## 4. Mô tả ticket

- [x] **SCRUM-152:** mô tả còn ghi đồng ý thu hộ riêng và endpoint proxy-consent. Comment 10529 đã bỏ phần này, code làm theo comment. Sửa mô tả Jira cho khớp (việc Jira, không sửa Report 3).

## 5. Quy trình sửa (DOCX là nguồn)

1. Sửa `Report_3_Software_Requirement_Specification.docx` → `python scripts/check_docx.py docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --fix-fonts` (phải `OK`, font Times New Roman).
2. `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3` (kéo theo `05-use-case-specifications.md`, `06-business-rules.md`, `uc-map.html`).
3. Rà `git diff -- docs/08-reports docs/01-requirements docs/uc-map.html` chỉ nằm trong vùng SCRUM-207.
4. Thêm một dòng Change Log Report 3 theo quy ước `08-reports/README.md` (tiếng Anh, dưới ~250 ký tự): `SCRUM-207 (note 50): align Report 3 and API docs with QA-built behavior 15/11 (drop UC-09/UC-69, fix UC/BR/MSG mismatches).`
5. Sửa `docs/03-api/*.md` + catalogue trong cùng đợt, cập nhật Mã tiếp theo nếu thêm mã (đợt này không thêm mã mới).
6. Không đẩy Drive khi chưa được yêu cầu. Không sửa ADR đã chấp nhận.

Làm sau khi PR #363 vào `main` và PR #364 rebase xong thì rebase nhánh này một lần để DOCX khỏi lệch nhị phân.

## 6. Sơ đồ use case (đã làm) + phạm vi ngoài ticket (không đụng)

**Sơ đồ use case — xong, vì ticket SCRUM-207 ghi rõ “sơ đồ use case”** (note 50 đã ghi phạm vi từ đầu): xoá 7 oval + 7 cạnh (`uc-55` Unlock = UC-09, `uc-84` Dashboard = UC-69) trong `trustcabinet-use-cases.drawio`, không rớt cạnh nào; xuất lại 6 PNG bằng Draw.io CLI (palette 256 qua `compress_png.py`), thay 6 ảnh nhúng tương ứng trong Report 3 §4.3 (Guest giữ nguyên); `check_docx OK`, resync khớp 100%. Đã soi ảnh System Admin: sạch cạnh thừa, giữ nguyên layout.
**Đặc tả màn hình giữ nguyên, không đụng (pending)** như đã chốt.
**Ngoài ticket, không đụng:** Report 1/2 còn nhắc FE-14 (kế hoạch/phạm vi), BF-01 + flow khóa/mở tài khoản (`09-bussinessflow`, `02-design`), sequence UC-09 (`.txt`, PNG, tab drawio gộp), class diagram README, wireframe các bản (snapshot lịch sử + metadata UC-09). Ticket SCRUM-207 chỉ nêu Report 3, danh mục use case, sơ đồ use case, BR liên quan, API docs/catalogue và mô tả SCRUM-152 — các mục trên cần ticket follow-up riêng nếu PO muốn dọn.

**Đặc tả màn hình giữ nguyên, không sửa (quyết định 15/11/2026 lúc rà diff):** chi tiết màn 4.3 theo note 40 (thẻ Hôm nay mặc định, 4 thẻ Summary cards, preset dropdown, lời nhắn bảng trống, wireframe 4.3 vẽ lại, Q1/Q2 của note 40) vẫn pending — PO cho làm sau. Trong đợt này chỉ sửa đúng câu chữ hệ quả trực tiếp, không thuộc đặc tả màn: footnote phạm vi Chair đọc mọi trạng thái (ADR-0021) và dòng actor còn nhắc personal dashboard.

- `docs/14-uc-diagram/trustcabinet-use-cases.drawio` còn oval cũ (id theo baseline 05/10/2026): `uc-55` Unlock User Account (= UC-09 hiện tại) trên trang System Admin kèm cạnh `rel-55-4-extend`; `uc-84` View Personal Dashboard & Meeting Workspace (= UC-69 hiện tại) trên 6 trang nội bộ (System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant) kèm cạnh `assoc-84`. Trang Guest không có oval nào trong hai oval này.
- Việc cần làm: xoá 7 oval + 7 cạnh, xuất lại 6 PNG (`uc-system-admin`, `uc-meeting-organizer`, `uc-meeting-approver`, `uc-chair`, `uc-secretary`, `uc-participant`), thay ảnh nhúng tương ứng trong Report 3 §4.3, kiểm tra hiển thị. Giữ nguyên layout các trang, không vẽ lại toàn bộ (theo DRAWIO_EDITING_RULES.md).
- Ngoài ra V.2 MSG466 trong Report 3 đã sửa `Dành sẵn, chưa triển khai` → `Table message` cho khớp catalogue FE đã làm (MSG463–465 vẫn giữ nguyên vì ngoài phạm vi đợt này).
- Extracts: chạy `sync_requirement_extracts.py --check` (báo `not in report: UC-09/UC-69` đúng như tiền lệ UC-62/63) rồi `--prune-missing` để xoá khối UC-09/UC-69 khỏi `05-use-case-specifications.md`; `--check` lại ra `UP TO DATE`.
- Dọn nốt trong DOCX: 3 dòng FE-14 (bảng Should-have + 2 dòng ánh xạ 14.1/14.2), hàng `User account | Unlock` trong ma trận phân quyền, câu màn hình `Unlocking a locked account (UC-09)…`; resync + `check_docx OK`, inventory + catalogue vẫn khớp 100%.
- Inventory §7 Thống kê: FE-02 5→4 UC, xoá hàng FE-14, Tổng 81→79 (UC con giữ 63).
