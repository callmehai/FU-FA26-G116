# PR #350 / #351 / #352 vào `main` 15/11/2026 — quyết định PO sau merge và việc còn lại

**Người ghi:** lead (ghi theo nhắn của PO/Hải ngày 15/11/2026, sau khi cả ba PR đã merge).
**Nguồn:** nhắn PO báo "PR 350 và 351 đã review và merge xong", Hải trả lời 3 câu, tóm tắt nội dung từng PR,
Jira các ticket liên quan đã comment xong, "Đã merge vào main hôm nay".

**Merge trên `main` (thứ tự stack):**

- PR #350 `5c29ece0` — Report 3 cho thu Voice ID hộ, ghi âm theo hình thức họp, tự kết thúc họp bị bỏ quên.
- PR #351 `d634ecd2` — Bỏ ký số khỏi Report 3.
- PR #352 `e7c3f295` (commit `724e0cb0`) — Xoá bảng chữ ký biên bản (code + docs).

## 1. Ba trả lời của Hải (giữ nguyên ý)

1. **Luật bỏ tiếng vọng loa phòng: không đưa vào Report 3**, vì chưa đo ở buổi họp kết hợp thật.
   Dòng nháp BR-459 đã bỏ khỏi DOCX Report 3, quyết định ghi ở note 45.
   Code vẫn giữ (ADR-0061 điểm 3 + `RoomEcho`).
2. **Đặc tả màn hình thu hộ và ghi âm họp kết hợp: để sau.**
   Gồm tab Thư ký thu / hộp thoại thu hộ (note 44 §2.6) và nhãn Tổng hợp + cảnh báo micro phòng (note 45 §2.5).
3. **Bảng chữ ký biên bản: xoá luôn** — đã làm trong PR #352, kèm sửa ERD, Report 3 phần thực thể,
   BF-04 và sơ đồ ngữ cảnh.

## 2. Tóm tắt từng PR (đã đối chiếu commit trên `main`)

- **PR #350 (Khánh)** — Report 3 cho thu Voice ID hộ (note 44: UC-81a AF-2, sửa câu 81b–81f, UC-86a–d,
  BR-448→, MSG473–477), ghi âm theo hình thức họp + bản tổng hợp (note 45: sửa BR-340/342, UC-44c EX MSG478,
  UC-43a AF bản tổng hợp, UC-45c EX MSG479, ERD `RecordingSource.Combined`), tự kết thúc họp bị bỏ quên
  (note 46: BR mới 24 giờ, BR-302 giữ Retired, UC-22 không đổi luồng, SM-01, ERD `MeetingStatusHistory.Action`
  thêm `AutoEnd`). Đã kiểm khớp code: mã thông báo, luật, ERD, sơ đồ trạng thái.
  Bỏ luật tiếng vọng loa phòng khỏi Report 3 (code giữ). Đặc tả màn hình để sau.
- **PR #351 (Khánh)** — Bỏ ký số khỏi Report 3: gỡ hẳn UC-62/63 (§12.5/12.6 + bảng danh mục),
  BR-166 Retired, dọn SM-13/14, UC diagram, prose. Sửa dòng ERD tự mâu thuẫn
  (note 47: vừa ghi "sẽ xoá bảng" vừa ghi "không cần migration").
- **PR #352 (Hải)** — Xoá bảng `minutes_signatures` thật:
  code: migration `20261115091231_DropMinutesSignature`, bỏ entity `MinutesSignature`, cấu hình, `DbSet`,
  bỏ `MeetingMinutes.Signatures`; test `DatabaseModelTests` sửa theo.
  Docs: ERD mục tiêu + Chen vẽ lại, Report 3 bỏ Entity 27 + viết lại Entity 26 theo luồng gửi duyệt →
  Chủ tọa duyệt là ban hành, BF-04, sơ đồ ngữ cảnh (chữ ký → duyệt), tài liệu API dự thảo biên bản, README yêu cầu.
  Bảng chưa từng có dữ liệu nên không mất gì; server test tự chạy migration ở deploy sau.

## 3. Điểm cần nhớ (kẻo đọc note cũ bị nhầm)

- **Note 47 §"Ranh giới: không đụng code và ERD theo code" đã hết hiệu lực.**
  Đó là quyết định tạm thời lúc viết note 47 (chờ migration BE riêng).
  PR #352 đã thay thế: xoá bảng thật trên cả code và ERD. Ai đọc note 47 thì đọc thêm note này.
- Jira các ticket liên quan PO báo đã comment xong (SCRUM-37, SCRUM-106, SCRUM-152, SCRUM-153,
  SCRUM-99 theo note 44–47). Giữ nguyên trạng thái ticket, chỉ comment (152/153 đang `Done`, 99 `Done`).

## 4. Việc còn lại

- [ ] **Khánh làm tiếp docs thì pull `main` trước** — DOCX Report 3 vừa đổi qua cả ba PR
  (PR #350 + #351 + #352 đều chạm DOCX và sync lại snapshot + `uc-map.html`).
  Nhánh `docs/ba-final-review-note47-bo-ky-so` dừng ở `6c334ce2`, **chưa có** commit PR #352 (`724e0cb0`)
  — trên nhánh này `MinutesSignature.cs` + `MeetingMinutes.Signatures` vẫn còn, chưa có migration Drop.
  Không sửa DOCX tiếp từ nhánh cũ khi chưa pull/rebase `main`.
- [ ] **Ai đang có nhánh BE thì pull `main` và chạy lại migration ở máy**
  (có migration mới `DropMinutesSignature`). Không cần backup bảng `minutes_signatures`
  (chưa từng có dữ liệu).
- [ ] Đặc tả màn hình thu hộ + ghi âm họp kết hợp: **để sau** theo PO (note 44 §2.6, note 45 §2.5).
  Không đưa vào đợt này.
- [ ] Luật tiếng vọng loa phòng: **không** đưa vào Report 3. Giữ ở ADR-0061 + code.
  Không tạo lại BR-459.
- [ ] Note 48 (dựng máy AI local dự phòng qua Tailscale): việc vận hành, **chưa làm**, không sửa Report 3.
- [ ] Kiểm lại một lượt trên `main` sau merge: Report 3 không còn UC-62/63, BR-166 Retired,
  Entity 27 đã bỏ, BF-04 + context diagram đã đổi chữ ký → duyệt, `02-erd` khớp code
  (không còn `MinutesSignature`).
