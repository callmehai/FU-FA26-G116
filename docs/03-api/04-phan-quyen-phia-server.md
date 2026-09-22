# 04 · Phân quyền phía server

**SCRUM-12.** Quy ước áp dụng cho **mọi** endpoint nghiệp vụ, không riêng ba nhóm đang có. Ba file
`01`–`03` mô tả từng tính năng; file này mô tả luật chung mà cả ba đều tuân theo, và mà endpoint
tiếp theo (cuộc họp, tài liệu, biên bản) phải tuân theo.

> Giao diện không bao giờ là ranh giới bảo mật. `RequireCapability` ở FE và việc lọc sidebar chỉ
> quyết định **render cái gì**. Ẩn một cái nút không phải là kiểm soát truy cập.

## Cổng quyền: hai lớp, không phải một

Mọi nhóm endpoint đi qua `CapabilityGate.RequireCapability(Capability)`
([`backend/TrustCabinet.Web/Endpoints/CapabilityGate.cs`](../../backend/TrustCabinet.Web/Endpoints/CapabilityGate.cs)),
và cổng này kiểm tra **hai lần**:

| Lớp | Đọc từ | Bắt được gì |
|---|---|---|
| 1. Authorization policy | claim `cap` trong access token | Caller chưa từng có quyền |
| 2. Đọc lại từ database | bảng `users` + `role_capabilities` | Caller **vừa mất** quyền |

Lớp 2 tồn tại vì access token là **ảnh chụp** của caller tại thời điểm phát hành. Thu hồi refresh
token không chạm được vào access token đang nằm trong tay họ. Không có lớp 2, một quản trị viên vừa
bị vô hiệu hoá — hoặc có vai trò vừa bị gỡ `AdminUsers` bằng màn hình SCRUM-60 — vẫn giữ nguyên
quyền cho tới khi token hết hạn (`Authentication:AccessTokenMinutes`). Đủ lâu để vô hiệu hoá lại
người vừa vô hiệu hoá mình, hoặc để tự cấp quyền lại cho vai trò của mình.

Caller bị từ chối ở lớp 2 khi: không đọc được claim `sub`, tài khoản không còn tồn tại, tài khoản
`is_active = false`, hoặc vai trò hiện tại không còn capability cần thiết.

Cổng đặt trên **nhóm** (`MapGroup`) chứ không trên từng route: một `MapGet` thêm vào nhóm đã được
canh sẽ tự thừa hưởng cổng, trong khi danh sách theo từng route thì cái ai đó quên sẽ mở toang.

Quyền luôn kiểm theo **capability**, không bao giờ so sánh tên vai trò. Chuyển một capability sang
vai trò khác trong `role_capabilities` phải có hiệu lực ngay mà không cần sửa code.

## Mã trạng thái: giấu dòng dữ liệu, không giấu endpoint

| Trường hợp | Mã | Lý do |
|---|---|---|
| Chưa đăng nhập | `401` | — |
| Đã đăng nhập, thiếu capability, **endpoint cố định** (`/api/users`, `/api/roles/capabilities`) | `403` | Đường dẫn nằm trong tài liệu API công khai, giấu đi không được gì; `404` chỉ khiến FE không phân biệt nổi "sai quyền" với "gọi sai URL" |
| Không được đọc **một tài nguyên cụ thể** (tài liệu, cuộc họp) | `404` | Ở đây `403` tự nó là thông tin: nó xác nhận tài nguyên đó **có tồn tại** — xem SCRUM-22 |
| Route UI thiếu quyền | `404` | Màn hình bị hạn chế không tự lộ ra là nó có tồn tại |

Tóm lại: giấu **sự tồn tại của một dòng dữ liệu**, không giấu **sự tồn tại của một endpoint**.

Hệ quả cho endpoint theo id: `404` của "không có quyền đọc" và `404` của "không tồn tại" phải
**giống hệt nhau từng byte**. Khác nhau một chữ trong body là đủ để dùng response làm công cụ dò
xem id nào có thật.

## Endpoint trả danh sách phải lọc ở server

Bản ghi mà người gọi không được xem thì **không bao giờ rời khỏi backend**. Không trả về rồi để FE
ẩn đi — dữ liệu đã nằm trong máy người dùng, đọc bằng DevTools là xong, và nó cũng nằm trong cache
của trình duyệt.

Hiện `GET /api/users` trả toàn bộ danh sách, và điều đó **đúng**: cả nhóm nằm sau `AdminUsers`, ai
qua được cổng thì được xem tất cả. Khi endpoint danh sách đầu tiên có luật theo từng dòng xuất hiện
(cuộc họp: chỉ thấy cuộc họp mình tham dự), việc lọc thuộc về câu truy vấn, không thuộc về response.

## `GET /api/auth/me`

Trả về tập quyền để giao diện biết render cái gì — chi tiết ở
[`01-xac-thuc.md`](01-xac-thuc.md). Đây là **nguồn duy nhất** của tập quyền phía FE.

Endpoint này cũng đọc lại từ database như cổng quyền, chứ không đọc claim trong token. Nó là chỗ
giao diện dựa vào để render, nên nếu nó trả lời theo ảnh chụp trong token thì người vừa bị vô hiệu
hoá sẽ thấy nguyên màn hình quản trị — mọi nút đều `403`, nhưng đó là màn hình không nên vẽ ra ngay
từ đầu. Tài khoản không còn hoặc đã bị vô hiệu hoá thì trả `401`, không phải `403`.

```jsonc
{
  "id": "0199...",
  "email": "thuky@trustcabinet.local",
  "fullName": "Nguyễn Thư Ký",
  "role": "Secretary",                              // PascalCase, đúng tên enum
  "capabilities": ["MeetingCreator", "MinutesEditor"]
}
```

Bảng cứng `ROLE_CAPABILITIES` trong [`frontend/src/types/user.ts`](../../frontend/src/types/user.ts)
là scaffolding, sẽ biến mất cùng SCRUM-46. Nó **không** phải nguồn sự thật và đang lệch với backend
(mã quyền ở đó là `snake_case`, backend trả PascalCase). Đừng thêm capability mới vào đó.

## Thêm một endpoint nghiệp vụ mới

1. Thêm capability vào `Capability` ([`AccessEnums.cs`](../../backend/TrustCabinet.Domain/Enums/AccessEnums.cs))
   nếu chưa có, kèm mô tả tiếng Việt trong
   [`PermissionCatalog`](../../backend/TrustCabinet.Domain/Access/PermissionCatalog.cs) — thiếu mô tả
   là fail ngay, chứ không lặng lẽ ra một cột trống trên màn hình phân quyền.
2. `app.MapGroup("/api/...").RequireCapability(Capability.X)`. Policy đã được
   `AddCapabilityPolicies()` đăng ký sẵn cho mọi capability, không cần khai báo thêm.
3. Endpoint theo id: trả `404` cho cả "không tồn tại" lẫn "không được đọc", cùng một body.
4. Endpoint danh sách: lọc trong truy vấn.
5. Lấy id người gọi bằng `CapabilityGate.GatedUserId(principal)` — **không bao giờ** lấy từ body:
   một actor id do caller tự chọn không chứng minh được gì, mà đó lại chính là thứ ghi vào nhật ký
   kiểm toán.

Test tương ứng: [`tests/TrustCabinet.Web.Tests/Access/CapabilityGateTests.cs`](../../tests/TrustCabinet.Web.Tests/Access/CapabilityGateTests.cs).
Thêm route mới vào `GatedRoutes()` ở đó là có ngay bộ kiểm tra 401/403 cho nó.
