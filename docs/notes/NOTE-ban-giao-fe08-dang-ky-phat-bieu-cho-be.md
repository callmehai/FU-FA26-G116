# Bàn giao FE-08 Đăng ký phát biểu (giơ tay, mời phát biểu) — cho BE (SCRUM-27)

Ngày 06/11/2026, KhánhPG/BA. Ticket: **SCRUM-27** "Đăng ký phát biểu" (epic SCRUM-7 Trong họp), **To Do**, giao
Nguyễn Quang Huy. **Chưa có ticket con và chưa có ticket BE/FE riêng** — toàn bộ việc nằm trong SCRUM-27. Comment BA nhắn BE
đã có từ 29/10/2026 (Jira #10329 đặc tả, #10336 việc BE); note này thay thế #10336 bằng bản đầy đủ hơn và chỉ ra các chỗ
spec chưa khớp với code hiện có.

## 1. Đọc theo thứ tự

1. UC-79 Register to Speak: [05-use-case-specifications.md](../01-requirements/05-use-case-specifications.md) (tìm `### UC-79`),
   BR-337, BR-338, BR-339 trong [06-business-rules.md](../01-requirements/06-business-rules.md).
2. Sequence [UC79_RegisterToSpeak.txt](../06-sequencediagram/online-meeting-room/UC79_RegisterToSpeak.txt) — **rút gọn**: gộp
   `Invited → Speaking → Finished` thành một bước. **Đặc tả UC-79 mới là chuẩn**; BA sẽ sửa sequence ở lần Final Review sau.
3. Phòng họp hiện có: [09-phong-hop-truc-tuyen.md](../03-api/09-phong-hop-truc-tuyen.md) (danh tính LiveKit, Khách, `call_participants`),
   [CallEndpoints.cs](../../backend/TrustCabinet.Web/Endpoints/CallEndpoints.cs), [MeetingHub.cs](../../backend/TrustCabinet.Web/MeetingHub.cs).
4. ERD: [02-erd.md](../02-design/02-erd.md) (`SpeakingRequest`, dòng ~305), ADR-0011 (panel dùng chung màn chi tiết và phòng họp).

## 2. Hiện trạng code (đã kiểm trên `main` 06/11/2026)

| Có | Chưa có |
|---|---|
| Entity [SpeakingRequest.cs](../../backend/TrustCabinet.Domain/Entities/SpeakingRequest.cs), enum `SpeakingRequestStatus` (`Queued/Invited/Speaking/Finished/Cancelled`), bảng `speaking_requests`, index `(MeetingId, Status, QueuePosition)` | Endpoint, service, repository |
| Hub `/hubs/meetings` với nhóm theo người / Khách (`MeetingHub.Group`, `GuestGroup`) và mẫu publisher (`ParticipantsChangePublisher`, `AgendaChangePublisher.ReaderGroupsAsync`) | Sự kiện hub cho hàng đợi và "mời phát biểu" |
| `call_participants` biết ai đang ở trong phòng (lượt `left_at IS NULL`) | Dọn yêu cầu khi người rời phòng hoặc cuộc họp kết thúc |
| | Test, tài liệu `docs/03-api/` cho nhóm này |

## 3. Việc BE phải làm

### 3.1 Dữ liệu — cần migration

- **Khách không có `UserId`.** UC-79 cho **Guest** giơ tay (Primary Actors: Chair, Secretary, Participant, Guest), nhưng
  `SpeakingRequest.UserId` là `Guid` bắt buộc, FK tới `User`. Làm theo mẫu `SpeechClip` ("speaking as guest"): `UserId` cho phép
  null, thêm khoá tới `MeetingGuestInvitation`, ràng buộc CHECK đúng một trong hai. Migration mới, **không sửa migration cũ**
  (`dotnet dotnet-ef migrations add …`, commit cùng `ModelSnapshot`; xem AGENTS.md "database is code-first").
- **BR-337 (mỗi người một yêu cầu đang mở)** phải được database giữ, không chỉ code: unique index lọc theo trạng thái mở
  (`Queued`, `Invited`, `Speaking`), một cho người dùng, một cho Khách. Hai lần bấm gần nhau không được tạo hai dòng.
- `QueuePosition`: gán trong transaction (hai người giơ tay cùng lúc không trùng vị trí). Chủ toạ đổi thứ tự thì ghi lại vị trí.
- `AgendaItemId`: **server** tự lấy mục đang bàn (`InProgress`) lúc giơ tay, FE không gửi lên. Không có mục đang bàn thì để null.
- Cập nhật `docs/02-design/02-erd.md` (bảng, quan hệ, ràng buộc) cùng commit.

### 3.2 Endpoint (đề xuất — BE chốt tên, ghi lại ở ticket)

Ticket chỉ nêu `POST /api/meetings/{id}/speaking-requests`; các thao tác còn lại suy từ UC-79:

| Thao tác (UC-79) | Đề xuất | Ai được gọi |
|---|---|---|
| Giơ tay (bước 1–2) | `POST /api/meetings/{id}/speaking-requests` | Người dùng hoặc Khách đang ở trong phòng |
| Hạ tay (AF-1) | `DELETE …/speaking-requests/{requestId}` | Chính chủ yêu cầu |
| Xem hàng đợi (bước 3) | `GET …/speaking-requests` | Mọi người trong phòng (tay giơ hiện ở ô người và panel Người tham gia); cờ quyền Chủ toạ do server trả |
| Đổi thứ tự (AF-2) | `POST …/speaking-requests/{requestId}/move` `{ "direction": "up"\|"down" }` | Chủ toạ hiện hành |
| Từ chối (AF-3) | `POST …/{requestId}/decline` | Chủ toạ hiện hành |
| Mời phát biểu (bước 4–5) | `POST …/{requestId}/invite` | Chủ toạ hiện hành |
| Bắt đầu lượt (bước 6) | `POST …/{requestId}/start` | Chính người được mời |
| Kết thúc lượt (bước 7) | `POST …/{requestId}/finish` | Người đang phát biểu **hoặc** Chủ toạ |

Quy tắc phải do server giữ (đừng tin UI — AGENTS.md):

1. **Chỉ nhận khi cuộc họp `InProgress`** và người gọi **đang kết nối trong phòng** (UC-79 bước 2 "rechecks that the actor is connected") — dùng `call_participants` lượt mở.
2. **Chủ toạ hiện hành** là người có assignment `Chair` hiện hành. Sau UC-73 chuyển Chủ toạ, người cũ mất quyền ngay (EX-4); không cache vai trò.
3. **Một người nắm lượt tại một thời điểm** (BR-338): không mời khi còn yêu cầu `Invited` hoặc `Speaking`.
4. Mời **không bật/tắt mic** ai (BR-338, UC-79 mô tả): **không** gọi LiveKit để bật mic. Người được mời tự bật mic (UC-39a).
5. Giữ `StartedAt` (khi `start`) và `EndedAt` (khi `finish`) cùng cuộc họp sau khi họp xong (BR-339). Không có người nhận việc, hạn, tiến độ — đây không phải task.
6. **Người rời phòng / mất kết nối** → huỷ yêu cầu mở của họ, hàng đợi dồn lên (EX-3). Móc vào chỗ webhook LiveKit xử lý `participant_left` / `CallRoomJanitor`.
7. **Cuộc họp Ended/Cancelled** → mọi yêu cầu còn mở chuyển `Cancelled` (BR-337 "or the Meeting ends"). Lượt đang `Speaking` thì đóng `EndedAt`.
8. Thu hồi lời mời Khách / gỡ thành viên (đã đối soát phòng ở `09-phong-hop-truc-tuyen.md`) → huỷ yêu cầu mở của người đó.
9. Cuộc họp người gọi không đọc được → **404**, không lộ cuộc họp tồn tại. Đã đọc được nhưng không phải Chủ toạ gọi thao tác của Chủ toạ → từ chối (xem câu hỏi mở 2).

### 3.3 Realtime — `/hubs/meetings`

- Sau mỗi thay đổi đã commit (giơ, hạ, đổi thứ tự, từ chối, mời, bắt đầu, kết thúc): publisher mới theo mẫu `ParticipantsChangePublisher`
  (nhóm theo `ReaderGroupsAsync`, **gồm cả Khách có phiên hợp lệ**) phát `speakingChanged` làm **invalidation** (FE tải lại qua GET, quyền kiểm lại). Không nhét dữ liệu hàng đợi vào sự kiện.
- Sự kiện riêng **"mời phát biểu"** (`speakingInvited`) chỉ tới nhóm của người được mời, để phòng họp báo "đến lượt bạn, hãy bật micro" (ticket, mục bổ sung 30/09).
- Phát lỗi thì chỉ log, **không** huỷ giao dịch đã commit (đúng như các publisher hiện có).

### 3.4 Mã thông báo

Spec UC-79 chỉ dùng hai mã sẵn có: **MSG117** (409, cuộc họp không `InProgress`) và **MSG124** (502, máy chủ phòng không phản hồi).
MSG124 chỉ có nghĩa nếu BE thực sự gọi LiveKit; giơ tay thuần database thì không dùng.
Các tình huống BE sẽ gặp mà spec chưa gán mã: người gọi không còn trong phòng, mời khi đang có người nắm lượt, thao tác của Chủ toạ do người khác gọi,
yêu cầu không còn mở. Quy tắc: **một mã một tình huống**, dùng lại mã cũ chỉ khi cùng tình huống; còn lại lấy số kế tiếp ở mục "Mã tiếp theo" của
[danh mục](../03-api/00-danh-muc-ma-thong-bao.md) (lấy mã ở mục "Mã tiếp theo" của catalogue — hiện **MSG457**; `MSG435`–`MSG456` đã dành sẵn), thêm dòng vào bảng **cùng commit**,
khai báo hằng đầu file endpoint, và có test khẳng định mã (`MessageCodeCatalogueTests` bắt thiếu).

### 3.5 Test và tài liệu

- Test tích hợp trong `tests/TrustCabinet.Web.Tests` (thư mục theo tính năng, cạnh các test phòng họp): giơ tay hai lần chỉ một dòng (BR-337); đúng thứ tự hàng đợi; Thành viên gọi thẳng `invite`/`move`/`decline` bị từ chối (ticket yêu cầu rõ); mời khi đang có người nắm lượt; Chủ toạ cũ sau UC-73; cuộc họp không `InProgress` → MSG117; người không thuộc cuộc họp → 404; Khách giơ tay được; rời phòng huỷ yêu cầu; họp kết thúc huỷ yêu cầu; `StartedAt/EndedAt` được giữ.
- `docs/03-api/20-dang-ky-phat-bieu.md` (số tiếp theo sau `19-ghi-chu-cuoc-hop.md`): endpoint, quyền, body, mã, sự kiện hub. Ghi vào `docs/03-api/README.md` nếu thư mục có mục lục.
- Danh mục MSG, ERD: cùng commit với code.
- Lệch so với ticket/UC khi làm thì **ghi comment Jira SCRUM-27** (skill `jira-deviation-note`) để BA đồng bộ lúc Final Review.

## 4. Câu hỏi mở — cần PO/BA chốt hoặc BE đề xuất

1. **Khách giơ tay** (3.1): xác nhận làm cùng đợt (đổi schema) hay giai đoạn sau chỉ người dùng. UC-79 hiện ghi cả Guest.
2. **Mã khi người không phải Chủ toạ gọi thao tác của Chủ toạ.** Spec EX-4 chỉ nói "nút biến mất, không đổi gì" (phía UI). Với API: đề xuất `403` + mã mới. Theo bảng 403/404 trong AGENTS.md, đây là endpoint cố định nên `403` được.
3. **Người được mời không bắt đầu lượt:** spec không có hạn chờ. Đề xuất không làm tự động; Chủ toạ bấm **từ chối/kết thúc** để mời người khác. Nếu PO muốn hết hạn tự động thì phải thêm vào spec.
4. **`start` do ai bấm:** spec bước 6 là người được mời ("chooses Start speaking"). Nếu BE cho `invite` đi thẳng sang `Speaking` thì lệch spec — ghi vào Jira.
5. Chủ toạ **tự giơ tay** được không? Spec không cấm (Primary Actors có Chair). Đề xuất cho phép như mọi người.

## 5. Phần FE (sau khi BE có endpoint — chỉ để BE biết hợp đồng cần trả)

`SpeakingPanel` (`meetingId`, `variant: 'page' | 'room'`) dùng chung màn chi tiết và phòng họp; nút "Giơ tay" trên thanh điều khiển gọi hàm đăng ký/huỷ trong `src/api/`;
panel tự nghe `/hubs/meetings`; quyền Chủ toạ lấy từ server (ADR-0011 mục 2). GET hàng đợi nên trả sẵn: `id`, tên hiển thị, vai trò (kể cả nhãn Khách), `status`,
`queuePosition`, `requestedAt`, mục nghị sự, và cờ `canManage` cho người gọi.

## 6. BA còn lại sau khi BE xong

- Sửa sequence `UC79_RegisterToSpeak.txt` theo luồng thật (mời, bắt đầu, kết thúc là ba bước riêng) và tab draw.io; chưa có sơ đồ lớp FE-08.
- UC-79 / BR-337–339 chỉnh theo những chỗ BE lệch (mục 4) và mã MSG mới.
- Comment Jira SCRUM-27 và chuyển trạng thái theo [DEVELOPMENT_WORKFLOW §2.2](../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update).
