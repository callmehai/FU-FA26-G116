# Biểu quyết — thiết kế contract cho BE (FE-11 / SCRUM-96)

Ngày 28/10/2026; cập nhật 30/10/2026. **Backend SCRUM-28 đã triển khai** UC-52a, UC-52b, UC-53–UC-57
(xem, nháp, mở, bỏ phiếu, đóng, kết luận) và sự kiện realtime ở [§7](#7-đã-triển-khai--scrum-28). Gắn / gỡ tài
liệu (UC-76a/76b), xác nhận đã đọc (UC-74) và chặn `MSG252` **chưa** có — thuộc SCRUM-96.

Nguồn đã đối chiếu: UC-52a/52b, UC-53–UC-57, UC-74, UC-76a/76b; BR-159/160/165/181/189/190/252–274/
285/287–293/304/305; ADR-0014/0016/0017/0019/0020; sequence
[voting-management](../06-sequencediagram/voting-management/README.md) và UC-74; class diagram
[FE-11](../07-classdiagram/feature-11-voting-management-class-diagram.drawio); schema
[ERD](../02-design/02-erd.md); danh mục [MSG205–236, MSG252–255, MSG265–270](00-danh-muc-ma-thong-bao.md).

## 1. Phạm vi và ranh giới

| Trong phạm vi | Ngoài phạm vi |
|---|---|
| CRUD nháp, mở/đóng, bỏ phiếu, ghi kết luận | Quorum, biểu quyết trước/sau họp, Resolution |
| Gắn/gỡ tài liệu Vote khi `Draft` (UC-76) | Nhóm “Biểu quyết” trên tab tài liệu cuộc họp (UI UC-32a AF-1) — cùng dữ liệu `VoteDocument` |
| Xác nhận đã đọc tài liệu Vote (UC-74) | Ghi âm / transcript / biên bản |
| Guest bỏ phiếu qua invitation (ADR-0016) | Capability hệ thống — quyền theo vai trò cuộc họp |

- Prefix: `/api/meetings/{meetingId}/votes`.
- Auth: JWT user active, hoặc Guest session đã xác minh (cùng adapter UC-04 như điểm danh/agenda).
- Ẩn tài nguyên: meeting/vote không được đọc → `404` cùng body (không `403` lộ tồn tại).
- Mutation khóa Meeting (như agenda); kiểm lại quyền/trạng thái trong transaction trước khi ghi.
- Tally: đọc từ bộ đếm `VotingOption.BallotCount`, tăng trong cùng transaction với receipt (SCRUM-28, review PR #200) — xem §7.
- Realtime: `voteChanged` trên `/hubs/meetings` sau commit (cùng nhóm JoinMeeting); FE GET lại — xem §7.

## 2. Quyền theo trạng thái

| Thao tác | Actor | Meeting | Vote |
|---|---|---|---|
| GET danh sách / chi tiết | Ai đọc được Meeting; Draft chỉ Chair/Secretary hiện hành (BR-253) | Approved+ cho chuẩn bị; mọi trạng thái có quyền xem Meeting cho Open/Closed | — |
| Tạo / sửa / xoá nháp; gắn / gỡ tài liệu | Chair **hoặc** Secretary hiện hành | Approved, Published, InProgress | Draft |
| Mở phiếu | Chỉ Chair hiện hành | InProgress | Draft; không có Vote Open khác |
| Bỏ phiếu | Người trong `VoteEligibleVoter` + còn quyền Meeting | InProgress | Open |
| Đóng phiếu | Chỉ Chair hiện hành | InProgress hoặc Ended | Open |
| Ghi kết luận | Chỉ Chair hiện hành | InProgress hoặc Ended | Closed, chưa có conclusion |
| Xác nhận đã đọc (UC-74) | Ai xem được Meeting và tài liệu còn gắn Vote | — | Tài liệu còn là Vote document (Open hoặc Draft đều cho xác nhận khi còn gắn) |

## 3. Endpoint

| Method | Path | UC | Thành công |
|---|---|---|---|
| GET | `/api/meetings/{meetingId}/votes` | UC-52a | 200 snapshot danh sách |
| GET | `/api/meetings/{meetingId}/votes/{voteId}` | UC-52b | 200 chi tiết theo trạng thái |
| POST | `/api/meetings/{meetingId}/votes` | UC-53 | 201/MSG210 |
| PUT | `/api/meetings/{meetingId}/votes/{voteId}` | UC-53 AF-1 | 200/MSG211 |
| DELETE | `/api/meetings/{meetingId}/votes/{voteId}` | UC-53 AF-2 | 200/MSG212 |
| POST | `/api/meetings/{meetingId}/votes/{voteId}/open` | UC-54 | 200/MSG217 |
| POST | `/api/meetings/{meetingId}/votes` + `"openImmediately": true` | UC-53 AF-3 → UC-54 | 200/MSG217 (không tạo Draft nếu mở lỗi) |
| PUT | `/api/meetings/{meetingId}/votes/{voteId}` + `"openImmediately": true` | UC-53 AF-3 → UC-54 AF-1 | 200/MSG217 (mở lỗi thì nháp giữ nguyên, không lưu phần sửa) |
| POST | `/api/meetings/{meetingId}/votes/{voteId}/ballots` | UC-55 | 200/MSG227 |
| POST | `/api/meetings/{meetingId}/votes/{voteId}/close` | UC-56 | 200/MSG229 + tally |
| POST | `/api/meetings/{meetingId}/votes/{voteId}/conclusion` | UC-57 | 200/MSG235 |
| POST | `/api/meetings/{meetingId}/votes/{voteId}/documents` | UC-76a | 200/MSG265 |
| POST | `/api/meetings/{meetingId}/votes/{voteId}/documents/from-library` | UC-76a AF | 200/MSG265 |
| POST | `/api/meetings/{meetingId}/votes/{voteId}/documents/upload` | UC-76a AF | 200/MSG265 (multipart) |
| DELETE | `/api/meetings/{meetingId}/votes/{voteId}/documents/{meetingDocumentId}` | UC-76b | 200/MSG268 |
| POST | `/api/meetings/{meetingId}/documents/{meetingDocumentId}/read-confirmation` | UC-74 | 200/MSG253 |

Ba nguồn gắn tài liệu tách endpoint giống SCRUM-112 (agenda), không gộp một body `source` đa hình
trừ khi BE và FE thống nhất lại trong cùng PR.

## 4. Body và snapshot

### Tạo / sửa nháp / mở ngay

```json
{
  "title": "Thông qua kế hoạch Q4",
  "description": "",
  "options": ["Đồng ý", "Không đồng ý", "Bỏ phiếu trắng"],
  "isAnonymous": false,
  "eligibleRoles": ["Chair", "Secretary", "Member", "Guest"],
  "openImmediately": false
}
```

- `title` trim 1–300; `description` mặc định `""`, tối đa 4000.
- `options`: ≥ 2 chuỗi khác nhau sau trim, mỗi chuỗi 1–200; không nhận `id` từ client khi tạo.
- `isAnonymous` bắt buộc bool; `eligibleRoles` ≥ 1 giá trị trong `Chair|Secretary|Member|Guest`.
- PUT thay toàn bộ trường cấu hình; chỉ khi Vote còn `Draft`. PUT cũng nhận `openImmediately` (mở từ form sửa).
- `openImmediately: true`: chỉ Chair + Meeting `InProgress`; thành công → status `Open`, không
  để lại Draft lỗi; thất bại giữ form phía FE, DB không đổi nháp cũ (UC-53 AF-3).

### Bỏ phiếu

```json
{ "optionId": "<guid>" }
```

Một lựa chọn; không đổi/rút sau khi ghi (BR-265). Mọi phiếu ghi `VoteReceipt` có voter và tăng bộ đếm của phương án.
Phiếu công khai thêm một dòng `VoteChoice` ghi người chọn; phiếu kín **không ghi `VoteChoice`** (§7).

### Kết luận

```json
{ "conclusion": "Passed", "note": "" }
```

`conclusion` ∈ `Passed|NotPassed|Deferred` (đúng hoa thường); `note` tùy chọn, trim, tối đa 2000 — doc,
validation và cột `conclusion_note` cùng một số từ SCRUM-28. Ghi một lần, không sửa (BR-262). Hệ thống không
suy từ tally (BR-263).

### Gắn từ tài liệu cuộc họp / kho

```json
{ "meetingDocumentIds": ["…"] }
```

```json
{ "documentIds": ["…"] }
```

Upload: multipart, cùng giới hạn loại/dung lượng tài liệu cuộc họp (BR-187, BR-214, BR-215, MSG288
nếu áp dụng cùng lần). Tất cả hoặc không (BR-304). Gỡ Vote không gỡ MeetingDocument (BR-305).

### Xác nhận đã đọc

```json
{ "lastPageReached": true }
```

Client chỉ gửi khi đã cuộn tới trang cuối trong viewer (BR-290). Server từ chối nếu
`lastPageReached !== true` → 400/MSG254. Xác nhận theo (actor, MeetingDocument); dùng chung mọi
Vote cùng tài liệu (BR-291); không rút; không liên kết với `VoteChoice` (BR-293).

### Snapshot danh sách (GET)

```json
{
  "meetingId": "<guid>",
  "meetingStatus": "InProgress",
  "serverTime": "2026-10-28T10:00:00+00:00",
  "permissions": {
    "canManageDraft": true,
    "canOpen": true,
    "canClose": false,
    "canRecordConclusion": false
  },
  "votes": [{
    "id": "<guid>",
    "title": "…",
    "status": "Open",
    "isAnonymous": false,
    "eligibleRoles": ["Chair", "Secretary", "Member", "Guest"],
    "openedAt": "…",
    "closedAt": null,
    "eligibleCount": 12,
    "receiptCount": 3,
    "conclusion": null,
    "documentCount": 2,
    "actions": {
      "canEdit": false,
      "canDelete": false,
      "canAttachDocuments": false,
      "canOpen": false,
      "canCast": true,
      "canClose": true,
      "canRecordConclusion": false,
      "canViewResult": false
    }
  }]
}
```

- Lọc Draft khỏi danh sách nếu actor không phải Chair/Secretary hiện hành; đếm/`votes` chỉ phần
  nhìn thấy (BR-253). Danh sách rỗng vẫn 200; FE có thể hiện MSG207.
- Chi tiết Open: turnout `receiptCount`/`eligibleCount`, **không** lộ lựa chọn cá nhân khi còn Open.
- Chi tiết Closed: tally theo option; phiếu công khai có thể kèm lựa chọn từng người / “chưa bỏ”;
  phiếu kín chỉ tổng.

## 5. Mã lỗi (trích)

| Tình huống | HTTP | MSG |
|---|---|---|
| Không truy cập Meeting/Vote / Draft bị ẩn | 404 | MSG205 |
| Không tải danh sách / chi tiết | 500/503 | MSG206 / MSG208 |
| Body cấu hình sai | 400 | MSG209 |
| Không còn quyền quản lý Draft | 409 | MSG213 |
| Lỗi lưu tạo/sửa/xoá nháp | 503 | MSG214–216 |
| Không được mở / đã có Open / không ai đủ điều kiện | 409 | MSG218–220 |
| Lỗi mở | 503 | MSG221 |
| Chưa chọn / option sai / không đủ điều kiện / hết nhận phiếu / đã bỏ | 400/409 | MSG222–226 |
| Chưa xác nhận đọc hết tài liệu Vote | 409 | MSG252 |
| Lỗi ghi phiếu | 503 | MSG228 |
| Không được đóng / lỗi đóng | 409/503 | MSG230–231 |
| Kết luận sai / đã ghi / không được ghi | 400/409 | MSG232–234 |
| Lỗi ghi kết luận | 503 | MSG236 |
| Gắn trùng / lỗi gắn / gỡ không còn / lỗi gỡ | 409/503 | MSG266–270 |
| Tài liệu cuộc họp không truy cập (UC-74) | 404 | MSG156 |
| Chưa tới trang cuối / lỗi xác nhận đọc | 400/503 | MSG254 / MSG255 |

Câu tiếng Việt khi triển khai lấy từ catalogue (cột thông báo chuẩn hiện là tiếng Anh dành sẵn —
BE/BA đổi sang tiếng Việt cùng PR code, không đổi mã).

## 6. Schema BE (SCRUM-96)

Entity và migration cho mô hình này **không nằm trong PR tài liệu**: chúng ở PR code SCRUM-96, với
migration tạo trên `main` mới nhất. Cần có: `VotingSession`, `VotingOption`, `VoteReceipt`, `VoteChoice`,
`VoteEligibleVoter`, `VoteDocument` (xem [ERD](../02-design/02-erd.md)). `VotingSession.Title` tối đa 300 ký tự.

Còn thiếu bảng **`document_read_confirmations`** (UC-74 / BR-290–293): xem ERD mục tiêu; thêm
cùng PR code với endpoint xác nhận đọc.

## 7. Đã triển khai — SCRUM-28

Code: `backend/TrustCabinet.Web/Endpoints/VoteEndpoints.cs`, `backend/TrustCabinet.Infrastructure/Votes/`,
`backend/TrustCabinet.Domain/Votes/`; test `tests/TrustCabinet.Web.Tests/Votes/`. HTTP của từng mã ở
[catalogue](00-danh-muc-ma-thong-bao.md#biểu-quyết--contract-backend-scrum-28).

### Khác hoặc chốt thêm so với các mục trên

- **Đường dẫn** theo §3 (`/api/meetings/{meetingId}/votes/{voteId}/…`), không phải `/api/votes/{id}/…` như mô tả
  ticket SCRUM-28.
- **Thứ tự kiểm tra khi bỏ phiếu** theo sequence UC-55: không đọc được → `404/MSG205`; không trong danh sách chốt →
  `409/MSG224`; không Open hoặc cuộc họp không InProgress → `409/MSG225`; đã bỏ → `409/MSG226`; rồi mới tới
  `400/MSG222` (thiếu `optionId`) và `400/MSG223`.
- **Guest đủ điều kiện** = lời mời chưa thu hồi lúc mở phiếu. "Email đã xác minh" là việc của UC-04, chưa có ở Backend;
  khi bỏ phiếu, Guest vẫn phải có phiên còn hiệu lực (adapter `IParticipantGuestAccess`). Chờ PO chốt có lọc theo phiên
  Khách đã xác minh lúc mở phiếu không, sau khi PR #203 (UC-04) merge.
- **Guest chỉ xem biểu quyết khi cuộc họp Published hoặc InProgress**; cuộc họp kết thúc thì Guest nhận `404/MSG205`
  (PO 30/10/2026, cùng luật `ParticipantRules.GuestCanView` của PR #203). Người nội bộ vẫn xem được sau khi kết thúc.
- **Người dùng đủ điều kiện** = phân công hiện hành, tài khoản còn hoạt động, giữ vai trò đã chọn và đọc được cuộc họp.
  Điểm danh không liên quan (BR-190).
- **Phương án trùng** so sánh sau trim, không phân biệt hoa thường (`Đồng ý` và `đồng ý` là trùng).
- **Mở từ form** (`openImmediately: true`) dùng được ở cả POST (tạo) lẫn PUT (sửa nháp), trả `200/MSG217`, không phải `201`.
  Mở lỗi thì không lưu gì: POST không để lại nháp, PUT giữ nháp như trước khi sửa (UC-54 AF-1, EX-5). Thư ký gửi kèm cờ
  này nhận `409/MSG218`. Thành viên gọi POST nhận `409/MSG213` (nhánh EX-2 đi trước AF-3 trong sequence UC-53); gọi PUT
  thì nhận `404/MSG205`, vì nháp bị ẩn với Thành viên như ở mọi thao tác trên nháp có sẵn (BR-253).
- **Thứ tự kiểm tra khi mở từ form** theo sequence UC-54: `MSG218` → `MSG219` → `MSG209` → `MSG220`.
- **Đóng phiếu** trả kèm `result` (cùng dạng `result` của chi tiết).
- **Tài khoản bị vô hiệu hoá** nhận `403` body rỗng, như điểm danh.

### Chi tiết (GET `/votes/{voteId}`)

Ngoài các trường của dòng danh sách: `description`, `options[] { id, position, label }`, `isEligible`, `hasVoted`
(cả hai theo người gọi), `result` (null khi chưa Closed):

```json
{
  "ballotCount": 2,
  "options": [{ "optionId": "<guid>", "label": "Đồng ý", "count": 1, "percentage": 50 }],
  "voters": [{ "id": "<userId | invitationId>", "kind": "Internal", "displayName": "…", "optionId": null }]
}
```

`percentage` làm tròn 2 chữ số, null khi chưa ai bỏ (BR-261). `voters` chỉ có ở phiếu công khai — `optionId: null`
là chưa bỏ (BR-273); phiếu kín luôn `voters: null` (BR-259). Dòng danh sách có thêm `hasVoted`.

### Realtime

Sự kiện `voteChanged` `{ eventId, meetingId, voteId, changeType, title, occurredAt }`, `changeType` ∈
`DraftCreated | DraftUpdated | DraftDeleted | Opened | BallotCast | Closed | ConclusionRecorded`.

- Ba sự kiện `Draft*` chỉ gửi Chair/Secretary hiện hành (BR-253); còn lại gửi mọi người đang đọc được cuộc họp, kể cả
  Guest.
- `title` chỉ có ở `Opened`, cho thông báo nổi "Chủ toạ mở biểu quyết: {nội dung}". `BallotCast` không có gì về người
  bỏ phiếu.
- Thao tác bị từ chối hoặc lỗi không phát gì.

### Phiếu kín ở tầng dữ liệu

Phiếu kín **không ghi dòng nào vào `vote_choices`** (migration `SecretBallotsCountedOnOptions`). Lý do: review PR #200
cho thấy dù choice không có người, không có thời điểm và Id ngẫu nhiên, một câu `SELECT` thường (không `ORDER BY`)
vẫn trả các choice đúng theo thứ tự ghi, khớp từng dòng với `vote_receipts` sắp theo `cast_at`.

- Mỗi phiếu (kín hay công khai) tăng `voting_options.ballot_count` trong cùng transaction với receipt. Câu `UPDATE`
  ghi lại **mọi** phương án của phiên (cộng 1 cho phương án được chọn, 0 cho phần còn lại) và không đổi `updated_at`,
  nên `xmin` và timestamp của các dòng phương án không cho biết dòng nào vừa tăng. Kiểm phiếu đọc bộ đếm này.
- `vote_choices` chỉ còn phiếu công khai, mỗi dòng ghi người chọn; ràng buộc `ck_vote_choices_one_voter`
  (`num_nonnulls(voter_id, guest_invitation_id) = 1`) chặn ở DB mọi choice không tên. `cast_at` đã bỏ khỏi
  `vote_choices`; thời điểm bỏ phiếu chỉ ở receipt.
- Test `A_secret_ballot_leaves_nothing_that_pairs_a_choice_with_its_voter` dựng lại phép thử của review (3 người, 3
  phương án khác nhau): không có choice nào, bộ đếm đúng, mọi dòng phương án cùng một `xmin` và `updated_at` không đổi.
- Còn lại: người theo dõi DB **liên tục** trong lúc phiếu mở có thể so hai lần đọc để thấy bộ đếm nào vừa tăng khi một
  receipt mới xuất hiện. Một ảnh chụp DB tại một thời điểm thì không ghép được. Xem
  [ERD](../02-design/02-erd.md#biểu-quyết-ẩn-danh--đọc-kỹ-chỗ-này).

### Khoá và đồng thời

Mọi thao tác ghi khoá `meetings` trước (bỏ phiếu: `FOR SHARE`, còn lại `FOR UPDATE`), rồi tài khoản người gọi
(`FOR SHARE`), rồi phiên biểu quyết. Hai lệnh mở cùng lúc: một `MSG217`, một `MSG219`. Hai phiếu cùng người cùng lúc:
một `MSG227`, một `MSG226` — unique index của `vote_receipts` giữ phiếu đầu.
