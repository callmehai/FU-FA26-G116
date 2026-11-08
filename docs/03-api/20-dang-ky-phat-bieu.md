# Đăng ký phát biểu — SCRUM-27

UC-79 *Register to Speak*, BR-337 → BR-339, theo [note bàn giao](../notes/NOTE-ban-giao-fe08-dang-ky-phat-bieu-cho-be.md).
Người đang ở trong phòng họp trực tuyến giơ tay. Chủ toạ xem hàng đợi, đổi thứ tự, từ chối hoặc mời từng người. Người được mời
tự bật micro (UC-39a), bấm bắt đầu rồi kết thúc lượt. Giờ mời, bắt đầu và kết thúc được giữ cùng cuộc họp để biên bản biết ai
nói, về mục nào, bao lâu (BR-339). Đây không phải việc cần làm: không có người nhận, hạn hay tiến độ.

Mã thông báo: `MSG117` (dùng lại), `MSG457`–`MSG462` ([danh mục](00-danh-muc-ma-thong-bao.md)). Không dùng `MSG124`:
không thao tác nào gọi LiveKit, và mời phát biểu **không** bật hay tắt micro của ai (BR-338).

## Quyền

| Ai | Xem hàng đợi | Giơ tay, hạ tay, bắt đầu, kết thúc lượt của mình | Mời, từ chối, đổi thứ tự, kết thúc lượt của người khác |
|---|---|---|---|
| Chủ toạ hiện hành | Có, `canManage: true` | Có, khi đang ở trong phòng | Có, từ phòng hoặc màn chi tiết |
| Thư ký, Thành viên, Khách có phiên hợp lệ | Có | Có, khi đang ở trong phòng | `403` `MSG457` |
| Người khác đọc được cuộc họp (người tạo, người duyệt) | Có | `409` `MSG458` (không ở trong phòng) | `403` `MSG457` |
| Không đọc được cuộc họp | `404` | `404` | `404` |

- **Chủ toạ hiện hành:** thành viên đang hoạt động có vai trò `Chair`, đọc lại ở mọi request. Sau khi bàn giao (UC-73) người
  cũ mất quyền ngay ở request kế tiếp (EX-4).
- **Đang ở trong phòng:** có lượt kết nối đang mở (`call_participants.left_at IS NULL`) trong phiên phòng `Active` của cuộc
  họp, theo LiveKit báo về. Lượt chỉ được ghi khi LiveKit báo `participant_joined` (hoặc lần đối soát 30 giây), **không** ghi
  lúc `POST …/call/join`. Vì vậy FE chỉ bật nút "Giơ tay" sau khi LiveKit báo đã kết nối; vài giây đầu vẫn có thể nhận `MSG458`.
- Mọi thao tác ghi cần cuộc họp `InProgress` (`409` `MSG117`, EX-1). Khách nhận `404` khi cuộc họp không còn mở cho Khách,
  như ở biểu quyết và chương trình nghị sự.
- Thao tác trên yêu cầu của người khác mà chỉ chủ yêu cầu được làm (hạ tay, bắt đầu): `404`, như yêu cầu không tồn tại,
  ở mọi trạng thái của cuộc họp.

## Endpoint

Không route nào có body. Thành công trả `SpeakingQueue` (không mã); giơ tay lần đầu là `201`, các thao tác còn lại `200`.

| Method | Đường dẫn | Ai | Trạng thái yêu cầu |
|---|---|---|---|
| `GET` | `/api/meetings/{id}/speaking-requests` | người đọc được cuộc họp | — |
| `POST` | `/api/meetings/{id}/speaking-requests` | người trong phòng | tạo `Queued`. Đã có yêu cầu mở thì `200`, giữ nguyên (EX-2, BR-337) |
| `DELETE` | `…/speaking-requests/{requestId}` | chủ yêu cầu, trong phòng | `Queued`/`Invited` → `Cancelled` (AF-1) |
| `POST` | `…/{requestId}/move-up`, `…/{requestId}/move-down` | Chủ toạ | đổi chỗ với yêu cầu `Queued` liền trên/dưới (AF-2). Đã ở đầu/cuối thì không đổi gì, vẫn `200` |
| `POST` | `…/{requestId}/decline` | Chủ toạ | `Queued`/`Invited` → `Cancelled` (AF-3) |
| `POST` | `…/{requestId}/invite` | Chủ toạ | `Queued` → `Invited` (bước 4–5). Đang có người `Invited`/`Speaking` → `409` `MSG459` |
| `POST` | `…/{requestId}/start` | người được mời, trong phòng | `Invited` → `Speaking`, ghi `startedAt` (bước 6) |
| `POST` | `…/{requestId}/finish` | người đó (trong phòng) hoặc Chủ toạ (không cần ở trong phòng, kể cả với lượt của chính mình) | `Speaking` → `Finished`, `Invited` → `Cancelled`; ghi `endedAt` (bước 7) |

```jsonc
// SpeakingQueue
{
  "meetingId": "…",
  "meetingStatus": "InProgress",
  "canManage": false,              // người gọi là Chủ toạ hiện hành: hiện nút mời, từ chối, đổi thứ tự
  "myRequestId": "…",              // yêu cầu đang mở của người gọi, null nếu chưa giơ tay
  "requests": [                    // chỉ yêu cầu đang mở: người được mời/đang nói trước, rồi hàng đợi theo thứ tự Chủ toạ
    {
      "id": "…",
      "kind": "User",              // "User" | "Guest"
      "displayName": "Nguyễn Văn A", // Khách: tên tự khai, không có thì tên trong lời mời
      "role": "Member",            // vai trò trong cuộc họp; Khách là "Guest"
      "status": "Queued",          // "Queued" | "Invited" | "Speaking"
      "queuePosition": 3,
      "requestedAt": "ISO", "invitedAt": null, "startedAt": null,
      "agendaItemId": "…", "agendaItemTitle": "…", // mục đang bàn lúc giơ tay, do server lấy; null nếu không có
      "isMine": false
    }
  ]
}
```

Lỗi: `{ "code": "MSGnnn", "message": "…" }` theo bảng mã ở danh mục — `MSG457` (403), `MSG458`, `MSG459`, `MSG460`, `MSG117`
(409), `MSG461` (500, đọc), `MSG462` (503, lưu). Tài khoản bị khoá: `403` không body.

## Tự đóng yêu cầu (BR-337)

Server tự đóng yêu cầu đang mở, cùng giao dịch với thay đổi gây ra nó. Lượt đang `Speaking` thành `Finished` có `endedAt`,
còn lại thành `Cancelled`. `endedAt` là lúc người đó rời phòng hoặc phòng đóng theo LiveKit, không phải lúc server xử lý
(lần đối soát có thể trễ tới 30 giây). Khách vào phòng từ nhiều thiết bị (nhiều phiên của cùng lời mời) chỉ mất tay giơ khi
thiết bị cuối cùng rời phòng:

| Khi | Đóng yêu cầu của |
|---|---|
| Chủ toạ kết thúc cuộc họp | mọi người |
| Người đó rời phòng hoặc mất kết nối (webhook `participant_left` / `participant_connection_aborted`, lần đối soát) | người đó (EX-3) |
| Phòng đóng (`room_finished`, hoặc phiên phòng kết thúc) | mọi người |
| Người đó bị gỡ khỏi cuộc họp (không còn vai trò nào) hoặc lời mời Khách bị thu hồi | người đó |

Rời phòng và đóng phòng khoá dòng cuộc họp trước, như giơ tay: một lần giơ tay trùng lúc với lúc rời phòng hoặc bị đóng lại
ngay, hoặc bị từ chối vì không còn ở trong phòng (`MSG458`); không để lại tay giơ treo.

## Realtime — `/hubs/meetings`

- `speakingChanged` `{ eventId, meetingId, changeType, requestId, occurredAt }` tới mọi người đang đọc được cuộc họp, kể cả
  Khách. Chỉ để báo "tải lại": FE gọi lại `GET`, quyền được kiểm lại. Thao tác không đổi gì (giơ tay khi tay đã giơ, đổi chỗ
  ở đầu/cuối hàng đợi) không phát sự kiện. `changeType` là tên thao tác (`Raise`, `Lower`,
  `MoveUp`, `MoveDown`, `Decline`, `Invite`, `Start`, `Finish`) hoặc `Left` khi có người rời phòng hoặc phòng đóng.
- `speakingInvited` (cùng nội dung) chỉ tới người được mời, để phòng họp báo "Đến lượt bạn — hãy bật micro".
- Kết thúc cuộc họp không phát `speakingChanged`: FE tải lại khi nhận `agendaChanged` loại `MeetingEnded`. Gỡ người khỏi
  cuộc họp đi kèm `participantsChanged`.

## Ghi chú cho BA

- Lệch đặc tả, đã ghi lên SCRUM-27: Chủ toạ thao tác được từ màn chi tiết mà không cần ở trong phòng; người khác gọi thao tác
  của Chủ toạ trả `403` `MSG457`; không có hạn chờ tự động cho người được mời; không dùng `MSG124`.
- Sequence `UC79_RegisterToSpeak` gộp `Invited → Speaking → Finished` thành một bước; theo code thì đây là ba request riêng.
