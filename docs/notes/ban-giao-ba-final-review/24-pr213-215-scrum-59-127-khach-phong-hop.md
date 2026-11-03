# PR #213, #215 — Khách vào phòng họp trực tuyến (SCRUM-59 BE, SCRUM-127 FE): việc BA Final Review

**Người ghi:** KhanhPG (BA), 02/11/2026. **Nguồn:** [PR #213](https://github.com/callmehai/FU-FA26-G116/pull/213) (BE) và
[PR #215](https://github.com/callmehai/FU-FA26-G116/pull/215) (FE), mục "Deviations"; comment Jira của Lộc ngày 01/11/2026:
[SCRUM-59](https://sep490g116.atlassian.net/browse/SCRUM-59?focusedCommentId=10392),
[SCRUM-127](https://sep490g116.atlassian.net/browse/SCRUM-127?focusedCommentId=10393). Contract:
[09-phong-hop-truc-tuyen.md](../../03-api/09-phong-hop-truc-tuyen.md) mục **Khách (SCRUM-59)**. Note này gom việc BA, không thay
Jira hay đặc tả.

> **Tiến độ 03/11/2026: ĐÃ SỬA PHẦN UC/MSG** (Report 3 DOCX → snapshot + extract) **và ERD mục tiêu**
> (`13-erd/muc-tieu` + `13-erd/chen`: thêm `GuestSession`, `CallParticipant` dùng `GuestSessionId`; thêm `TranscriptSpeaker`)
> — xem §4.> Đã làm: UC-37 (PRE cho cả Khách, Khách xác nhận thông báo ghi âm + EX-9 MSG380 + EX-10 hết phiên, `retentionDays`, AF-3 hỏi lại
> mỗi 10 giây, POST-4 lượt vào/ra theo phiên, EX-1 màn kết thúc ≤20 giây), UC-38 EX-6, UC-41 EX-1, UC-28c POST-2, §V.2 thêm MSG380.
> Còn lại: ERD drawio, đặc tả màn hình, ghi âm Khách chờ SCRUM-129.

## 1. Trạng thái

- Stack #212 → #213 → #215, **cả ba đã merge vào `main`** (01/11/2026). Màn Khách và tài liệu: [note 23](23-pr212-scrum-121-122-123-man-khach.md).
- Mã mới: **MSG380** (400, Khách chưa xác nhận thông báo ghi âm). Danh mục đã có. MSG381 sau đó cấp cho khoá tên tự khai (PR #222, SCRUM-116); mã tiếp theo trên `main` là **MSG384**.
- Migration `GuestCallParticipants`: `call_participants.user_id` cho phép null, thêm `guest_session_id` (check đúng một trong hai);
  `call_sessions.started_by_id` cho phép null. ERD Markdown đã sửa trong PR; **ERD drawio chưa**.
- **Mâu thuẫn với PO chốt 01/11:** #213 điểm 8 và #215 ghi "không ghi âm theo micro cho Khách". PO đã chốt **có ghi**, Hải làm ở
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
| 9 | Số **180 ngày** trong thông báo ghi âm từng ghi cứng ở FE. **Đã sửa ở PR #222:** `GET …/call` trả `retentionDays`, màn chờ lấy thời hạn từ đó | 127 #1 | Chỉ ghi nhận |
| 10 | Màn chờ tự mở bằng **hỏi lại server mỗi 10 giây** và khi quay lại tab (hub không báo bắt đầu cuộc họp cho Khách) | 127 #2 | UC-37 AF Khách chờ phòng mở |
| 11 | Cuộc họp kết thúc: hiện MSG331 trước, rồi sang màn kết thúc trong tối đa 20 giây | 127 #3 | UC-42 (hoặc UC kết thúc phòng) phần Khách |
| 12 | **I2/I3 khác I1:** phiên hết hạn hoặc đăng xuất ở tab khác → Khách bị đưa ra (~20 giây), dữ liệu gỡ khỏi màn hình, nhưng lời mời còn hiệu lực nên về màn nhập mã MSG378, xác minh lại thì vào lại được. Thu hồi lời mời → màn kết thúc, không vào lại | 127 #4 | UC-04/UC-37: tách hai nhánh; sửa kỳ vọng I2/I3 trong ticket SCRUM-127 |
| 13 | Không cần mã mới cho màn kết thúc (MSG381 sau đó cấp cho khoá tên, PR #222) | 127 #5 | Chỉ ghi nhận |

## 4. Việc BA, theo thứ tự

- [x] UC-37 (vào phòng): PRE `InProgress` cho cả Khách; bước xác nhận thông báo ghi âm + EX-9 MSG380 và EX-10 hết phiên; AF-3 hỏi lại
      mỗi 10 giây cho Khách; POST-4 mỗi phiên Khách một kết nối, ghi IP/thời điểm vào-ra; EX-1 thêm màn kết thúc ≤20 giây (điểm 1, 3, 4, 5, 10, 11).
- [x] UC-38 EX-6 (hết phiên Khách trong phòng), UC-41 EX-1 (Khách không bao giờ điều phối), UC-28c POST-2 (thu hồi ngắt ngay,
      hết phiên/đăng xuất ≤30 giây). BR-181 đã có UC-37–UC-42 trong cột UC, không phải sửa (điểm 6, 12).
- [x] Report 3 §V.2: thêm MSG380 (dòng 380, Inline / Form error, UC-37 / EX-9) (điểm 7).
- [x] **ERD mục tiêu — ĐÃ SỬA ngày 03/11/2026** (điểm 2: `muc-tieu` + `chen`, không phải snapshot `hien-tai`):
      - `muc-tieu`: `CallParticipant` đổi `GuestInvitationId` → **`GuestSessionId`** (uuid?, FK); thêm thực thể
        **`GuestSession`** (Id, InvitationId, ExpiresAt, RevokedAt?) đặt dưới `CallParticipant`; quan hệ `r28` "guest" chuyển từ
        `MeetingGuestInvitation` sang `GuestSession` (0..1 → 0..*). `CallSession.StartedById` và `CallParticipant.UserId` đã là `uuid?`.
      - Thêm luôn **`TranscriptSpeaker`** (việc của note 25, điểm ERD): Id, TranscriptId, Label, SpeechMs, SuggestedUserId,
        SuggestionScore, AssignedUserId **hoặc** AssignedGuestId, AssignedById, AssignedAt — mô hình đích nay 32 thực thể.
      - `chen`: thêm hình chữ nhật **`GuestSession`** nối vào hình thoi `Joins as` (R28) thay cho `MeetingGuestInvitation`, và
        **`TranscriptSpeaker`** + hình thoi `Names` nối với `Transcript` (32 thực thể / 53 quan hệ).
      - `02-erd.md` đã đồng bộ: §8 có khối `GuestSession`, §7 có khối `TranscriptSpeaker`, bảng trạng thái có 2 dòng mới,
        30 → 32 thực thể (35 bảng kể cả ba bảng nối), bỏ câu "không thêm thực thể vào mô hình mục tiêu".
      - Đã xuất lại 2 ảnh ERD + nhúng ảnh ERD mới vào Report 3 §3.1; `validate.py` báo 0 lỗi, 0 lỗi qua đỉnh; đã mở ảnh kiểm tra.
      - Cập nhật số đếm ở `13-erd/README.md` và `chen/README.md`.
      - **`hien-tai` (snapshot schema) đã xoá ngày 03/11/2026** cùng nhánh này: bản sinh tự động, cũ 5 migration; đối chiếu schema
        dùng migration + `02-erd.md`.
      - **`02-erd.md` chưa đồng bộ**: dòng 7 vẫn nói `guest_sessions` không thêm thực thể vào mô hình đích, §8 lại vẽ `GuestSession`
        mà không định nghĩa — BE cần sửa §8 (định nghĩa thực thể, số thực thể 30 → 34) để khớp hình.
- [ ] Ghi rủi ro (điểm 5): bằng chứng thông báo ghi âm chỉ là cờ trong audit, không lưu câu đã hiện — **đã ghi trong UC-37 bước 5**.
      Điểm 9 (180 ngày ghi cứng) đã sửa ở #222; UC-37 giờ ghi "the number of days the server returns".
- [ ] Ghi âm Khách: **chờ SCRUM-129**, rồi mới sửa UC ghi âm (điểm 8).
- [ ] Đặc tả màn hình: màn chờ của Khách (ô xác nhận ghi âm), nhãn "Khách" trong phòng — chưa làm.
- [x] Sync Report 3, Change Log (dòng 03/11/2026 / M / KhanhPG, cùng lượt với note 23, 25).
