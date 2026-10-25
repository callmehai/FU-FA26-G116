# Phòng họp trực tuyến — rà soát FE-08 ngày 25/10/2026

**Ngày:** 25/10/2026. **Người viết:** BA. **Nguồn:** code đang chạy trên nhánh `main`
(`backend/TrustCabinet.Web/Endpoints/CallEndpoints.cs`,
`backend/TrustCabinet.Infrastructure/Calls/`, `frontend/src/features/calls/room/`), Jira SCRUM-39/41/42/43,
[ADR-0010](../05-decisions/0010-phong-hop-truc-tuyen-dung-livekit-sfu.md),
[ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md) và Report 3 mục 8.

Note này ghi **phần code chưa khớp yêu cầu**. Đặc tả UC-37 → UC-42, UC-39d, UC-78 trong Report 3 đã
viết theo yêu cầu (không viết xuống theo code), nên hai điểm dưới đây là việc của BE, không phải
sửa lại đặc tả.

## Đã cập nhật trong đợt này

| Loại | File |
|---|---|
| UC Specification | UC-37, UC-38, UC-39a, UC-39b, UC-39c, **UC-39d**, UC-40, UC-41, UC-42, **UC-78** trong [05-use-case-specifications.md](../01-requirements/05-use-case-specifications.md#fe-08--online-meeting-room-management) |
| Business Rules | BR-322 → BR-331 trong [06-business-rules.md](../01-requirements/06-business-rules.md) |
| Message codes | `MSG119`–`MSG121`, `MSG125` chuyển từ dành sẵn sang FE; `MSG122` ngừng dùng; `MSG290`–`MSG299` cấp mới (FE); mã tiếp theo `MSG300` — [danh mục](../03-api/00-danh-muc-ma-thong-bao.md#thông-điệp-phía-trình-duyệt--scrum-39-scrum-41) |
| API contract | [09-phong-hop-truc-tuyen.md](../03-api/09-phong-hop-truc-tuyen.md): thêm hành vi phía trình duyệt, mã FE, lý do ngắt kết nối |
| Danh mục UC | [00-master-use-case-inventory.md](../01-requirements/00-master-use-case-inventory.md): FE-08 còn 7 UC / 4 UC con; tổng 78 UC / 40 UC con |
| Report 3 | DOCX + Markdown: thay 8 placeholder FE-08 bằng bảng 7 dòng, thêm UC-39d và UC-78, BR-322 → BR-331, 15 dòng mã thông báo, sửa màn 8.1/8.2 và bảng màn hình §I.5.b |

## BE cần làm

### 1. Guest chưa vào được phòng họp (UC-37 AF-2, BR-322)

**Yêu cầu.** UC-37, UC-38, UC-39a–UC-39d, UC-40, UC-41, UC-42 và UC-78 đều nhận **Guest** có lời
mời hợp lệ làm actor. ADR-0016 cho Guest media, chat, chia sẻ màn hình và danh sách người đang trong
phòng như Member; chỉ không được điều phối phòng.

**Code hiện tại.**

- `CallEndpoints.cs` gắn `.RequireActiveUser(...)` cho cả ba endpoint, nên principal không phải tài
  khoản nội bộ bị chặn trước khi tới nghiệp vụ.
- `CallRepository.FindRoomAccessAsync` chỉ so `meeting_participants.UserId == userId`, không xét
  `guest_invitations`.
- `UnavailableMeetingGuestIdentityResolver` trả `null` — **chưa có xác thực Guest** (SCRUM-25 chưa
  cài), nên chưa endpoint nào phân biệt được Guest với người lạ.

**Hệ quả.** Guest mở phòng nhận `404` như cuộc họp không tồn tại. Phần Guest trong đặc tả là yêu
cầu, chưa phải mô tả code.

**Hướng làm.** Khi SCRUM-25 có guest token: cho endpoint `call` nhận cả `MeetingGuestSession`, cấp
quyền vào phòng theo `guest_invitations` (còn hiệu lực, đúng cuộc họp), đặt `identity` của phòng là
định danh guest (không dùng display name), `metadata.meetingRole = "Guest"`, và cho janitor đối soát
theo cùng nguồn. Không cấp quyền tắt mic cho Guest.

### 2. Đổi vai trò khi phòng đang mở không tới được phòng (UC-40 AF-2, BR-328)

**Yêu cầu.** BR-328: đổi Meeting Role có hiệu lực trong phòng mà không phải vào lại — phòng hiện
vai trò mới và quyền mới ngay.

**Code hiện tại.** `CallRoomService` ghi `metadata = {"meetingRole": ...}` **một lần lúc cấp token**
(`CallRoomService.cs`, dòng ~91). FE đọc đúng metadata đó (`roomSupport.meetingRoleOf`) để xếp hàng
và hiện huy hiệu chủ toạ. Sau UC-73 (giao chủ toạ) hoặc UC-28b (đổi vai trò), metadata trong phòng
không đổi: người vừa nhận vai trò Chủ toạ vẫn hiện "Thành viên", người vừa trả vai trò vẫn hiện
"Chủ toạ" và vẫn thấy nút "Tắt mic" cho tới khi tải lại trang. Quyền tắt mic ở server thì **đúng**:
`CallRoomService.MuteAsync` đọc lại chủ toạ từ database, nên người đã trả vai trò bị `403 MSG123` —
chỉ phần hiển thị sai.

**Hướng làm.** Sau khi lưu thay đổi vai trò, gọi RoomService API cập nhật metadata của người trong
phòng (`UpdateParticipant` với metadata mới), hoặc phát một sự kiện để FE đọc lại vai trò từ
`GET /api/meetings/{id}/participants`. Cách thứ hai rẻ hơn và dùng được cho cả UC-28b.

## Không phải lỗi — đã cố ý để ngoài phạm vi

- **Giơ tay, tắt mic tất cả, mời người ra khỏi phòng:** không có trong phiên bản này; UC-41 chỉ còn
  "Chủ toạ tắt mic người khác" và §III 8.3 ghi "Not provided".
- **Panel Biểu quyết (màn 8.5) và panel Tài liệu (màn 8.6):** đang là "Planned" trong bảng màn hình
  §I.5.b, chưa phải yêu cầu của đợt này.
- **Ghi âm trong phòng (SCRUM-44):** Report 1 LI-01 cấm bot ghi âm tham gia phòng; chưa làm.
- **Chat lưu lại:** BR-326 chốt không lưu; FE dùng text stream của LiveKit, không gọi API nào.

## Còn phải làm (không phải BE)

- Report 3 trên Drive chưa thay (chỉ sửa DOCX local).
- Wireframe 8.1–8.7: bản vẽ hiện tại chưa có màn hình nổi (UC-78) và khay chọn thiết bị của UC-39d.

## 4 điểm còn hở cần xử lý tiếp trên nhánh này

### 1. Đồng bộ trạng thái triển khai: BR-328, UC-41, UC-73 và nhánh Guest của UC-37
- **Hiện trạng**: 
  - `BR-328` ghi "Đổi vai trò có hiệu lực ngay trong phòng họp mà không cần vào lại", nhưng LiveKit metadata hiện tại chỉ cấp 1 lần lúc tạo token. Sau khi Chair bàn giao qua UC-73 hoặc đổi role qua UC-28b, người dùng trong phòng chưa nhận được metadata mới ngay lập tức.
  - Nhánh Guest của `UC-37` (AF-2) phụ thuộc xác thực Guest (`UC-04` / SCRUM-25), hiện tại backend trả `404` vì chưa có token Guest.
- **Phương án chọn để thực hiện tiếp**:
  - Ghi chú rõ trạng thái **"Chưa triển khai (Chờ cập nhật LiveKit metadata / Token Guest UC-04)"** trong đặc tả `BR-328`, `UC-41`, `UC-73` và `UC-37` để phản ánh đúng hiện trạng giữa tài liệu và code.
  - Hoặc triển khai bổ sung cơ chế gửi SignalR event / gọi LiveKit UpdateParticipant API khi đổi role và hoàn thiện Guest resolver khi UC-04 hoàn tất.

### 2. Định nghĩa hằng số mã thông báo MSG ở Frontend (`RoomView.tsx`)
- **Hiện trạng**: Các mã `MSG290`–`MSG299` và `MSG119`–`MSG121`, `MSG125` đã được đưa vào danh mục mã thông báo (`docs/03-api/00-danh-muc-ma-thong-bao.md`), và giao diện `RoomView.tsx` (dòng 853...) đang hiển thị chuỗi tiếng Việt khớp hoàn toàn với nội dung danh mục. Tuy nhiên, code FE đang hardcode chuỗi trực tiếp, chưa khai báo các hằng số mã `MSGnn`.
- **Việc cần làm**:
  - Tạo file hằng số thông báo FE (ví dụ `frontend/src/features/calls/room/callMessages.ts` hoặc tương đương).
  - Khai báo các constant mã `MSG119`, `MSG120`, `MSG121`, `MSG125`, `MSG290`–`MSG299` kèm text tương ứng để code FE và catalogue đồng bộ 100%.

### 3. Vẽ Sequence Diagram cho FE-08 (Online Meeting Room)
- **Hiện trạng**: Thư mục `docs/06-sequencediagram/` hiện đã có đầy đủ sơ đồ cho các feature khác (Agenda, Participant, Voting, Notification...) nhưng chưa có thư mục riêng cho phòng họp trực tuyến FE-08.
- **Việc cần làm**:
  - Tạo thư mục `docs/06-sequencediagram/online-meeting-room/` kèm `README.md`.
  - Thiết kế các file sequence diagram (`.txt` và drawio / PNG):
    - `UC37_JoinMeetingRoom` (gồm Main flow và AF-2 Guest join)
    - `UC38_LeaveMeetingRoom` (rời phòng, ngắt kết nối duplicate session)
    - `UC39_ManageRoomControls` (`UC39a` Mic, `UC39b` Camera, `UC39c` Screen Share, `UC39d` Chọn thiết bị)
    - `UC40_ViewParticipants`
    - `UC41_ModerateMeetingRoom` (Chủ toạ tắt mic một chiều)
    - `UC42_InRoomChat` (Text stream LiveKit)
    - `UC78_FloatingMeetingWindow` (Document PiP)

### 4. Ghi chú cập nhật Ticket Jira cho các điểm lệch yêu cầu
Theo quy trình phát triển ([DEVELOPMENT_WORKFLOW.md](../../DEVELOPMENT_WORKFLOW.md)), các thay đổi và thu hẹp nghiệp vụ cần được ghi comment vào ticket Jira trước khi chuyển BA Final Review:
- **SCRUM-39 (Join/Leave Room)**:
  - Ghi chú: Nhánh Guest join (UC-37 AF-2) phụ thuộc token Guest (UC-04 / SCRUM-25), hiện tạm thời trả `404` cho tới khi xác thực Guest sẵn sàng.
  - Một tài khoản một kết nối (BR-327, LiveKit duplicate identity kick out).
- **SCRUM-41 (Room Moderation & Chat)**:
  - Ghi chú thu hẹp phạm vi: Chỉ làm chức năng Chủ toạ tắt mic người khác 1 chiều (`UC-41`, `BR-324`). Không làm giơ tay, tắt mic tất cả và mời người ra khỏi phòng.
  - Chat trong phòng (`UC-42`, `BR-326`): dùng text stream tạm thời, không lưu vào database và không hiển thị cho người vào sau.
  - Lỗi role metadata (`BR-328`): Đổi vai trò chưa đồng bộ ngay lập tức vào LiveKit metadata trong phiên hiện tại.
- **SCRUM-42 (Device & Stage Controls)**:
  - Ghi chú bổ sung: Tách `UC-39d` (Chọn thiết bị audio/video, lưu `localStorage`, `BR-330`) và bổ sung `UC-78` (Cửa sổ nổi PiP, `BR-331`).
- **SCRUM-43 (Screen Sharing)**:
  - Ghi chú: Tại một thời điểm chỉ 1 người chia sẻ màn hình (`BR-325`), khi người khác chia sẻ thì người trước tự động dừng và nhận `MSG292`.

