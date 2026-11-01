# SCRUM-122 — kết quả kiểm thử E2E Khách (UC-04 → UC-26 Agenda, thu hồi, hết hạn)

Ngày kiểm tra: 01/11/2026 (UTC+7). Branch `feat/scrum-121-123-guest-frontend`, tách từ `main` tại `67ad43a` (đã có
PR #203 SCRUM-116 và PR #208 SCRUM-123). Chạy cùng FE của SCRUM-121 (màn 1.3, hồ sơ tự khai, trang cuộc họp của Khách)
và SCRUM-123 (Khách xem, tải tài liệu). Contract: [16-truy-cap-khach-uc04.md](16-truy-cap-khach-uc04.md).

## Môi trường và cách làm

- Backend `dotnet run` (:5225) và Vite (:5173) trên máy local, PostgreSQL / SeaweedFS / Redis của `docker compose`, DB
  dev. Email đi qua SMTP thật; hộp thư Khách là hộp thư tạm trên mail.tm.
- **Không có token Khách tạo tay hay adapter giả.** Mọi token đi qua luồng thật: Thư ký mời → thư mời tới hộp thư → lấy
  link trong thư → mở lời mời → gửi mã → đọc mã trong thư OTP → xác minh → khai tên.
- Cuộc họp đi đúng workflow qua API: tạo → gửi duyệt → duyệt → công bố → bắt đầu → kết thúc. Mục nghị sự có gắn tài
  liệu PDF.
- Realtime dùng chính client `@microsoft/signalr` của FE, chạy trong Node, nối thẳng `/hubs/meetings`.
- Hai spec Playwright, giữ ngoài repo (`~/.playwright-e2e/tests/`): `scrum-122.spec.ts` (API + hub, 9 case) và
  `scrum-121.spec.ts` (giao diện Khách, 9 case). Dữ liệu tạo ra có tiền tố `[Test FE]`.

## Kết quả theo mục của ticket

| # | Mục | Kết quả |
|---|---|---|
| 1 | Thư ký mời Khách vào cuộc họp Published có mục nghị sự gắn tài liệu. Khách nhận mail, mở link, nhập OTP, khai hồ sơ | **Đạt.** Thư "Lời mời tham dự cuộc họp: …" tới hộp thư, link dạng `/guest/{invitationId}#token=…`. Thư OTP tới sau vài giây. Chưa khai tên thì GET Agenda `404`; khai tên `200/MSG376`. JWT có `sub = guest:…`, không có `cap` |
| 2 | GET Agenda bằng token Khách → 200, có metadata tài liệu, mọi cờ chỉnh sửa false. Cuộc họp khác → 404 body rỗng. Options và mutation → 403 | **Đạt.** `items[0].documents = [{id, title: "To-trinh-khach.pdf"}]`; `permissions` và mọi `items[].actions` không có giá trị `true`. Cuộc họp khác `404`, body rỗng. `editor-options` và 6 mutation (thêm, sửa, xoá, sắp xếp, bắt đầu mục, kết thúc mục) đều `403` |
| 3 | Khách mở realtime: Thư ký sửa mục nghị sự, Khách nhận event và tải lại snapshot mới | **Đạt.** Join rồi GET; Thư ký đổi tên mục → Khách nhận `agendaChanged`, GET lại thấy tên mới. Trên giao diện, tên mới hiện không cần tải lại trang |
| 4 | Một lời mời hai phiên: thu hồi phiên thứ nhất → phiên đó GET 404 và không nhận event; phiên thứ hai vẫn chạy | **Đạt.** Phiên 2 lấy bằng mã mới (sau 1 phút chờ gửi lại). `DELETE /session` của phiên 1 → `204`, gọi lại vẫn `204`; GET Agenda `404`, GET session `404/MSG378`; join lại bị từ chối. Thư ký sửa mục: phiên 2 nhận `agendaChanged`, phiên 1 không nhận gì. Phiên 2 GET `200` |
| 5 | Thu hồi lời mời → mọi phiên mất quyền GET/join và không nhận event. Hết hạn tương tự. JWT hết hạn thì hub đóng | **Đạt** (xem ghi chú về JWT). Lời mời có hai phiên A, B đang join hub. Cho phiên A hết hạn (đặt `guest_sessions.expires_at` về quá khứ — không chờ được 12 giờ): A GET `404`, B vẫn `200`. Thu hồi lời mời → A, B đều GET `404`, join bị từ chối, không nhận `agendaChanged` khi Thư ký sửa mục; mở lại link → `404/MSG371` |
| 6 | Số dòng User và roster không tăng. Khách không gọi được API nội bộ | **Đạt.** `users` và `meeting_participants` không đổi. Token Khách: `/api/auth/me` `401`; `/api/users`, `/api/meetings`, `/api/notifications`, `/api/documents` `403`; hub `/hubs/notifications` nhận bắt tay rồi đóng ngay (`Context.Abort`) |
| 7 | Cuộc họp Draft/WaitingApproval/Rejected/Approved: Khách không đọc được Agenda | **Đạt, theo cách gián tiếp.** Trước khi công bố, Khách không có link: mời Khách vào cuộc họp nháp, gửi duyệt rồi duyệt — hộp thư vẫn trống, không có `guest_sessions`; token Khách của cuộc họp khác GET Agenda cuộc họp này `404`. Công bố xong thì thư mời mới tới. Trường hợp Khách đã có phiên mà cuộc họp *quay lại* các trạng thái này không xảy ra được trong workflow; luật `GuestAccessRules.MeetingOpen` có test tích hợp ở BE |
| 8 | Chair kết thúc cuộc họp khi Khách đang xem: không nhận thêm event; GET Agenda, thành phần, chi tiết đều 404; join lại bị từ chối; FE không hiện trang chi tiết; người nội bộ vẫn xem được | **Đạt, một điểm lệch mã.** Kết thúc cuộc họp: Thư ký nhận `agendaChanged`, Khách không nhận event nào. Khách GET `agenda`, `participants`, `participant-context`, `invitation-response`, `attendance`, `documents`, nội dung tài liệu, `votes` đều `404`; GET session `404/MSG378`; join bị từ chối. Thư ký vẫn GET Agenda `200`. **GET `/api/meetings/{id}` (chi tiết) trả `403`, không phải `404`**: endpoint này chỉ dành cho người dùng nội bộ (`RequireActiveUser`), Khách nhận `403` với mọi id ở mọi trạng thái nên không lộ cuộc họp nào tồn tại. Trên giao diện, trang đang mở chuyển sang "Cuộc họp đã kết thúc hoặc lời mời đã bị thu hồi" trong vòng 20 giây (kiểm lại phiên định kỳ), dữ liệu cũ bị gỡ khỏi màn hình |
| 9 | Các gate trong `09-thanh-phan-tham-du-guest-uc04.md` (trừ gate 6 cũ) | Xem bảng dưới |

### Gate trong [09-thanh-phan-tham-du-guest-uc04.md](09-thanh-phan-tham-du-guest-uc04.md)

| Gate | Kết quả |
|---|---|
| 1. Mail → link → OTP đúng email → hồ sơ → phiên thật; không tăng User | Đạt (mục 1, 6) |
| 2. Tự trả lời Accepted/Declined; không đọc API nội bộ hay cuộc họp khác; đoán ID không cấp quyền | Đạt. `PUT /invitation-response` `Declined` → `200/MSG105`; trả lời lần hai `409`. Mục 2, 6 |
| 3. Đọc roster/response; không có quyền quản lý; REST và hub theo phiên | Đạt. `participant-context`: `myRole = Guest`, `canManageParticipants = false`; roster có dòng Khách với `displayName` tự khai và `invitedName` gốc; `POST /guest-invitations` bằng token Khách `403` |
| 4. Hai phiên một lời mời; thu hồi riêng phiên và thu hồi cả lời mời; link cũ không hồi sinh | Đạt (mục 4, 5) |
| 5. Retry/crash provider trước/sau persist | Không kiểm ở E2E (cần gây lỗi lưu trữ), do test tích hợp BE đảm nhận như contract yêu cầu |
| 6. *(đổi 30/10/2026)* Cuộc họp `Ended`: Khách không đọc gì, không nhận realtime | Đạt (mục 8) |

## Giao diện Khách (SCRUM-121, SCRUM-123) — cùng lượt

| Kịch bản | Kết quả |
|---|---|
| Mở link: tên cuộc họp, email che (`tc***@…`), không sidebar; link token bị xoá khỏi thanh địa chỉ; tự gửi mã, "Gửi lại mã (n giây)" đếm theo BE; "Mã có hiệu lực trong 15 phút."; thông báo ghi âm và phạm vi Khách | Đạt |
| 6 ô mã: chỉ nhận số, tự nhảy ô, dán cả mã; mã sai → MSG126 dưới ô | Đạt |
| Hồ sơ: tên mặc định là tên Thư ký đã mời; để trống → báo lỗi tại chỗ; ký tự điều khiển (U+202E) → `MSG377` hiện dưới ô; lưu → vào cuộc họp | Đạt |
| Trang cuộc họp: tên tự khai + chức vụ + nhãn Khách, nút Thoát; 4 tab; không nút quản lý nào; trả lời "Tham dự" → chip "Đã xác nhận tham dự", DB `Accepted`; dòng của Khách trong roster có "(Tôi)" | Đạt |
| ALT-1: mở lại link khi còn phiên → vào thẳng, không gửi thêm mã | Đạt |
| Tài liệu (SCRUM-123): danh sách, tải về đúng tên tệp, mở ở `/guest/{id}/documents/{docId}`, PDF hiện đủ trang, không có nút Góp ý; "‹ Tài liệu" về đúng tab | Đạt |
| Biểu quyết có vai trò Khách và tài liệu đính kèm: "Gửi phiếu" khoá → mở tài liệu `?read=1`, cuộn tới cuối, "Xác nhận đã đọc" → quay lại bỏ phiếu → `vote_receipts` có 1 dòng của lời mời | Đạt |
| Trình duyệt đang đăng nhập nội bộ mở link Khách: mọi request `/api/meetings/{id}/…` mang token có `sub = guest:…`; quay lại `/mgmt/meetings` vẫn là phiên nội bộ | Đạt |
| Điện thoại 390px: không cuộn ngang; ô mã rộng ~45px; nút Thoát cao ≥ 40px | Đạt |
| Thoát: phiên đó GET `404`, phiên còn lại của cùng lời mời vẫn `200` | Đạt |
| Cuộc họp kết thúc khi đang mở trang; mở lại link sau khi kết thúc; thu hồi lời mời khi đang mở trang | Đạt — đều chuyển sang màn "Cuộc họp đã kết thúc hoặc lời mời đã bị thu hồi" |
| Sai mã 5 lần → `MSG372`, nút Xác minh khoá; hết 1 phút gửi lại mã mới → vào được | Đạt |

## Ngoài E2E, do test BE đảm nhận

- **JWT hết hạn thì hub đóng kết nối:** không chờ được 12 giờ trên máy thật. Hub `/hubs/meetings` bật
  `CloseOnAuthenticationExpiration` cho mọi token; `AgendaHubTests.Query_tokens_are_hub_only_and_expiring_connection_closes`
  kiểm việc đóng kết nối, test của SCRUM-116 kiểm `exp` của token Khách trùng hạn phiên.
- Lỗi lưu trữ, race khi xác minh/gửi mã đồng thời, giới hạn 20 mã/24 giờ: test tích hợp trong
  `tests/TrustCabinet.Web.Tests/GuestAccess/`.

## Ghi nhận

- Gửi mã chạy đồng bộ qua SMTP nên trên máy local mất khoảng 5–8 giây. FE hiện "Đang gửi mã 6 số tới …" trong lúc chờ.
- Ticket mục 8 ghi "chi tiết cuộc họp trả 404"; thực tế `403` như mô tả ở trên. Không lộ thông tin, nhưng BA/Tester cần
  cập nhật kỳ vọng.
