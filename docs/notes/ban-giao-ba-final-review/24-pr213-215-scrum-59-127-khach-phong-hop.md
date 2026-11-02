# PR #213, #215 — Khách vào phòng họp trực tuyến (SCRUM-59 BE, SCRUM-127 FE): việc BA Final Review

**Người ghi:** KhanhPG (BA), 02/11/2026. **Nguồn:** [PR #213](https://github.com/callmehai/FU-FA26-G116/pull/213) (BE) và
[PR #215](https://github.com/callmehai/FU-FA26-G116/pull/215) (FE), mục "Deviations"; comment Jira của Lộc ngày 01/11/2026:
[SCRUM-59](https://sep490g116.atlassian.net/browse/SCRUM-59?focusedCommentId=10392),
[SCRUM-127](https://sep490g116.atlassian.net/browse/SCRUM-127?focusedCommentId=10393). Contract:
[09-phong-hop-truc-tuyen.md](../../03-api/09-phong-hop-truc-tuyen.md) mục **Khách (SCRUM-59)**. Note này gom việc BA, không thay
Jira hay đặc tả.

> **Tiến độ 02/11/2026: CHƯA LÀM GÌ.** Mới ghi note.

## 1. Trạng thái

- Stack #212 → #213 → #215, **cả ba đã merge vào `main`** (01/11/2026). Màn Khách và tài liệu: [note 23](23-pr212-scrum-121-122-123-man-khach.md).
- Mã mới: **MSG380** (400, Khách chưa xác nhận thông báo ghi âm). Danh mục đã có; mã tiếp theo là **MSG381** (FE không dùng).
- Migration `GuestCallParticipants`: `call_participants.user_id` cho phép null, thêm `guest_session_id` (check đúng một trong hai);
  `call_sessions.started_by_id` cho phép null. ERD Markdown đã sửa trong PR; **ERD drawio chưa**.
- **Mâu thuẫn với PO chốt 02/11:** #213 điểm 8 và #215 ghi "không ghi âm theo micro cho Khách". PO đã chốt **có ghi**, Hải làm ở
  SCRUM-129 ([note 21 §4.1 D](21-scrum-116-truy-cap-khach-uc04.md)). Đừng ghi "Khách không ghi âm" vào UC; chờ SCRUM-129.

## 2. Đã làm

- `GET /api/meetings/{id}/call` và `POST …/call/join` nhận token Khách; mỗi lần gọi kiểm lại phiên, lời mời, đúng cuộc họp, đã khai tên,
  cuộc họp `InProgress` và Online/Hybrid.
- Khách phải tick xác nhận thông báo ghi âm ("được ghi âm để lập biên bản, tự xoá sau 180 ngày") mới bật nút Vào phòng. BE ghi bằng
  chứng vào audit `call.guest_admitted` (phiên, lời mời, tên tự khai, IP, thời điểm) trước khi cấp token; không ghi được thì không cấp.
- Quyền LiveKit như Thành viên (ADR-0016): mic, camera, chia sẻ màn hình, chat, xem ai trong phòng. Không có quyền điều phối (UC-41):
  Khách không tắt mic người khác (403); Chủ toạ tắt mic Khách được. Nhãn "Khách" + tooltip "Tên tự khai", không hiện email.
- Trang Khách có nút **Vào phòng họp** khi phòng mở; chưa bắt đầu hiện MSG117, họp trực tiếp hiện MSG280. Phòng ở `/guest/{id}/room`.
- Thu hồi lời mời (cả gỡ thành viên) → ngắt ngay khỏi phòng; phiên hết hạn/đăng xuất → job 30 giây đưa ra. Cuộc họp kết thúc → MSG331
  rồi màn kết thúc. Mở cùng phiên ở tab khác → tab cũ MSG329.
- Lượt vào/ra của Khách ghi ở `call_participants.guest_session_id`, **không** ghi `meeting_participants`.

## 3. Điểm khác tài liệu, và chỗ phải sửa

| # | Khác gì | Nguồn | Chỗ BA sửa |
|---|---|---|---|
| 1 | Phòng chỉ mở cho Khách khi cuộc họp **`InProgress`** (UC-37 PRE-3, kế hoạch D6), không phải "Published hoặc InProgress" như mô tả ticket. Khi `Published` Khách thấy `NotStarted`/MSG117 | 59 #1 | UC-37 PRE: ghi rõ áp dụng cả Khách; sửa mô tả SCRUM-59 |
| 2 | Lượt vào/ra tham chiếu **phiên Khách**, không phải lời mời như ERD cũ (`GuestInvitationId`) | 59 #2 | ERD drawio (`call_participants.guest_session_id`, nullable `user_id`, `call_sessions.started_by_id`); class diagram nếu có |
| 3 | Danh tính LiveKit **theo phiên** (`guest-{phiên}`), không theo lời mời: một Khách xác minh trên hai thiết bị là hai kết nối; thu hồi lời mời ngắt cả hai | 59 #3 | UC-37/38: ghi chú; BR nếu có câu "một Khách một kết nối" |
| 4 | IP lấy từ request xin token (webhook LiveKit không có IP). Giờ vào/ra ở `call_participants`; tên, email đi qua phiên → lời mời | 59 #4 | UC log lượt vào/ra của Khách (mô tả SCRUM-59): nguồn dữ liệu |
| 5 | Bằng chứng thông báo ghi âm chỉ là **cờ xác nhận trong audit**; nội dung câu thông báo do FE hiển thị, không lưu phiên bản | 59 #5 | UC-37 bước Khách vào phòng: thêm bước xác nhận bắt buộc + EX MSG380. Ghi rủi ro Luật 91/2025: chứng minh được Khách đã được báo, nhưng không lưu đúng câu đã hiện |
| 6 | Đăng xuất/hết hạn phiên xử lý bằng job 30 giây (không tức thì); thu hồi lời mời thì ngắt ngay | 59 #6 | UC-28c POST-2 (đã ghi "ends every session"): thêm "Khách đang trong phòng bị ngắt ngay"; hết hạn/đăng xuất ≤30 giây |
| 7 | **MSG380** (400) mới: Khách chưa xác nhận thông báo ghi âm | 59 #7 | Report 3 §V.2: thêm dòng MSG380 (tiếng Anh theo danh mục) |
| 8 | Khách chưa có quyền với bản ghi và gỡ băng; FE không ghi âm theo micro cho Khách | 59 #8, 127 | **Không ghi vào UC** — PO đã chốt có ghi (SCRUM-129). Giữ "Khách không xem bản ghi/gỡ băng" |
| 9 | Số **180 ngày** trong thông báo ghi âm **ghi cứng ở FE** (Khách không đọc được API ghi âm). Đổi `RecordingStorage:RetentionDays` thì BE phải trả thời hạn trong `GET …/call` | 127 #1 | BR thời hạn lưu bản ghi: ghi chú phụ thuộc; ghi vào danh sách rủi ro |
| 10 | Màn chờ tự mở bằng **hỏi lại server mỗi 10 giây** và khi quay lại tab (hub không báo bắt đầu cuộc họp cho Khách) | 127 #2 | UC-37 AF Khách chờ phòng mở |
| 11 | Cuộc họp kết thúc: hiện MSG331 trước, rồi sang màn kết thúc trong tối đa 20 giây | 127 #3 | UC-42 (hoặc UC kết thúc phòng) phần Khách |
| 12 | **I2/I3 khác I1:** phiên hết hạn hoặc đăng xuất ở tab khác → Khách bị đưa ra (~20 giây), dữ liệu gỡ khỏi màn hình, nhưng lời mời còn hiệu lực nên về màn nhập mã MSG378, xác minh lại thì vào lại được. Thu hồi lời mời → màn kết thúc, không vào lại | 127 #4 | UC-04/UC-37: tách hai nhánh; sửa kỳ vọng I2/I3 trong ticket SCRUM-127 |
| 13 | Không cần mã mới cho màn kết thúc (MSG381 chưa dùng) | 127 #5 | Chỉ ghi nhận |

## 4. Việc BA, theo thứ tự

- [ ] UC-37 (vào phòng): PRE `InProgress` cho cả Khách; bước xác nhận thông báo ghi âm + EX MSG380; AF chờ phòng mở (điểm 1, 5, 10).
- [ ] UC-38–UC-42: Khách có quyền như Thành viên, không điều phối; Chủ toạ tắt mic Khách được (điểm 3, 11). Rà theo cột UC của BR-181.
- [ ] UC-28c POST-2 và UC-04: thu hồi ngắt khỏi phòng ngay; hết hạn/đăng xuất về màn nhập mã (điểm 6, 12).
- [ ] Report 3 §V.2: MSG380.
- [ ] ERD drawio + class diagram phòng họp: `call_participants.guest_session_id`, các cột nullable (điểm 2).
- [ ] Ghi rủi ro: 180 ngày ghi cứng (điểm 9); bằng chứng thông báo không lưu câu (điểm 5).
- [ ] Ghi âm Khách: **chờ SCRUM-129**, rồi mới sửa UC ghi âm (điểm 8).
- [ ] Đặc tả màn hình: màn chờ của Khách (ô xác nhận ghi âm), nhãn "Khách" trong phòng.
- [ ] Sync Report 3, Change Log.
