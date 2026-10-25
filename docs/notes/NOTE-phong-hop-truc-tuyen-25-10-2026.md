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
- Sequence diagram cho UC-37 → UC-42, UC-39d, UC-78 chưa vẽ.
