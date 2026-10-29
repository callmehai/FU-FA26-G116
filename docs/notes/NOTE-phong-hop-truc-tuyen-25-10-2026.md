# Phòng họp trực tuyến — rà soát FE-08 (25/10/2026, cập nhật 29/10/2026)

**Ngày:** 25/10/2026; cập nhật 29/10/2026 sau khi gộp `main`. **Người viết:** BA. **Nguồn:** code trên
nhánh `main` (`backend/TrustCabinet.Web/Endpoints/CallEndpoints.cs`,
`backend/TrustCabinet.Infrastructure/Calls/`, `frontend/src/features/calls/room/`), Jira
SCRUM-39/41/42/43, [ADR-0010](../05-decisions/0010-phong-hop-truc-tuyen-dung-livekit-sfu.md),
[ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md), ghi chú bàn giao
[11](ban-giao-ba-final-review/11-tai-lieu-fe-phong-hop-ghi-am.md) và
[16](ban-giao-ba-final-review/16-ghi-am-voice-id-28-29-10-2026.md), Report 3 mục 8.

Note này ghi **phần code chưa khớp yêu cầu** và những gì đã đổi khi gộp `main`. Đặc tả UC-37 → UC-42,
UC-39d, UC-78 trong Report 3 viết theo yêu cầu đã chốt và đối chiếu với code; phần Guest còn là yêu cầu,
chưa phải mô tả code.

## Số hiệu sau khi gộp `main` (29/10/2026)

`main` đã dùng `BR-322` (không nhận thông báo về hành động của chính mình), `BR-333`–`BR-335` (kho tài
liệu) và `MSG290`–`MSG327` (bản ghi, gỡ băng, Voice ID), nên số của nhánh này được đánh lại:

| Loại | Trước | Sau |
|---|---|---|
| BR phòng họp | `BR-322` → `BR-331` | `BR-323` → `BR-332`; thêm `BR-336` (tối đa 50 người một phòng), `BR-337` → `BR-339` (đăng ký phát biểu, UC-79) |
| Mã FE: bị chặn micro/camera | `MSG119`, `MSG120` | `MSG334` (một câu có `{thiết bị}`) |
| Mã FE: không chia sẻ được màn hình | `MSG121` | `MSG341` |
| Mã FE: gửi chat lỗi | `MSG125` | `MSG343` |
| Mã FE: tắt mic người khác thành công | `MSG290` | `MSG342` |
| Mã FE: bị chủ toạ tắt mic | `MSG291` | `MSG339` |
| Mã FE: bị người khác lấy lượt trình bày | `MSG292` | `MSG340` |
| Mã FE: phòng đóng khi cuộc họp kết thúc | `MSG293` | `MSG331` |
| Mã FE: vào từ tab/thiết bị khác | `MSG294` | `MSG329` |
| Mã FE: không còn trong danh sách | `MSG295` | `MSG330` |
| Mã FE: mất kết nối | `MSG296` | `MSG332` (đang nối lại: `MSG333`) |
| Mã FE: không thấy / đang bị dùng / không mở được thiết bị | `MSG297`, `MSG298`, `MSG299` | `MSG335`, `MSG336`, `MSG337` |
| Mã FE: bị chặn phát âm thanh; tab rời phòng không qua nút Rời; cuộc họp đã huỷ | — | `MSG338`; `MSG328`; `MSG350` |

`MSG119`–`MSG122`, `MSG125` ngừng dùng, không tái cấp. `MSG344` (nút Rời phòng bị khoá khi Thư ký ghi
cả cuộc gọi) vẫn còn trong code `main` nhưng note 16 bỏ việc ghi cả cuộc gọi, nên UC không dùng nó.
Mã tiếp theo không đổi (`MSG351`; note 16 giữ chỗ `MSG351`–`MSG367` cho ghi âm).

## Đã cập nhật trong đợt này

| Loại | File |
|---|---|
| UC Specification | UC-37, UC-38, UC-39a–UC-39d, UC-40, UC-41, UC-42, UC-78 và **UC-79** (mới) trong [05-use-case-specifications.md](../01-requirements/05-use-case-specifications.md#fe-08--online-meeting-room-management) |
| Business Rules | `BR-323` → `BR-332`, `BR-336` → `BR-339` trong [06-business-rules.md](../01-requirements/06-business-rules.md); `BR-329` thêm ý PO 26/10/2026 (không khoá đổi vai trò khi đang họp) |
| Message codes | Mã FE `MSG328`–`MSG343`, `MSG350` có dòng trong Report 3 §V.2; danh mục đánh dấu ngừng dùng như trên |
| API contract | [09-phong-hop-truc-tuyen.md](../03-api/09-phong-hop-truc-tuyen.md) |
| Danh mục UC | [00-master-use-case-inventory.md](../01-requirements/00-master-use-case-inventory.md): 79 UC |
| Report 3 | DOCX + Markdown: UC-37 → UC-42, UC-39d, UC-78, UC-79; hai ảnh mới ở màn 8.1 và 8.2; bỏ gọi 1–1 khỏi từ điển dữ liệu (SCRUM-40 đã bỏ) |
| Wireframe | [v10](../wireframe/TrustCabinet-wireframe-v10.html): màn mới 8.1b (chọn thiết bị) và 8.2b (cửa sổ nổi) |
| Sequence diagram | [online-meeting-room](../06-sequencediagram/online-meeting-room/README.md): UC-37, UC-38, UC-39a–UC-39d, UC-40, UC-41, UC-42, UC-78, UC-79 |

## Khớp code (kiểm ngày 29/10/2026)

- Phòng chỉ mở khi `InProgress` (PO 26/10/2026, SCRUM-41): trước giờ họp màn chờ hiện `MSG117` và tự
  mở khi nhận thông báo `MeetingStarted`; `Ended` hiện `MSG331`; `Cancelled` hiện `MSG350`. Đặc tả: UC-37 AF-3, EX-1, EX-6.
- Phím tắt Ctrl+D (micro) / Ctrl+E (camera); trên Mac là Cmd. Chat tối đa 1.000 ký tự. Chọn loa chỉ có
  khi trình duyệt có `setSinkId`. Cửa sổ nổi dùng Document Picture-in-Picture và tự mở khi chuyển tab.
- **Tối đa 50 người một phòng:** do `room.max_participants` trong cấu hình LiveKit chặn; không có mã
  riêng, FE hiện lỗi kết nối chung. Đặc tả có `BR-336` và UC-37 EX-7 **không gán mã MSG**. Cần PO/BE chọn
  mã (lấy từ `MSG351` trở đi, sau khi hỏi nhánh ghi âm đang giữ `MSG351`–`MSG367`).
- Ghi âm (note 16): họp trực tuyến ghi mic từng người, nút mic là công tắc duy nhất, màn chờ báo "ghi âm
  để lập biên bản · tự xoá sau 180 ngày". UC-37 và UC-39a chỉ nhắc ngắn, dẫn sang FE-09; **không** viết
  luồng ghi âm (nhánh khác làm). Không còn chuyện nút Rời phòng bị khoá vì ghi âm.

## 4 điểm còn hở (trạng thái 29/10/2026)

1. **Đồng bộ vai trò trong phòng (BR-329): xong ở `main`** (commit `b650570`,
   `frontend/src/features/calls/room/roomRoles.ts`): FE đọc lại danh sách thành phần mỗi lần
   `participantsChanged`, nên đổi Chủ toạ giữa họp có hiệu lực trong phòng mà không cần vào lại. Server
   vẫn đọc lại chủ toạ từ database khi tắt mic. Không còn cần ghi "chưa triển khai" vào BR-329, UC-41, UC-73.
2. **Hằng số mã MSG ở FE: xong ở `main`** (`roomMessage` trong `roomSupport.ts`, mỗi câu có chú thích mã).
   Danh mục và Report 3 đã khớp (`MessageCodeCatalogueTests`).
3. **Sequence diagram FE-08: xong** — thư mục `online-meeting-room/`.
4. **Comment Jira: đã soạn nháp, chưa đăng** — nháp nằm trong báo cáo bàn giao PR #147 (SCRUM-41 đổi số MSG/BR,
   SCRUM-27 UC-79 mới, SCRUM-40 đã bỏ khỏi Report 3).

## Còn mở

### 1. Guest chưa vào được phòng họp (UC-37 AF-2) — chờ SCRUM-25

**Yêu cầu.** Guest có lời mời hợp lệ là actor của UC-37, UC-38, UC-39a–UC-39d, UC-40, UC-42, UC-78, UC-79
(ADR-0016: media, chat, chia sẻ màn hình, danh sách người trong phòng như Member; không điều phối phòng).
Đặc tả **giữ Guest** vì đó là yêu cầu.

**Code: chưa làm.** `CallEndpoints.cs` gắn `RequireActiveUser`; `CallRepository.FindRoomAccessAsync`
chỉ so `meeting_participants.UserId`; `UnavailableMeetingGuestIdentityResolver` trả `null` — chưa có xác thực
Guest (SCRUM-25). Guest mở phòng nhận `404`. **PR phải ghi rõ: Guest chưa được cài.**

**Hướng làm (khi SCRUM-25 có guest token).** Cho endpoint `call` nhận `MeetingGuestSession`, cấp quyền theo
`guest_invitations` còn hiệu lực đúng cuộc họp, đặt `identity` theo định danh Guest (không dùng tên hiển thị),
`metadata.meetingRole = "Guest"`, cho job đối soát theo cùng nguồn. Không cấp quyền tắt mic.

### 2. UC-79 Đăng ký phát biểu (SCRUM-27) — yêu cầu, chưa cài

Chỉ có bảng `speaking_requests` (entity `SpeakingRequest`: cuộc họp, mục nghị sự, người xin, thứ tự, trạng thái
`Queued/Invited/Speaking/Finished/Cancelled`, giờ bắt đầu/kết thúc). Chưa có endpoint hay màn hình. UC-79 viết
theo ghi chú 11 §3 ý 1 và §6.2.4; chế độ "phát biểu theo lượt" (Chủ toạ mời từng người, như AI-eCabinet) cần PO
xác nhận chi tiết. UC-79 không dùng mã MSG mới; trường hợp "không còn là Chủ toạ" chỉ ẩn nút, chưa có câu báo.

### 3. Không làm (có chủ ý)

- Tắt mic tất cả, mời người ra khỏi phòng; panel Biểu quyết và Tài liệu trong phòng (màn 8.5, 8.6 chưa phải yêu cầu đợt này).
- **Gọi 1–1 (SCRUM-40): đã bỏ** (PO 26/10/2026) khỏi UC và từ điển dữ liệu Report 3. Enum `CallKind.Direct` và
  `CallStatus.Ringing/Missed` còn trong code và `docs/02-design/02-erd.md`; xoá khi BE dọn.
- Ghi cả cuộc gọi từ máy Thư ký (SCRUM-44): bỏ theo note 16.
- Chat lưu lại: `BR-327` chốt không lưu.

### 4. Việc ngoài repo

- Report 3 trên Drive chưa thay (chỉ sửa DOCX local, theo yêu cầu không đẩy Drive).
- Figma: plugin `figma-v10/` chưa chạy trong Figma Desktop.
