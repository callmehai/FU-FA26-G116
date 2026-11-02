# SCRUM-116 — Việc BE còn lại: che email roster với Khách, khoá tên sau lần khai đầu

**Người ghi:** KhanhPG (BA), 02/11/2026. **Người nhận:** BE làm SCRUM-116 (Lộc / gumarr). **Nguồn yêu cầu:**
[comment PO trên SCRUM-116](https://sep490g116.atlassian.net/browse/SCRUM-116?focusedCommentId=10394) (Hải, 02/11/2026).
Gốc của hai việc này là hai câu hỏi PO ở [note 21 §4.1](21-scrum-116-truy-cap-khach-uc04.md) (điểm A và B).

> **Trạng thái code: ĐÃ LÀM XONG ở PR #222 (`02c7057`, merge vào `main` 02/11/2026).** Cả A (che email) và B (khoá tên) đã có
> code, test, contract và danh mục mã MSG381. Kết quả: A chọn kiểu `email: null`; B khoá cả `jobTitle`, gửi lại đúng giá trị
> cũ vẫn `200`. Giới hạn còn lại: chưa khoá dòng nên hai thiết bị khai khác tên cùng lúc vẫn có thể để bên ghi sau thắng. Phần dưới
> giữ nguyên làm bản giao việc gốc. Việc còn lại là của BA: sửa Report 3, xem [note 21 §3](21-scrum-116-truy-cap-khach-uc04.md).

Hai việc độc lập, làm trong một PR hay hai PR đều được. Mỗi việc ghi rõ: PO chốt gì, code hiện làm gì, cần đổi gì, test, tài liệu.

---

## A. Che email khi người xem là Khách

**PO chốt:** Khách xem danh sách thành phần chỉ thấy **tên và vai trò**, không thấy email của ai (thành viên nội bộ lẫn Khách khác).
Quy tắc chỉ áp cho người xem là Khách; Chủ toạ, Thư ký, Thành viên vẫn thấy email như cũ.

### A.1. Code hiện làm gì

- `GET /api/meetings/{meetingId}/participants` do `ParticipantEndpoints.cs` dựng actor (có `Guest` khi token là token Khách) rồi gọi
  `ParticipantService.ReadAsync` (`backend/TrustCabinet.Infrastructure/Participants/ParticipantService.cs`).
- Dòng ~216–249: hàng nội bộ lấy `p.User.Email`, hàng Khách lấy `g.Email`, đưa vào `ParticipantRow.Email`
  (`backend/TrustCabinet.Domain/Participants/ParticipantContracts.cs` dòng 49, kiểu `string` không null). Không có nhánh nào theo
  `actor.Guest`.
- Dòng ~259–261: ô tìm kiếm `q` khớp `r.Email`. **Khách vẫn dò được email của người khác bằng tìm kiếm**, ví dụ `q=an@` ra hay không ra
  hàng của ai. Che trường trong JSON mà để nguyên điều kiện tìm là chưa che.

### A.2. Việc cần làm

1. Khi `actor.Guest is not null`: không đưa email vào hàng trả về, **kể cả hàng của chính Khách đó** (email của họ đã có ở
   `GET /api/guest-access/session`, trường `email`, nên không cần ở roster).
2. Khi `actor.Guest is not null`: bỏ email khỏi điều kiện tìm `q` (chỉ tìm `DisplayName` và `InvitedName`).
3. Giữ nguyên vai trò (`role`): hàng nội bộ vẫn có Chair/Secretary/Member; hàng Khách có `role = null` và `kind = "Guest"`, FE đã
   hiện nhãn "Khách".
4. **Phải chốt một kiểu trả**, vì `ParticipantRow.Email` đang là `string` không null:
   - (a) trả `email: null` cho Khách, đổi kiểu thành `string?`; hoặc
   - (b) bỏ hẳn khoá `email` khỏi JSON khi người xem là Khách (cần hai DTO hoặc serializer có điều kiện).
   BA nghiêng về (a): cùng một hình dạng JSON, FE chỉ cần kiểm `null`. Ghi kiểu đã chọn vào contract.
5. Rà các đường khác Khách đọc được xem còn lộ email không. **BA đã rà, không thấy chỗ nào khác**, ghi để BE xác nhận lại:
   - `participants/editor-options` và `participants/chair-handover-candidates`: Khách bị chặn (`Forbidden`) ở `ReadAsync`
     (`CanManageParticipants` false; `HandoverAccess` trả `Forbidden` cho Khách). Có email nhưng Khách không tới được.
   - `participant-context`, `invitation-response`, điểm danh, biểu quyết, agenda, tài liệu, phòng họp (`GET /call`), `participantsChanged`
     trên hub: các DTO không có trường email (không có `Email` trong `GuestAccessContracts`/DTO tương ứng trừ `GuestSessionView`
     là email của chính Khách).
   - `participants` realtime: `ParticipantsChangePublisher` chỉ gửi `participantsChanged` (không kèm dữ liệu hàng).
   - Dòng ~683 `g.Email` trong audit (`JsonSerializer.Serialize`): ghi log nội bộ, không trả cho Khách.
   Nếu BE thấy thêm chỗ, báo BA để ghi.
6. `InvitedName` trên hàng Khách (`invitedName`) vẫn trả cho Khách như hiện nay: đó là tên Thư ký gõ khi mời, không phải email. Nếu
   Thư ký từng gõ một địa chỉ email vào ô tên thì nó lộ; BA coi đây là dữ liệu do Thư ký nhập, không xử lý trong việc này.

### A.3. Tài liệu BE phải sửa cùng commit (AGENTS.md: đổi hợp đồng API thì sửa `docs/03-api/` cùng commit)

- [09-thanh-phan-tham-du.md](../../03-api/09-thanh-phan-tham-du.md): ví dụ roster item (dòng ~47) và đoạn ngay sau (dòng ~53: "tìm
  displayName/email/invitedName") ghi thêm: với người xem là Khách, `email` là `null` (hoặc bị bỏ) và `q` không tìm theo email.
- [09-thanh-phan-tham-du-guest-uc04.md](../../03-api/09-thanh-phan-tham-du-guest-uc04.md): dòng 16 nói email vẫn thuộc lời mời; thêm
  một dòng về việc che.
- Không có mã MSG mới, không cần sửa danh mục mã.

### A.4. Test cần có (đặt cạnh `tests/TrustCabinet.Web.Tests/GuestAccess/GuestMeetingAccessTests.cs`, đoạn roster dòng ~44)

- Khách gọi `GET …/participants`: **không hàng nào** có email (cả nội bộ lẫn Khách, cả hàng của chính họ).
- Khách gọi với `q=<một phần email của thành viên>`: kết quả rỗng (`total = 0`); `q=<tên>`: vẫn ra.
- Thành viên nội bộ gọi cùng endpoint: vẫn thấy email (không hồi quy).
- Test hiện có ở dòng 44–47 chỉ kiểm `selfDeclaredDisplayName` và `invitedName` nên không vỡ.

### A.5. FE (làm cùng hoặc ngay sau, vì BE đổi dữ liệu)

- `frontend/src/features/participants/ParticipantsPanel.tsx` dòng ~285 truyền `email={row.email}` vào `Person`
  (`Person.tsx` dòng 7 khai `email: string`, dòng 19 in `cell-sub`). Khi `email` là `null` cần không in dòng phụ (hoặc đổi kiểu `string | null`).
- Dòng ~197 ô tìm kiếm ghi "Tìm theo tên hoặc email": với trang Khách (`guestInvitationId` có giá trị) đổi thành "Tìm theo tên".
- Kiểu `participant` trong `frontend/src/types/` và `frontend/src/api/participants.ts`: đổi `email` thành `string | null` nếu chọn (a).
- Chạy theo skill `verify-ui-in-browser` và `check-responsive`, vì đây là màn Khách (điện thoại).

---

## B. Khoá tên hiển thị sau lần khai đầu

**PO chốt:** Khách khai tên một lần; từ lần sau **không đổi được**. (Trước đó: khai lại thì ghi đè, contract 16 ghi "tạm chưa làm".)

### B.1. Code hiện làm gì

- `PUT /api/guest-access/session/profile` → `GuestAccessService.SaveProfileAsync`
  (`backend/TrustCabinet.Infrastructure/GuestAccess/GuestAccessService.cs` dòng 176–205): kiểm tên (bắt buộc, ≤200, không ký tự điều
  khiển) và chức vụ, rồi **luôn ghi đè** `Invitation.SelfDeclaredDisplayName` / `SelfDeclaredJobTitle` (dòng 194–195).
- Hồ sơ **dùng chung cho mọi phiên của một lời mời** (cột nằm trên `meeting_guest_invitations`), nên "lần khai đầu" nghĩa là lần đầu của
  cả lời mời, không phải của một phiên/thiết bị.
- Trạng thái "đã khai" đã có sẵn: `profileCompleted` = `SelfDeclaredDisplayName is not null` (dòng 149 và 236).

### B.2. Việc cần làm

1. Trong `SaveProfileAsync`, sau khi tìm được phiên (`FindSessionAsync`) và **trước** bước kiểm tên: nếu
   `current.Invitation.SelfDeclaredDisplayName is not null` thì từ chối, **không ghi gì**.
2. **Mã thông báo mới.** Số kế tiếp trong danh mục là **`MSG381`**
   ([00-danh-muc-ma-thong-bao.md](../../03-api/00-danh-muc-ma-thong-bao.md), mục "Mã tiếp theo"). `MSG380` đã dùng (Khách chưa xác nhận
   thông báo ghi âm); `MSG368`–`MSG370` bỏ qua có chủ ý. Đọc lại mục này ngay trước khi đặt số, vì PR khác có thể lấy MSG381 trước.
3. Gợi ý HTTP **409 Conflict**. BE chọn mã trạng thái khác thì ghi rõ trong contract, FE khớp theo mã `MSGnn` chứ không theo chữ.
4. Thông điệp gợi ý (tiếng Việt, FE hiển thị): "Thông tin hiển thị đã được khai và không thể thay đổi."
5. **Thử lại cùng giá trị:** nếu giá trị gửi lên (sau khi cắt khoảng trắng) **giống hệt** giá trị đã lưu thì trả `200/MSG376` như thành
   công, không báo 409. Lý do: bấm hai lần hoặc mạng rớt giữa chừng rồi gửi lại là chuyện thường; không nên bắt Khách thấy lỗi vì
   một lần khai đã thành công. (Đề xuất của BA, BE quyết; nếu bỏ thì ghi vào contract.)
6. **Hai phiên khai cùng lúc** (hai thiết bị cùng một lời mời): phải có đúng một người thắng. `FindSessionAsync(tracked: true)` đang
   theo dõi thực thể; cần đảm bảo bước kiểm "đã khai" và bước ghi không bị lọt hai request song song (khoá dòng lời mời như các luồng
   OTP đã làm: "khoá dòng lời mời", hoặc cập nhật có điều kiện `WHERE self_declared_display_name IS NULL` và kiểm số dòng bị ảnh hưởng).
   Người thua nhận 409 (hoặc 200 nếu giá trị trùng, mục 5).
7. **Chức vụ (`jobTitle`) có bị khoá không?** PO chỉ nói "khoá tên". BA đề xuất **khoá cả hai cùng lúc** (một lần khai, hai ô), cho đơn giản
   và đúng với luồng hiện tại (FE gửi cả hai ô trong một lần). **Cần xác nhận với PO/Hải trước khi làm**; trả lời thế nào thì ghi vào
   ticket SCRUM-116 và BA sửa BR-180 theo.
8. Thứ tự lỗi: Khách đã khai mà gửi dữ liệu sai (ví dụ tên rỗng) thì trả 409 trước hay 400 trước? Đề xuất **409 trước** (đã khoá thì không
   cần kiểm nội dung).
9. Không đụng `InvitedName` và `Email` (bằng chứng lời mời), như BR-180 hiện nay.
10. Thư ký sửa được tên không? Ngoài phạm vi: PO không nói, và hiện không có chức năng nào cho Thư ký sửa hồ sơ tự khai. Không thêm.

### B.3. Tài liệu BE phải sửa cùng commit

- [16-truy-cap-khach-uc04.md](../../03-api/16-truy-cap-khach-uc04.md):
  - §4.2 (dòng ~121): bỏ câu "Khai lại thì ghi đè lần khai trước"; thêm dòng 409/`MSG381` vào bảng trả về và nêu quy tắc thử lại cùng giá trị.
  - §9 (dòng ~208): bỏ dòng "Khoá tên hiển thị sau lần khai đầu: tạm chưa làm".
  - Bảng mã cuối file: thêm `MSG381`.
- [00-danh-muc-ma-thong-bao.md](../../03-api/00-danh-muc-ma-thong-bao.md): thêm dòng `MSG381` vào bảng và cập nhật "Mã tiếp theo" **trong
  cùng commit** (quy ước AGENTS.md). Cột "Report 3" ghi bản tiếng Anh để BA đưa vào Report 3 §V.2.
- Mã khai báo bằng hằng số đầu file endpoint (`private const string Msg381… = "MSG381";`), không viết chuỗi `"MSG381"` rải rác.
  `MessageCodeCatalogueTests` sẽ vỡ nếu thiếu dòng danh mục hoặc không có test khẳng định mã.

### B.4. Test cần có (`tests/TrustCabinet.Web.Tests/GuestAccess/GuestSessionTests.cs`)

- Khai lần 1 → `200/MSG376`; khai lần 2 với tên **khác** → `409/MSG381`, và đọc DB thấy `SelfDeclaredDisplayName` vẫn là giá trị lần 1.
- Khai lần 2 với **cùng** giá trị → `200/MSG376` (nếu giữ mục B.2.5).
- Phiên thứ hai của cùng lời mời (đã có test `Reopening_with_a_live_session…`, dòng 68–80) cố khai tên khác → 409.
- Hai request khai đồng thời với hai tên khác nhau → đúng một `200`, một `409`; DB giữ tên của request thắng.
- Khách đã khai gửi tên rỗng → theo thứ tự lỗi đã chọn (mục B.2.8).
- Test hiện có `Profile_is_self_declared_and_never_overwrites_the_invitation` (dòng 34–66) khai đúng một lần thành công nên không vỡ.
- **Chú ý fixture:** `GuestAccessFixture.SignIn(s, profile: true)` (`GuestAccessFixture.cs` dòng 153–161) tự gọi PUT profile với tên cố định
  "Khách tự khai" và đòi `200/MSG376`. Test nào đăng nhập **hai lần cho cùng một `Scenario` với `profile: true`** sẽ nhận 409 ở lần
  hai. Nếu giữ mục B.2.5 thì không vỡ (cùng giá trị trả 200); nếu bỏ thì phải sửa fixture. Chạy cả `GuestAccess/` và `Calls/` trước khi đẩy.

### B.5. FE (SCRUM-121 đã merge, cần chỉnh nhỏ)

- `GuestPage.tsx` dòng ~75 đã cho vào thẳng khi `profileCompleted`, nên Khách đã khai không thấy lại form: luồng thường không đổi.
- `GuestProfileForm.tsx` chỉ cần xử lý mã mới cho trường hợp đua (hai thiết bị): bắt 409/`MSG381`, hiện thông báo, rồi tải lại phiên
  (`GET /api/guest-access/session`) và vào cuộc họp với tên đã lưu. Hiện nó chỉ bắt 400/`MSG377`, 404, 401; 409 sẽ rơi vào `errorMessage`
  chung.
- Hiển thị trên form: nếu muốn báo trước cho Khách "tên chỉ khai được một lần", thêm một dòng ghi chú dưới ô tên (xin ý Hải về chữ).

---

## Sau khi merge: báo BA

- BA sửa **BR-180** (bỏ "chưa khoá tên", ghi khoá sau lần khai đầu), **UC-04** bước 5 và thêm exception cho khai lại bị từ chối, thêm
  dòng `MSG381` vào Report 3 §V.2, sửa BR-181/UC-28 nhóm theo việc che email, rồi ghi Change Log (xem [note 21 §3](21-scrum-116-truy-cap-khach-uc04.md)).
- Ghi vào ticket SCRUM-116 (theo `DEVELOPMENT_WORKFLOW.md §2.1`): mã MSG mới, kiểu trả của `email` (a hay b), `jobTitle` có khoá không,
  và cách xử lý thử lại cùng giá trị. BA đọc các ghi chú đó thay vì tự đoán từ code.

## Hạn chế của note này

- BA **đã đọc code** `ParticipantService`, `GuestAccessService`, `ParticipantContracts`, `ParticipantEndpoints`, `CapabilityGate`, FE
  `ParticipantsPanel`/`GuestProfileForm`/`GuestPage` và hai file test Khách; **chưa chạy** app hay test để xem hành vi thật.
- Mục A.2.5 (không còn đường lộ email khác) là kết quả rà bằng `grep` và đọc DTO, không phải chứng minh. BE xác nhận lại.
- Mục B.2.5–B.2.8 và B.2.7 là đề xuất của BA, chưa có quyết định của PO. Chỉ hai điều PO đã chốt: che email khi người xem là Khách; khoá tên sau lần khai đầu.
