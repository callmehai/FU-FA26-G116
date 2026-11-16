# Rà soát SCRUM-135 — 19 điểm docs còn lệch + 6 câu hỏi PO

**Người ghi:** Khánh, 16/11/2026. **Nguồn:** ba mũi đối chiếu docs-vs-code trên `main` (`bfb3bf5d`) ngày 16/11/2026; kết quả đã comment lên Jira (`10622`). Ticket SCRUM-135 giữ `BA Final Review`, chưa đóng. SCRUM-208 (email FE-13) giữ `To Do`, làm sau note này.

**Phạm vi:** chỉ sửa docs, không đụng code. Các điểm nằm trong Report 3 extract (`05-use-case-specifications.md`, `06-business-rules.md`) thì sửa trong DOCX ở `docs/08-reports/source/` rồi chạy `python scripts/sync_reports.py --report 3`, không sửa tay file extract.

**Đã khớp, không cần làm gì (15 ticket):** SCRUM-35 (cả 4 ADR-0045/0048/0049/0050 đã vào BR/UC/code), 17, 21, 22, 26, 29, 47, 60, 63, 98, 105, 107, 109. Ba điểm UNCLEAR đã có đáp án theo code: UC-05 không phân trang là chủ đích; ADR-0032 hủy trước công bố thì không đọc được (quyết định #4 + `MeetingReadPolicy`); BR-09 code cố tình giữ cửa sổ access token ≤ 15 phút (chỉ thu hồi refresh token).

## 1. Cụm biểu quyết (điểm 1–3, sửa trực tiếp file `.md`)

- [ ] **Điểm 1 (SCRUM-96):** `docs/03-api/15-bieu-quyet.md` §2 vs §8 — ai được xác nhận đọc trên tài liệu chỉ gắn nháp: §2 cho "ai xem được Meeting" (Draft cũng cho), §8 bắt Thành viên/Khách `404/MSG156` còn Chủ toạ/Thư ký `409/MSG224`. Chốt một bản theo code (`VoteService` nhánh `MSG156/MSG224/MSG225`).
- [ ] **Điểm 2 (SCRUM-96):** `15-bieu-quyet.md` §4 vs §8 bước 4 — đã xác nhận rồi mà gửi `lastPageReached:false` thì 400 hay 200 `alreadyConfirmed`. Chốt thứ tự ưu tiên đã-xác-nhận trước.
- [ ] **Điểm 3 (SCRUM-28):** `docs/06-sequencediagram/voting-management/README.md` còn đoạn "phiếu kín vẫn có `VoteChoice` không người/không link", trái code (kín = không ghi dòng nào, `VoteService.cs:495-496`). Viết lại theo docs 15 §7.

## 2. Cụm thông báo/email (điểm 4–6, sửa trực tiếp + sequence)

- [ ] **Điểm 4 (SCRUM-14):** `docs/03-api/06-thong-bao-trong-ung-dung.md:61,154` còn "chuông trên app bar, dùng chung hai workspace" — thật là chuông ở chân sidebar, một shell duy nhất (ADR-0012, `AppShell.tsx:159`).
- [ ] **Điểm 5 (SCRUM-14):** Sequence `UC68_OpenNotificationTarget.txt` gộp 2 round-trip (`POST /{id}/read` rồi `GET /{id}/target`) thành một bước server — tách ra theo code (`NotificationEndpoints.cs`, `NotificationsProvider.tsx` `openTarget`). UC-67a/67b đã khớp, không đụng.
- [ ] **Điểm 6 (SCRUM-15):** `docs/03-api/07-gui-email-thu-moi.md` (§12 + § tích hợp SCRUM-19) còn nguyên văn luồng producer cũ `IMeetingInvitationQueue` — viết lại thành `MeetingInteractionDispatch`/`MeetingInvitationDispatchProcessor`/`EmailQueueProcessor`/`MeetingReminderEmails`; cùng họ: `07-gui-email-thu-moi-note-cai-dat-may-moi.md:121` + class diagram feature-04 (`MeetingInvitationQueue`).

## 3. Cụm đăng nhập/người dùng (điểm 7–11; 7–9 qua DOCX + sync)

- [ ] **Điểm 7 (SCRUM-11):** extract UC-02 EX-2 hứa `MSG04` không bao giờ trả (code gộp mọi lỗi verify Google thành `MSG03`, `AuthEndpoints.cs:66-81`).
- [ ] **Điểm 8 (SCRUM-11):** extract UC-02 POST-2 "server-side session" trái kiến trúc JWT stateless (UC-01 không có dòng này).
- [ ] **Điểm 9 (SCRUM-13):** tàn dư "username" ở UC-06a step 5 + BR-13, trong khi code không có username (`02-quan-ly-nguoi-dung.md:1` đã khai chỉ kiểm email).
- [ ] **Điểm 10 (SCRUM-13):** `docs/03-api/02-quan-ly-nguoi-dung.md:9` chê BR-20/BR-22 "không được định nghĩa" dù BR hiện hành đã có — cập nhật mục này.
- [ ] **Điểm 11 (SCRUM-20):** sequence `UC28c_RemoveMeetingParticipant.txt:25` còn "Organizer may recover the missing Secretary", trái PO 20/10 và BR-79 (BR-79: Organizer không có quyền quản trị roster riêng). Kiểm tra PNG UC-28c có cùng nội dung thì xuất lại.

## 4. Cụm tồn đọng khác (điểm 12–19)

- [ ] **Điểm 12 (SCRUM-18):** Report 3 DOCX UC-17 còn "mở sẵn lọc Đang diễn ra" (extract đã đúng "tab Hôm nay") — sửa DOCX + sync. Dính Q1/Q2 (§5).
- [ ] **Điểm 13 (SCRUM-19):** `ReturnToDraft`/`TransferOrganizer` có trong code nhưng inventory ghi "chỉ có trong code, chưa thành UC" (`00-master-use-case-inventory.md:379`) — PO/BA quyết promote thành UC hay dọn code.
- [ ] **Điểm 14 (SCRUM-23/58):** catalogue còn dòng "MSG346–348 chưa lên main" dù code đã có (`DocumentCommentEndpoints`, `PersonalLibraryEndpoints`) — xoá chữ cũ.
- [ ] **Điểm 15 (SCRUM-41):** BR-336/UC-37 EX-7/PRE-4 mô tả chặn 50 người nhưng backend không thực thi — chờ PO (§5) rồi sửa docs theo hướng chốt.
- [ ] **Điểm 16 (SCRUM-99):** ADR-0032 còn để trạng thái "Đề xuất" dù đã triển khai (`MeetingReadPolicy`, `MeetingOverdueService`) — đổi trạng thái ADR.
- [ ] **Điểm 17 (SCRUM-116):** BR-371 giới hạn OTP còn ghi chờ PO — chờ PO (§5) rồi sửa.
- [ ] **Điểm 18 (SCRUM-35):** banner extract còn "DOCX cập nhật 24/10/2026", nội dung đã có UC-84/85 tháng 11 — cập nhật ngày banner (cosmetic, làm kèm lần sync tới).
- [ ] **Điểm 19:** `docs/06-sequencediagram/README.md:579` còn coi UC-09 là "thiết kế đích" (UC-09 đã bỏ hẳn); UC-34b/BR-334/335 viết "locked account" trong khi code là "deactivated" (`PersonalLibraryEndpoints.cs:8`).

## 5. Sáu câu hỏi chờ PO/BA (chi tiết)

1. **BR-09 (cửa sổ token sau đăng xuất):** BR-09 (`06-business-rules.md:33`) đòi sau sign-out không còn quyền nào; code logout chỉ hủy refresh token (`AuthEndpoints.cs:158-163`), access JWT còn xài được tới hết hạn (≤ 15 phút, `AuthenticationOptions.cs`). Tài liệu API (`01-xac-thuc.md`) đã mô tả đúng code. PO chọn: (a) chấp nhận cửa sổ ≤ 15 phút → sửa BR-09 theo code; (b) đòi thu hồi access token thật → việc BE lớn (blacklist/versioning), mở ticket riêng.
2. **BR-04 (wording sau SCRUM-63):** BR-04 đòi "active role/permission" để đăng nhập; sau SCRUM-63 mọi vai trò kể cả 0 capability đều đăng nhập được (`01-xac-thuc.md:123-124` tự ghi "Chờ BA sửa đặc tả UC-01"). BA chốt lại wording BR-04/BR-06 + UC-01/UC-02.
3. **`actions.canCast`:** `15-bieu-quyet.md` §8 FE tự khóa nút khi `readConfirmedAt:null`, nhưng §2/§5 vẫn liệt kê `409/MSG252` như điều kiện bỏ phiếu. PO chốt: BE tính "chưa đọc" vào `canCast:false`, hay giữ FE tự khóa như hiện tại.
4. **Q1/Q2 note 40 (UC-17):** dropdown Trạng thái có giữ "Hôm nay" không (Q1); vẽ lại wireframe 4.3 (Q2). Chưa có thì điểm 12 chỉ sửa được chữ DOCX.
5. **BR-371 (OTP khách):** gửi lại mã cách nhau ≥ 1 phút, tối đa 20 mã/24h — docs còn "NEEDS REVIEW PO". PO chốt số.
6. **Chặn 50 người trong phòng (BR-336/UC-37 EX-7):** docs mô tả chặn và ghi "chưa gán MSG"; backend không thực thi (rào thực tế nằm ở LiveKit ngoài repo). PO chốt: ai chặn, có cần MSG mới không, hay bỏ mốc 50 khỏi docs.

## 6. Đóng ticket (DEVELOPMENT_WORKFLOW §2.2)

Khi các PR docs của note này vào `main`: comment lên SCRUM-135 một dòng cho mỗi cụm (UC/BR/MSG đổi gì, PR + commit, còn gì), rồi chuyển `Done` vì ticket thuần docs. Không viết "đã vào main" từ trang PR — kiểm `git log --oneline origin/main` trước.
