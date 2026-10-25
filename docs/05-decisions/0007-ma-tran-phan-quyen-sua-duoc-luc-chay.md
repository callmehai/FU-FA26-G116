# ADR-0007: Ma trận phân quyền sửa được lúc chạy — bỏ `HasData`

- **Trạng thái:** Đã chấp nhận — phần *"Kèm theo: thu hồi session nằm ngoài transaction"* thay thế
  bởi [ADR-0022](0022-sua-quyen-vai-tro-khong-dang-xuat.md) đối với việc sửa ma trận quyền
- **Ngày:** 2026-09-22
- **Người quyết định:** Nhóm SEP490-G116
- **Liên quan:** `SCRUM-60` (quản lý ma trận vai trò – quyền), `SCRUM-12` (thực thi quyền),
  `SCRUM-16` (nhật ký kiểm toán)

## Bối cảnh

Bảng `role_capabilities` được dựng thành **bảng** thay vì `switch` cứng trong code chính là để
quản trị viên đổi được ai làm được gì mà không phải deploy lại. Comment trong
[`RoleCapability.cs`](../../backend/TrustCabinet.Domain/Entities/RoleCapability.cs) đã ghi nhận
điều đó từ đầu, kèm một cảnh báo: các dòng dữ liệu đang ship qua `HasData`, nên một migration sau
sẽ đè lên những gì quản trị viên đã chỉnh.

Đến `SCRUM-60` thì cảnh báo đó thành vấn đề phải xử lý, vì story này mở đúng màn hình cho phép
chỉnh sửa.

**Vì sao `HasData` và "người dùng sửa được" không đi chung được:**

EF so sánh seed data của model với seed data của **migration trước**, chứ không so với dữ liệu
thật trong database. Nên khi ai đó sửa `ReferenceData.RoleCapabilities` rồi sinh migration mới, EF
sinh ra `UPDATE`/`DELETE` nhắm thẳng vào đúng những dòng quản trị viên đã đổi trên production — và
âm thầm khôi phục giá trị mặc định ngay lúc deploy. Không có cảnh báo, không có xung đột, chỉ là
quyền của cả tổ chức lặng lẽ quay về mặc định.

Ticket yêu cầu chọn một trong hai và ghi thành ADR.

## Quyết định

**Bỏ `HasData`. Chuyển sang seed lúc khởi động, và chỉ khi bảng rỗng.**

Cụ thể:

- `RoleCapabilityConfiguration` không còn gọi `builder.HasData(...)`.
- Thêm [`ReferenceDataSeeder`](../../backend/TrustCabinet.Infrastructure/Seed/ReferenceDataSeeder.cs):
  chèn ma trận mặc định **chỉ khi `role_capabilities` hoàn toàn rỗng**.
- `ReferenceData.RoleCapabilities` từ nay là **giá trị khởi tạo**, không còn là sự thật của hệ
  thống đang chạy. Muốn biết một deployment đang thực thi gì thì đọc bảng.

### Vì sao "chỉ khi bảng rỗng", không phải "bù các dòng thiếu"

Bù từng dòng thiếu nghe an toàn hơn, nhưng thực ra là phá đúng thứ ADR này muốn bảo vệ: một dòng
"thiếu" không phân biệt được với một dòng **quản trị viên đã cố ý gỡ**. Bù lại tức là mỗi lần khởi
động lại lặng lẽ hoàn tác công việc của họ. Bảng rỗng hoàn toàn thì chỉ xảy ra với database chưa
seed bao giờ.

### Migration `RoleCapabilitiesEditable` cố ý để trống

EF scaffold ra 5 lệnh `DeleteData` khi thấy `HasData` biến mất khỏi model. Đã **xoá bằng tay**, vì
chạy chúng sẽ xoá ma trận khỏi một hệ thống đang chạy: giữa lúc migration chạy và lúc app khởi
động lại, không vai trò nào giữ `AdminUsers` và không ai đăng nhập vào sửa được. Nặng hơn nữa với
database mà quản trị viên đã chỉnh: 5 dòng mặc định bị xoá, các dòng riêng của họ ở lại, và seeder
thì bỏ qua vì bảng không còn rỗng.

Migration chỉ đổi **nguồn khai báo** của dữ liệu, không đụng vào **dữ liệu**. Database cũ giữ
nguyên những gì đang có.

**Đã kiểm thực tế**, không phải suy luận: dựng một database ở migration `AddGoogleSignIn`, sửa ma
trận như một quản trị viên (gỡ `MinutesEditor` khỏi `Secretary`, cấp thêm `AdminUsers`), rồi apply
`RoleCapabilitiesEditable`. Kết quả: cả 5 dòng **giữ nguyên không đổi**. Nếu để nguyên 5 lệnh
`DeleteData` mà EF sinh ra thì 4 trong 5 dòng đó đã bị xoá.

Còn database migrate từ đầu thì **vẫn có sẵn ma trận**: migration `InitialCreate` có lệnh
`InsertData` chèn đúng 5 dòng mặc định, và migration đã lên `main` thì bất biến nên vẫn chạy như
cũ. Nên `ReferenceDataSeeder` trên thực tế là **lưới an toàn**, không phải nguồn duy nhất — nó chỉ
có việc để làm khi bảng rỗng, tức khi database được dựng bằng cách nào đó không qua migration đầy
đủ (restore một bản dump không kèm dữ liệu, hoặc provision schema bằng cách khác).

## Hệ quả

**Được:**

- Quản trị viên sửa được ma trận, và thay đổi đó **sống sót qua deploy** — điều kiện cần để
  `SCRUM-60` có nghĩa.
- Không còn khả năng một migration âm thầm reset quyền của cả tổ chức.

**Mất / phải nhớ:**

- **`ReferenceData.RoleCapabilities` từ nay chỉ là giá trị khởi tạo.** Sửa nó **không** thay đổi
  hệ thống đang chạy, và cũng không còn sinh ra migration nào. Muốn đổi mặc định cho database mới
  thì sửa ở đó; muốn đổi hệ thống đang chạy thì dùng màn hình phân quyền.
- **Một database có bảng `role_capabilities` rỗng là hỏng**: không ai có `AdminUsers` → không ai
  đăng nhập vào sửa được. Luồng migrate thông thường không rơi vào đó (`InitialCreate` đã chèn
  sẵn), nhưng restore một bản dump thiếu dữ liệu thì có.
  - Ở Development: `MigrateAndSeedDevelopmentAsync` đã gọi seeder, không phải làm gì thêm.
  - Ngoài Development: bật `Database:SeedReferenceDataOnStartup` nếu muốn lưới an toàn đó.
    Xem [`03-co-so-du-lieu.md`](../02-design/03-co-so-du-lieu.md).
- Test `Role_capabilities_match_the_agreed_mapping` trong `DatabaseModelTests` đã đổi tên thành
  `..._agreed_default_mapping` và bổ sung ghi chú: nó pin **giá trị mặc định**, không phải hành vi
  của hệ thống đang chạy.
- Một test mới (`Role_capabilities_are_not_seeded_through_the_migration`) canh để không ai vô tình
  thêm `HasData` trở lại.

## Kèm theo: sửa ma trận phải nằm trong một transaction có khoá bảng

Phát hiện khi review PR, không phải lúc thiết kế — ghi lại vì nó là hệ quả trực tiếp của việc cho
sửa ma trận lúc chạy.

Luật *"phải còn ít nhất một tài khoản đang hoạt động giữ `AdminUsers`"* **vắt qua nhiều dòng** của
bảng. Nên đọc–kiểm–ghi mà không khoá thì hai request đồng thời đều đọc ma trận cũ, đều thấy "vai
trò kia vẫn giữ quyền", đều qua validate, rồi đều ghi — kết quả là không ai giữ nữa. Đúng cái trạng
thái luật đó sinh ra để chặn.

**Quyết định (bản đầu, đã thay — xem mục dưới):** `UpdateAsync` chạy trong một transaction
`Serializable` và lấy `LOCK TABLE role_capabilities IN EXCLUSIVE MODE` ngay đầu.

Có test với PostgreSQL thật chạy hai update đồng thời (`RolePermissionRepositoryTests`). Đã kiểm
ngược: bỏ dòng khoá đi thì test fail 3/3 lần.

### Sửa sau review PR #10: khoá bảng không phủ hết bất biến

Review chỉ ra khoá trên chỉ đóng **một nửa**. Bất biến *"còn ít nhất một tài khoản đang hoạt động
giữ `AdminUsers`"* do **hai bảng** cùng quyết định: `role_capabilities` nói vai trò nào mang quyền,
`users` nói ai đang hoạt động và mang vai trò gì. `UserService` (SCRUM-13) canh đúng bất biến đó từ
phía kia, nhưng nó **không bao giờ chạm** `role_capabilities` nên **không bao giờ lấy** khoá này:

```
Ma trận: Admin→{AdminUsers}, Secretary→{AdminUsers}
Alice = Admin (active), Bob = Secretary (active)

A: PUT /api/users/{alice}/status  (vô hiệu hoá)  → còn Bob giữ quyền     → cho phép
B: PUT /api/roles/secretary/...   (gỡ AdminUsers) → còn Alice đang active → cho phép

Cả hai commit → không tài khoản nào quản trị được nữa
```

Mỗi request tách riêng đều hợp lệ; chỉ khi đi cùng nhau mới sai. Khoá `role_capabilities` không
chặn được A, còn khoá `users` không chặn được B.

**Quyết định mới:** bất biến trải trên hai bảng nên **không thuộc bảng nào**. Thay bằng một
**advisory lock dùng chung** — `IAdministratorInvariantGuard`, gọi
`pg_advisory_xact_lock(hashtext('admin-users-invariant'))` — và **cả hai phía** đều đi qua nó:
`RolePermissionService.UpdateAsync`, `UserService.UpdateAsync` và `UserService.ChangeStatusAsync`.

- `pg_advisory_xact_lock` (bản `_xact_`) chứ không phải `pg_advisory_lock`: khoá tự nhả khi
  transaction kết thúc dù kết thúc kiểu gì. Bản session-scoped sẽ sống sót qua rollback rồi rò rỉ
  theo connection trong pool.
- Isolation hạ từ `Serializable` xuống **`Read Committed`**, và đây là điểm phản trực giác:
  snapshot của một transaction được chụp ở câu lệnh **đầu tiên**, tức là *trước* khi giành được
  khoá. Nên dưới `Serializable`, request phải xếp hàng sẽ cầm một snapshot có từ trước lúc request
  thắng commit, rồi ghi đè lên thứ nó không nhìn thấy → PostgreSQL huỷ nó bằng lỗi `40001` thay vì
  cho đọc lại. Một lần sửa thứ hai hoàn toàn bình thường lại thành lỗi serialization mà caller chỉ
  có thể retry. Dưới `Read Committed`, mọi câu lệnh sau khi lấy khoá đọc đúng kết quả đã commit của
  request thắng — đúng thứ validate cần. Chính advisory lock mới là thứ bảo đảm loại trừ lẫn nhau,
  không phải isolation level.
- `IRolePermissionRepository.InTransactionAsync` bị **xoá**: để lại hai cơ chế khoá song song cho
  cùng một bất biến chính là cái bẫy mà review vừa chỉ ra.

Test: `AdministratorInvariantGuardTests` chạy với PostgreSQL thật, chứng minh request thứ hai phải
đợi request thứ nhất commit, khoá được nhả khi write ném lỗi, và lời gọi lồng nhau chạy chung
transaction bên ngoài. Đã kiểm ngược: bỏ dòng lấy khoá đi thì test serialisation fail.

### Kèm theo: thu hồi session nằm ngoài transaction

Cũng từ review PR #10. Vòng thu hồi refresh token chạy **bên trong** transaction, tức là giữ khoá
suốt N round trip sang Redis, tuần tự — vai trò đông người thì chặn mọi thao tác phân quyền khác
trong cả khoảng đó, và **Redis chết thì rollback luôn thay đổi hợp lệ**.

**Quyết định:** commit trước, thu hồi session sau; thất bại thì log rồi đi tiếp, không ném lỗi.
Thay đổi đã lưu và đúng — báo lỗi cho quản trị viên chỉ khiến họ làm lại một việc đã xong. Cái mất
là session kết thúc muộn hơn, tối đa đúng bằng vòng đời access token (15 phút) — cùng cửa sổ đã
chấp nhận ở chỗ khác, và cũng là lý do `/api/users` đọc lại tài khoản người gọi ở mỗi request.
`UserService` áp dụng y hệt.

## Các phương án đã cân nhắc

**Giữ `HasData`, không cho sửa qua UI (tức huỷ `SCRUM-60`).** Đơn giản nhất, và trung thực hơn
phương án nửa vời. Loại vì lý do tồn tại của bảng `role_capabilities` chính là để sửa được — giữ
`HasData` thì bảng đó chỉ là một `switch` viết vòng qua database, và nên thay bằng `switch` thật
cho đỡ nhầm.

**Cột "đã bị người dùng sửa" để migration biết đường tránh.** Giữ được cả hai, nhưng phải thêm cột,
thêm luật hoà giải giữa mặc định mới và chỉnh sửa cũ, và mỗi migration sau đều phải nhớ luật đó.
Quá nhiều máy móc cho một bảng 5 dòng.
