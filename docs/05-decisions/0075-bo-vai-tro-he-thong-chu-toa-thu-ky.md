# ADR-0075: Bỏ vai trò hệ thống Chủ toạ và Thư ký, bỏ quyền "Soạn biên bản"; chỉ người có quyền "Thu giọng mẫu hộ" thu hộ

- **Trạng thái:** Đề xuất — theo spec của Lộc 21/11/2026 (đã thống nhất trong nhóm; Khánh xác nhận đây là ý định ban
  đầu, dữ liệu seed làm sai)
- **Ngày:** 2026-11-22
- **Người quyết định:** Nhóm (Lộc đề xuất, Khánh xác nhận), Hải thực hiện
- **Thay một phần:** [ADR-0008](0008-vai-tro-la-du-lieu.md) (bốn vai trò mặc định),
  [ADR-0060](0060-thu-ky-thu-voice-id-ho.md) (Thư ký thu Voice ID hộ trong cuộc họp),
  điểm "mỗi người thu tối đa 5 mẫu" của [ADR-0071](0071-thu-giong-mau-ho-bat-ky-luc-nao.md)
- **Liên quan:** [ADR-0007](0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md), [ADR-0073](0073-hoi-y-nguoi-duoc-thu-moi-lan-thu-ho.md),
  [ADR-0004](0004-mot-to-chuc-khong-co-don-vi.md)

## Bối cảnh

Hệ thống có hai loại vai trò:

- **Vai trò hệ thống** (`roles`, `role_capabilities`): quyết định người đó được làm gì trong cả hệ thống (tạo cuộc họp, phê
  duyệt, quản trị người dùng).
- **Vai trò trong cuộc họp** (`MeetingParticipant.Role`: Chủ toạ, Thư ký, Thành viên): quyết định người đó làm gì trong
  **một** cuộc họp (Thư ký soạn biên bản, Chủ toạ duyệt, …).

Dữ liệu seed lại có thêm hai vai trò hệ thống trùng tên **Chủ toạ** và **Thư ký**, cùng quyền hệ thống **Soạn biên bản**
(`MinutesEditor`). Quyền này không endpoint nào kiểm: biên bản luôn xét theo vai trò trong cuộc họp (`MinutesService`:
chỉ Thư ký của cuộc họp soạn, sửa, gửi duyệt; chỉ Chủ toạ của cuộc họp duyệt, trả lại; Chủ toạ và Thư ký đọc bản nháp).
Nó chỉ làm ma trận phân quyền hiểu nhầm. Ngoài ra, từ ADR-0060 Thư ký cuộc họp thu được giọng mẫu hộ, còn PR #461 cho cả Thư
ký không có quyền hệ thống thu khi cuộc họp đang mở: dữ liệu sinh trắc học của người khác đi theo một vai trò trong cuộc họp
mà ai tạo cuộc họp cũng gán được.

Spec của Lộc (21/11/2026, Discord):

1. Bỏ hai vai trò hệ thống Chủ toạ và Thư ký. Chủ toạ, Thư ký chỉ là vai trò trong cuộc họp.
2. Chỉ còn ba vai trò mặc định: Thành viên (Tạo cuộc họp); Phê duyệt viên (Tạo + Phê duyệt cuộc họp); Quản trị viên (Tạo
   cuộc họp + Thu giọng mẫu hộ + Quản trị người dùng).
3. "Soạn biên bản" không phải quyền hệ thống: bỏ khỏi ma trận phân quyền.
4. Thư ký không còn là người thu hộ: chữ trong hệ thống đổi thành quản trị viên thu hộ.

## Quyết định

1. **Ba vai trò mặc định** (`BuiltInRoles`):

   | Vai trò | id | Quyền mặc định |
   |---|---|---|
   | Thành viên (`Member`) | `01a0ce78-7204-…` (giữ nguyên) | Tạo cuộc họp |
   | Phê duyệt viên (`Approver`) | `01a0ce78-7202-…` (id của "Chủ toạ" cũ) | Tạo cuộc họp, Phê duyệt cuộc họp |
   | Quản trị viên (`Admin`) | `01a0ce78-7205-…` (giữ nguyên) | Tạo cuộc họp, Thu giọng mẫu hộ, Quản trị người dùng |

   Thứ tự hiển thị: Thành viên, Phê duyệt viên, Quản trị viên, rồi vai trò tự tạo theo thời gian tạo. Thành viên vẫn là vai
   trò chọn sẵn khi tạo tài khoản. Vai trò tự tạo (ví dụ "All-in-one" trên server test) giữ nguyên, chỉ mất quyền không còn
   tồn tại.
2. **Bỏ capability `MinutesEditor`** khỏi enum, catalogue (`PermissionCatalog`), ma trận và bản sao ở FE. Tên này không dùng
   lại. Không quyền biên bản nào đổi: các endpoint biên bản và dự thảo AI vốn đã xét theo vai trò trong cuộc họp.
3. **Thu giọng mẫu hộ chỉ theo quyền `VoiceCollector`.** Bỏ đường thu theo cuộc họp của Thư ký (bốn route
   `/api/meetings/{id}/voice-collection…`, thẻ "Giọng mẫu cho mic phòng" ở tab Thành phần tham dự và Bản ghi, việc mở sớm
   tab Bản ghi cho Thư ký thu giọng), bỏ cảnh báo "Thư ký không có vai trò hệ thống" ở danh sách người dự, bỏ giới hạn 5 mẫu
   mỗi người thu (còn tổng 50 mẫu mỗi người). Mã `MSG477` ngừng dùng. Người có quyền thu ở *Quản trị → Người dùng → nút
   micro* (`/api/users/{userId}/voice-collection`, ADR-0071), mỗi lần thu đều hỏi ý người được thu (ADR-0073).
   `VoiceCollector` chuyển sang nhóm "Quản trị" của ma trận.
4. **Thông báo Voice ID lên 1.5.** Câu mục đích bỏ "Thư ký cuộc họp tôi dự thu bằng micro phòng họp", nói người được quản
   trị viên giao quyền "Thu giọng mẫu hộ" thu sau khi hỏi ý. 1.5 hẹp hơn 1.4 (bớt một người thu), nên người đồng ý 1.4 vẫn
   được thu hộ (`ProxyNoticeVersion` giữ 1.4); họ thấy lời mời đồng ý điều khoản mới như mọi lần lên phiên bản.
5. **Dữ liệu** — migration `SystemRolesWithoutChairSecretary` (chỉ dữ liệu, không đổi schema):
   1. Vai trò tự tạo nào đã tên "Phê duyệt viên" được đổi tên sang một bên (thêm 8 ký tự đầu của id).
   2. "Chủ toạ" thành "Phê duyệt viên", giữ id, nên người giữ "Chủ toạ" ở lại đúng vai trò đó với quyền như cũ.
   3. Người giữ "Thư ký" chuyển sang **Phê duyệt viên** (gần nhất: vẫn tạo được cuộc họp, thêm phê duyệt) để không ai mất
      vai trò; xoá vai trò "Thư ký".
   4. Xoá mọi dòng `role_capabilities` của `MinutesEditor`, kể cả của vai trò tự tạo.
   5. Ba vai trò mặc định nhận đúng quyền ở bảng trên (DB có `role_capabilities` trống thì để `ReferenceDataSeeder` điền).
   Seed dev và `--seed-demo`: `chutoa@` là Phê duyệt viên, `thuky@` là Thành viên (vẫn là Chủ toạ, Thư ký ở các cuộc họp
   mẫu). DB đã có từ trước thì `thuky@` thành Phê duyệt viên theo migration.

## Hệ quả

- Thành viên giờ tạo được cuộc họp (trước đây không có quyền nào). Ai được giao làm Thư ký hay Chủ toạ là do người tạo cuộc
  họp chọn, không còn phụ thuộc vai trò hệ thống.
- Người từng là "Thư ký" hệ thống có thêm quyền Phê duyệt cuộc họp. Quản trị viên có thể đổi họ về Thành viên nếu không
  muốn.
- Quản trị viên có thêm Tạo cuộc họp và Thu giọng mẫu hộ. Vai trò tự tạo chỉ có `VoiceCollector` mà không có `AdminUsers`
  thì gọi được API thu hộ nhưng không có màn hình chọn người (danh sách người dùng cần `AdminUsers`).
- Mẫu Thư ký đã thu trong cuộc họp trước ADR-0075 vẫn thuộc người được thu, hiện kèm tên cuộc họp; người có quyền thu hộ
  thấy, nghe, xoá được như mẫu thu ngoài cuộc họp.
- Không đổi quy tắc 401/403/404. Route theo cuộc họp đã bỏ trả `404` như mọi URL lạ.

## Nghĩa vụ theo Luật 91/2025/QH15

- Dữ liệu sinh trắc học của người khác giờ chỉ do người được quản trị viên cấp quyền chạm vào, không theo một vai trò trong
  cuộc họp mà người tạo cuộc họp gán được. Quyền đọc lại trong cơ sở dữ liệu mỗi lần gọi.
- Đồng ý vẫn chỉ do chính chủ, ở trang của họ; mỗi lần thu hỏi ý họ (ADR-0073); rút đồng ý hoặc bị vô hiệu hoá tài khoản thì
  xoá ngay mọi mẫu, cả mẫu thu hộ (ADR-0049).
