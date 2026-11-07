# Ghi chú cuộc họp của Thư ký — SCRUM-30

UC-82 *Take Meeting Notes*, theo [đặc tả sơ bộ](../notes/NOTE-dac-ta-so-bo-scrum-30.md). Thư ký gõ ghi chú trong lúc
họp. Mỗi ghi chú gắn với một mục nghị sự hoặc với cả cuộc họp. Bản nháp biên bản đọc lại các ghi chú này.

Mã thông báo: `MSG415`–`MSG423` ([danh mục](00-danh-muc-ma-thong-bao.md)). `MSG423` do FE tự hiện.
FE: [`NotesPanel`](../../frontend/src/features/notes/NotesPanel.tsx), kiểu [`types/note.ts`](../../frontend/src/types/note.ts), gọi API ở [`api/notes.ts`](../../frontend/src/api/notes.ts).

## Quyền

| Ai | Đọc | Ghi |
|---|---|---|
| Thư ký hiện tại | Có | Có, khi cuộc họp còn nhận ghi chú |
| Chủ toạ hiện tại | Có, chỉ đọc (`canWrite: false`) | `403` `MSG416` |
| Người khác đọc được cuộc họp (thành viên, người tạo, người duyệt, Thư ký đã bị thay) | `404`, như thể không có ghi chú (EX-8) | `404` (EX-8) |
| Không đọc được cuộc họp | `404` | `404` |

- **Thư ký hiện tại:** thành viên đang hoạt động, có vai trò `Secretary`. Nếu đổi Thư ký, người mới bắt đầu với ghi chú
  trống. Ghi chú của người cũ vẫn còn trong database nhưng không hiện và không vào biên bản.
- **Ghi chú được ghi khi:** cuộc họp `InProgress`, hoặc `Ended` mà chưa tạo bản nháp biên bản (BR-396, cùng mốc khoá
  với kết luận mục nghị sự).
- Server kiểm tra lại quyền ở mọi request.

## Endpoint

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `GET` | `/api/meetings/{id}/notes` | `200` `NotesState` |
| `PUT` | `/api/meetings/{id}/notes` | `200` `MSG415` + `{ note }`. Body `{ agendaItemId, body, updatedAt }` |

```jsonc
// NotesState
{
  "notes": [                                   // ghi chú của Thư ký hiện tại: ghi chú chung trước, rồi theo thứ tự chương trình
    { "agendaItemId": null, "body": "…", "updatedAt": "ISO", "author": { "id": "…", "fullName": "…" } },
    { "agendaItemId": "…", "body": "Ý kiến: …\nKết luận: …", "updatedAt": "ISO", "author": { … } }
  ],
  "currentAgendaItemId": "…",                  // mục Chủ toạ đang để InProgress, null nếu không có (BR-397)
  "canWrite": true,                            // người gọi là Thư ký và cuộc họp còn nhận ghi chú
  "secret": false                              // cuộc họp Mật: FE không giữ nháp trong localStorage (BR-403)
}

// PUT body
{ "agendaItemId": "…" | null, "body": "…", "updatedAt": "ISO" | null }
```

- **`agendaItemId: null`** là ghi chú chung của cả cuộc họp. Mỗi Thư ký có tối đa một ghi chú cho mỗi mục và một ghi
  chú chung (BR-398).
- **`updatedAt`** là giá trị server trả về lần đọc hoặc lần lưu trước. Với ghi chú chưa từng lưu thì gửi `null`.
  Server trả `409` `MSG418` và không ghi đè khi:
  - ghi chú đã được lưu ở cửa sổ khác sau lần đọc đó;
  - gửi `null` cho ghi chú đã tồn tại;
  - gửi một thời điểm cho ghi chú chưa tồn tại.

  Hai nơi cùng mở (khung trong phòng họp và tab mới) vì thế không ghi đè nhau (BR-399).
- **`body`:** văn bản thuần, tối đa 20.000 ký tự, được để trống (BR-400). `\r\n` được lưu thành `\n`.
- **Dấu đầu dòng (BR-401):** một dòng bắt đầu bằng `Ý kiến:`, `Kết luận:` hoặc `Nhiệm vụ:` (không phân biệt hoa thường)
  là dòng có đánh dấu. Dấu chỉ là chữ: TrustCabinet không lưu người được giao, hạn hay tiến độ.
- **Lỗi:**
  - `403` `MSG416` khi người gọi là Chủ toạ hiện tại (EX-1). Người không phải Chủ toạ hay Thư ký nhận `404` (EX-8).
  - Thứ tự kiểm tra theo UC-82 bước 4: vai trò, trạng thái cuộc họp, mục nghị sự, độ dài, rồi bản đang giữ.
  - `409` `MSG417` khi cuộc họp chưa bắt đầu, đã huỷ, hoặc đã kết thúc và đã có bản nháp biên bản.
  - `409` `MSG418` khi bản đang giữ đã cũ.
  - `400` `MSG419` khi quá 20.000 ký tự.
  - `409` `MSG420` khi mục nghị sự không thuộc cuộc họp này hoặc đã bị xoá.
  - `500` `MSG421` khi lỗi đọc, `503` `MSG422` khi lỗi lưu.

## Sự kiện `notesChanged` trên `/hubs/meetings`

Sau mỗi lần lưu, server gửi `notesChanged { eventId, meetingId, agendaItemId, updatedAt }` tới các kết nối đã
`JoinMeeting` của Chủ toạ và Thư ký hiện tại. Người khác không nhận được.

Sự kiện không chứa nội dung. Cửa sổ nhận được thì gọi lại `GET`; nếu đang có chữ chưa lưu ở đúng ghi chú đó thì giữ
chữ và báo `MSG418`, không tự ghi đè. Gửi sự kiện thất bại không làm hỏng lần lưu: cửa sổ kia sẽ nhận `MSG418` ở lần lưu
sau.

## Xoá mục nghị sự đã có ghi chú

Chỉ mục `Pending` mới xoá được (UC-23c). Khi xoá, ghi chú của mục đó được nối vào cuối ghi chú chung của cùng người
viết, dưới dòng `<tên mục>:`, rồi ghi chú của mục bị xoá. Nếu chưa có ghi chú chung thì tạo mới. Phần vượt 20.000 ký tự
bị cắt. Ghi chú chung đổi `updatedAt`, nên cửa sổ đang giữ bản cũ sẽ nhận `MSG418` và phải tải lại.

Trước SCRUM-30, khoá ngoại `SET NULL` đụng ràng buộc một ghi chú chung cho mỗi người viết, nên việc xoá mục thất bại
với `503` `MSG70`.

## Ghi chú vào bản nháp biên bản (BR-402)

`POST /api/meetings/{id}/minutes` ([contract](17-du-thao-bien-ban.md)) đọc ghi chú khác rỗng của Thư ký hiện tại. Khi
**chưa có dự thảo AI**, dù đã duyệt hay chưa (từ #263), ghi chú được điền như sau:

| Dòng ghi chú | Vào đâu |
|---|---|
| Dòng thường và dòng `Ý kiến:` của ghi chú chung | Đầu mục II, trước mục nghị sự đầu tiên |
| Dòng thường và dòng `Ý kiến:` của một mục | Mục II, dưới tiêu đề của mục đó. Khi mục có những dòng này, dòng "… trình bày." bỏ dấu "……" |
| Dòng `Kết luận:` | Mục III, "1. Kết luận của chủ trì", sau kết luận Chủ toạ đã ghi: `- <tên mục>: <nội dung>` (ghi chú chung: `- <nội dung>`) |
| Dòng `Nhiệm vụ:` | Mục III, "2. Nhiệm vụ, sản phẩm và thời hạn": `- <tên mục>: <nội dung>` (ghi chú chung: `- <nội dung>`), thay cho "……" |

Mỗi dòng được làm phẳng như chữ của AI: dấu `#`, `@@`, `^`, `~`, `**`, `__` ở đầu dòng bị bỏ, nên ghi chú không thể
thành tiêu đề hay khối chữ ký. Khi đã có dự thảo AI, biên bản lấy mục I–III từ dự thảo và không đọc ghi chú. Dự thảo AI
chưa đọc ghi chú (Q1 trong đặc tả sơ bộ).

## Hợp đồng tích hợp FE

Ghi chú là một panel dùng chung ([ADR-0011](../05-decisions/0011-tinh-nang-hop-la-panel-dung-chung.md)), có ở hai nơi:

- **Tab Ghi chú** trên trang chi tiết cuộc họp. Tab chỉ hiện với Chủ toạ và Thư ký khi cuộc họp `InProgress` hoặc
  `Ended`. Người khác mở `?tab=notes` thì về tab Thông tin chung.
- **Panel Ghi chú** trong phòng họp trực tuyến. Trên điện thoại panel nằm sau nút ⋯. Nút mở tab mới dẫn tới tab
  Ghi chú, và hai nơi không ghi đè nhau.

Cách panel chạy:

- **Chọn ghi chú:** ô chọn mặc định theo mục Chủ toạ đang để `InProgress` và đổi theo khi Chủ toạ chuyển mục.
  Thư ký chọn mục khác hoặc "Chung" thì ô chọn giữ lựa chọn đó.
- **Lưu:** tự lưu 1 giây sau khi ngừng gõ, khi rời ô nhập, khi đổi ghi chú và khi rời tab.
- **Nháp trên máy (BR-403):** chữ chưa lưu được giữ theo người dùng, cuộc họp và ghi chú, rồi xoá khi server đã có cùng
  nội dung:
  - cuộc họp Thường (`secret: false`): trong `localStorage`, nên đóng tab rồi vào lại vẫn còn;
  - cuộc họp Mật (`secret: true`): chỉ trong `sessionStorage`, mất khi đóng trang; nháp cũ trong `localStorage` của
    cuộc họp vừa đổi sang Mật được chuyển sang `sessionStorage`;
  - `GET` trả `404` (đã rời cuộc họp, hoặc không còn là Chủ toạ/Thư ký): xoá mọi nháp của cuộc họp đó;
  - đăng xuất: xoá mọi nháp ghi chú; phiên hết hạn thì giữ lại.
- **Mất mạng:** hiện `MSG423` và gửi lại khi có mạng (sự kiện `online`).
- **Lỗi 5xx:** hiện `message` của server, có nút "Thử lại", và tự thử lại sau 10 giây.
- **`MSG418`:** giữ chữ đang gõ, hiện bản mới nhất bên cạnh, cho chọn "Dùng bản mới" hoặc "Giữ bản này" (lưu lại trên
  bản mới nhất).
- **`MSG415`:** không toast (tự lưu liên tục); dòng trạng thái dưới ô nhập hiện câu của server kèm giờ lưu, ví dụ
  "Đã lưu ghi chú. (15:17)" (UC-82 bước 5).
- **`404` khi lưu (EX-8):** đọc lại; panel báo không xem được ghi chú.
- **`MSG416`, `MSG417`, `MSG420`:** khoá ô nhập, giữ chữ để sao chép. Khi panel chuyển sang chế độ chỉ đọc, chữ chưa
  lưu được còn trên máy hiện ở đầu panel, kèm nút Sao chép và Bỏ.
- **Dấu đầu dòng:** không có nút; Thư ký tự gõ `Kết luận:`, `Nhiệm vụ:` (gợi ý ở placeholder của ô nhập).
