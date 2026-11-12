# Ghi chú cho FE — Chia sẻ lịch (SCRUM-142, FE-16)

**Từ:** BE (Huy), 10/11/2026. **Contract:** [23-chia-se-lich.md](23-chia-se-lich.md). **FE đã làm xong** (10/11/2026), mô tả ở §8 của contract.

## Việc FE cần làm

1. `frontend/src/types/calendarSharing.ts` — type cho `Share`, `ShareLists`, `ShareCandidatePage`, `SharedWeek` đúng như contract §3, §4.
2. `frontend/src/api/calendarSharing.ts` — gọi qua `api.get/post/patch` của `client.ts`, không `fetch` trong component. Chỉ dùng đường dẫn tương đối `/api/...`.
3. Khớp lỗi bằng `isApiError(error, status, 'MSGnn')`, **không** khớp theo câu chữ. `MessageCodeCatalogueTests` sẽ fail nếu FE khớp
   cặp (status, mã) không có trong [danh mục](00-danh-muc-ma-thong-bao.md).
4. Route màn: BE dựng link thông báo là `/calendar/sharing?share={id}`. Nếu FE đặt route khác, báo BE để sửa một dòng
   (`NotificationTargetResponse.BuildUrl`). Mở từ thông báo thì cuộn tới / mở chia sẻ có `id` đó.
5. Màn Nhật ký hệ thống (SCRUM-16): BE ghi `objectType = "CalendarShare"` cho 13 mã `calendar_share.*`. Thêm dòng
   `CalendarShare: 'Chia sẻ lịch'` vào `frontend/src/features/audit/objectTypes.ts`, nếu không cột Đối tượng hiện nguyên chữ `CalendarShare`.

## Việc FE còn thiếu — từ BA Final Review (12/11/2026)

**Mở thông báo `?share={id}` mà dòng không còn thì phải báo lý do.** Hiện `CalendarSharingPage.tsx:70-71` chỉ `scrollIntoView` khi tìm thấy dòng
`share-{id}`; không thấy thì im lặng, người dùng không biết vì sao thông báo không dẫn tới đâu. `stale()` (dòng 96-101) chỉ toast cho lỗi 404 **sau khi lưu**, không phải lúc mở.

Cần: khi `lists` đã tải xong, có `focusId` mà không có dòng nào khớp (cả `asOwner` lẫn `asViewer`) thì hiện toast lỗi **một lần**, rồi bỏ `?share=` khỏi URL
(để tải lại trang không toast lại):

| Trường hợp | Toast |
|---|---|
| Người dùng không có quyền tạo cuộc họp (đã mất quyền, nên chia sẻ cho họ xem đã dừng) | câu `MSG439` |
| Còn lại (chia sẻ đã đóng, quá ngày kết thúc, hoặc không phải của họ) | "Chia sẻ này không còn." |

Cơ sở: UC-84f AF-3 (Report 3 §UC-84f), quyết định PO ở Jira `10516`. Không cần BE đổi gì: dữ liệu đã đủ trong `GET /api/calendar/shares`.
Nên có một test FE hoặc kiểm tay: mở `/calendar/sharing?share=<id không tồn tại>`, thấy toast và URL sạch `?share=`.

## Những điểm dễ sai

- **Nút hiện theo `actions`** của từng chia sẻ (`accept`, `decline`, `withdraw`, `requestChange`, `update`, `end`). Đừng tự suy từ `status`:
  ví dụ lời mời có `senderDeactivated = true` chỉ còn `decline`, và FE hiện câu `MSG468` ("Tài khoản người gửi đã bị vô hiệu hoá…"); yêu cầu mà người xin đã mất quyền tạo cuộc họp cũng chỉ còn `decline`.
- `requestChange` (người xem) và `update` (chủ lịch) cùng gọi `PATCH /api/calendar/shares/{id}`; người xem nhận `201 MSG454`, chủ lịch `200 MSG442`.
- `accept` của chủ lịch với yêu cầu UC-84a cho phép chọn mốc thấp hơn / khoảng ngắn hơn **trong** khoảng được xin; với chia sẻ UC-84b
  (`initiatedBy = Owner`) thì không gửi `level`/ngày (gửi khác giá trị hiện tại là `400 MSG443`).
- `status = Stopped`: FE tự hiện nhãn "Đã dừng" và câu `MSG439` khi bấm vào; BE không gửi mã này. **Không có** trạng thái `Expired`:
  chia sẻ quá ngày kết thúc biến khỏi danh sách (PO 09/11).
- Người không có quyền tạo cuộc họp (Thành viên) vẫn mở được màn Chia sẻ lịch để thấy **lịch mình đã chia sẻ** (`asOwner`) — trả lời yêu cầu,
  tự chia sẻ (UC-84b), đổi, ngừng. `asViewer` của họ luôn rỗng; nút "Xin xem lịch" và tab Lịch được chia sẻ ẩn với họ
  (gọi vẫn trả `403 MSG448`). **PO chốt 10/11/2026 (L8, Jira 10498)** — giống AI-eCabinet, nên đặt màn Chia sẻ lịch ở chỗ mọi tài khoản vào được (eCab để trong menu tài khoản).
- Ô chọn người: `GET /api/calendar/share-candidates?purpose=request` (xin xem) hoặc `purpose=direct` (tự chia sẻ), tìm theo `q`, phân trang;
  `hasOpenShare = true` thì làm mờ người đó. Lỗi đọc `500 MSG467`.
- Ngày gửi lên dạng `YYYY-MM-DD` theo lịch Việt Nam; ngày bắt đầu sớm nhất là hôm nay, tối đa 365 ngày tính cả hai đầu (BR-431) — kiểm ở FE
  cho tiện người dùng, server vẫn kiểm (`400 MSG443`).
- Tab Lịch được chia sẻ (`GET /api/calendar/shared-week?week=`): `known = false` là "chưa biết", **không** vẽ như rảnh. Giờ bận là ISO có
  `+07:00`. Mỗi người một màu; khối chồng nhau đặt cạnh nhau. Không có tên cuộc họp nào để hiện (BR-427).
- Thông báo chia sẻ lịch có `targetType = "CalendarShare"`, `meetingId = null`; luồng mở thông báo hiện có (`/api/notifications/{id}/target`)
  dùng được, chỉ cần FE có route.

## Mã FE cần xử lý

| Mã | HTTP | Ở đâu |
|---|---|---|
| `MSG435`, `MSG441` | 201 | gửi yêu cầu / tự chia sẻ |
| `MSG436`, `MSG437`, `MSG442`, `MSG452` | 200 | trả lời chia sẻ hoặc thay đổi |
| `MSG454` | 201 | người xem xin thay đổi |
| `MSG438`, `MSG451` | 200 | rút / ngừng |
| `MSG443`, `MSG455` | 400 | giá trị sai / bên nhận không nhận được |
| `MSG448` | 403 | không có quyền tạo cuộc họp |
| `MSG449`, `MSG450`, `MSG468` | 409 | trùng / sai trạng thái / người gửi bị vô hiệu hoá |
| `MSG453`, `MSG467` | 500 | đọc lỗi (danh sách, tuần / danh sách người) |
| `MSG447` | 503 | lưu lỗi |
| `MSG439` | — | FE tự hiện cho `Stopped` |

## Sau review của đối tác (11/11/2026)

BE đã sửa (nhánh `fix/scrum-142-duyet-thay-doi-sau-khi-doi-khoang`): duyệt một thay đổi chờ mà chủ lịch đã dời ngày bắt đầu qua ngày
kết thúc được xin trước đây trả `503 MSG447` ("thử lại" mãi không được). Nay `actions` của chủ lịch không có `accept` trong trường hợp đó,
và gọi `accept` trả `409 MSG450`. FE đọc `actions` và đã xử lý `MSG450` (đóng hộp thoại, toast, tải lại), nên không phải đổi gì cho phần này.

Hai lỗi nhỏ phía FE, review tìm ra, BE không sửa:

1. `frontend/src/features/calendarSharing/ShareDialogs.tsx` (khoảng dòng 353): hộp thoại "Đồng ý thay đổi" của chia sẻ UC-84b hiện tên
   chủ lịch (chính người đang bấm). Thay đổi chờ luôn do người xem gửi, nên khi có `pendingChange` thì người gửi là `share.viewer`:
   `const sender = change ? share.viewer : (share.initiatedBy === 'Viewer' ? share.viewer : share.owner)`.
2. `ShareDialogs.tsx` (khoảng dòng 141 và 307): `onRefused` truyền vào là arrow function viết tại chỗ và nằm trong deps của `useEffect`,
   nên mỗi lần render lại hộp thoại là gọi lại `share-candidates` (đổi mức xem 2 lần → 4 request giống nhau). Bọc bằng `useCallback`
   trong `NewShareDialog`, hoặc giữ trong `useRef`.

Thêm một điểm để FE xem: thông báo `CalendarShareStopped` gửi cho người xem vừa mất quyền tạo cuộc họp mở `/calendar/sharing?share=…`,
nhưng theo L8 người đó không còn phần "được chia sẻ với tôi", nên trang không có dòng nào để tô. Nội dung thông báo đã nói lý do; nếu
không tìm thấy `share` trên trang thì FE có thể hiện câu `MSG439` thay vì im lặng.

**FE đã sửa cả ba điểm trên** (nhánh `fix/scrum-142-share-dialog-sender`): hộp thoại "Đồng ý thay đổi" ghi người xem là người xin;
`onRefused` đọc qua `useEffectEvent` nên chỉ gõ tìm mới gọi lại `share-candidates`; mở `?share=` mà trang không có dòng đó thì hiện
toast `MSG439` (chia sẻ đã dừng, hoặc người xem không còn thẻ "được chia sẻ với tôi") hay "Chia sẻ này không còn.".
