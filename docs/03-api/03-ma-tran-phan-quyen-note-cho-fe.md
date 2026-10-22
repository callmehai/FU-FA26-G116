# Note cho FE — Ma trận vai trò – quyền, của Ticket SCRUM-60

Backend của SCRUM-60 đã xong (UC-08, UC-10). File này ghi những gì FE cần biết để làm màn hình
phân quyền, tập trung vào **những chỗ dễ làm sai mà lỗi lại trông như bug của backend**.

Hợp đồng API đầy đủ: [03-ma-tran-phan-quyen.md](03-ma-tran-phan-quyen.md). File này không nhắc
lại, chỉ nói phần FE.

> **Đổi hợp đồng từ `SCRUM-63`:** vai trò định danh bằng **`roleId` (Guid)**, không còn chuỗi
> `"Secretary"`. Mỗi dòng `roles[]` là `{ id, name, description, isBuiltIn, capabilities }`; body
> `PUT` gửi `roleId`. Các ví dụ dưới đã cập nhật. Chi tiết: [04-quan-ly-vai-tro.md](04-quan-ly-vai-tro.md).

Hai file đã dựng sẵn cho bạn, chưa ai import:

- [`frontend/src/types/rolePermission.ts`](../../frontend/src/types/rolePermission.ts)
- [`frontend/src/api/rolePermissions.ts`](../../frontend/src/api/rolePermissions.ts)

Màn hình hiện tại ở [`frontend/src/features/access/RolesPage.tsx`](../../frontend/src/features/access/RolesPage.tsx)
đang là placeholder.

---

## 1. Cái sẽ xoá sạch quyền của một vai trò: `capabilities` là **trạng thái cuối**, không phải delta

`PUT /api/roles/capabilities` nhận:

```json
{ "roleId": "01a0ce78-7203-7c7f-8326-7a0baebf4c22", "capabilities": ["MeetingCreator", "MinutesEditor"] }
```

**Gửi đủ mọi quyền vai trò đó cần có SAU khi sửa**, kể cả những quyền nó đã có sẵn từ trước.

Người dùng tick thêm một ô → bạn phải gửi cả những ô đang tick sẵn. Chỉ gửi ô vừa tick nghĩa là
**gỡ hết các quyền còn lại**, và backend sẽ trả `200 MSG16 "thành công"` vì đó là một request hoàn
toàn hợp lệ.

| Người dùng làm gì | Trạng thái hiện tại | Phải gửi |
|---|---|---|
| Tick thêm `MinutesEditor` | `["MeetingCreator"]` | `["MeetingCreator", "MinutesEditor"]` |
| Bỏ tick `MeetingCreator` | `["MeetingCreator", "MinutesEditor"]` | `["MinutesEditor"]` |
| Bỏ tick hết | `["MeetingCreator"]` | `[]` |

Mảng rỗng `[]` là **hợp lệ** và gỡ sạch quyền của vai trò đó.

Nhưng **thiếu hẳn field** thì bị từ chối `400`:

| Body gửi lên | Kết quả |
|---|---|
| `{"roleId":"<Member>","capabilities":[]}` | ✅ gỡ sạch quyền của Member |
| `{"roleId":"<Member>"}` | ❌ `400` — thiếu `capabilities` |
| `{"roleId":"<Member>","capability":[]}` | ❌ `400` — sai tên field |

Cố ý như vậy: nếu field thiếu mà đọc thành mảng rỗng thì một request `{}` sẽ âm thầm gỡ sạch quyền
của cả vai trò. Cùng một cái bẫy với `isActive` ở
[note SCRUM-13](02-quan-ly-nguoi-dung-note-cho-fe.md#1-cái-sẽ-xoá-nhầm-tài-khoản-của-người-ta-isactive-bắt-buộc-phải-gửi).

---

## 2. Mã quyền là **PascalCase**, khác với bảng cứng đang có trong code FE

API trả và nhận: `"AdminUsers"`, `"MeetingCreator"`, `"MeetingApprover"`, `"MinutesEditor"`.

Nhưng [`frontend/src/types/user.ts`](../../frontend/src/types/user.ts) đang có
`ROLE_CAPABILITIES` dùng **snake_case** (`'admin_users'`). Hai thứ đó **không khớp nhau**, đừng
trộn lẫn.

```ts
// SAI — 'admin_users' là của bảng cứng cũ
if (capabilities.includes('admin_users')) { }

// ĐÚNG — API trả PascalCase
import type { CapabilityCode } from '../types/rolePermission'
if (capabilities.includes('AdminUsers')) { }
```

> ⚠️ **`ROLE_CAPABILITIES` trong `user.ts` từ giờ có thể SAI.** Nó là bản sao cứng của ma trận mặc
> định, mà quản trị viên bây giờ đổi được ma trận thật lúc chạy. Đừng dùng nó để quyết định gì
> nữa — hỏi backend. Nó sẽ bị xoá ở `SCRUM-46`.

`role` cũng PascalCase (`"Admin"`, `"Chair"`, `"Secretary"`, `"Member"`) — cùng cái bẫy đã gặp ở
SCRUM-11 và SCRUM-13, xử lý giống hệt. Khi **gửi lên** thì backend nhận không phân biệt hoa thường,
nhưng cứ gửi PascalCase cho nhất quán.

---

## 3. Cột của bảng lấy từ API, **đừng hardcode**

`GET /api/roles/capabilities` trả về cả hai nửa:

```json
{
  "capabilities": [ { "code": "AdminUsers", "name": "Quản trị người dùng",
                      "description": "…", "functionalGroup": "Quản trị" } ],
  "roles":        [ { "id": "01a0ce78-7205-…", "name": "Quản trị viên", "description": "…",
                      "isBuiltIn": true, "capabilities": ["AdminUsers"] } ]
}
```

- `capabilities` → **cột** của lưới, kèm nhãn tiếng Việt và `functionalGroup` để gom nhóm cột
- `roles` → **dòng**

Nhãn tiếng Việt nằm ở backend để thêm một quyền mới vào enum thì không cần sửa FE. Nếu bạn tự giữ
bảng nhãn ở FE, quyền mới sẽ ra màn hình thành cột trống.

**Mọi vai trò đều xuất hiện, kể cả vai trò không có quyền nào** (`Member` → `capabilities: []`).
Dòng rỗng khác dòng thiếu — đừng lọc bỏ.

---

## 4. Ba nguyên nhân `MSG17`, phân biệt bằng **key** chứ không phải bằng message

`400` kèm `code: "MSG17"` có thể vì ba chuyện khác nhau. Đọc `errors` để biết là chuyện nào:

```json
{ "code": "MSG17", "message": "Cấu hình phân quyền không hợp lệ.",
  "errors": { "adminUsers": "Không thể lưu cấu hình này: …" } }
```

| Key | Nghĩa | Nên hiện ở đâu |
|---|---|---|
| `role` | Tên vai trò sai | Lỗi chung — gần như chắc là bug FE |
| `capabilities` | Mã quyền sai, hoặc thiếu field | Lỗi chung — message **có nêu tên mã sai** |
| `adminUsers` | **Thao tác này sẽ khoá tất cả mọi người ra ngoài** | Ngay tại ô checkbox `AdminUsers`, nổi bật |

`adminUsers` là cái quan trọng nhất phải hiện cho đúng — xem mục tiếp theo.

---

## 5. Luật chống tự khoá: **đừng chỉ ẩn nút**, và đừng đoán luật ở FE

Backend từ chối mọi cấu hình mà sau khi lưu sẽ **không còn tài khoản đang hoạt động nào** giữ
`AdminUsers`.

Điểm dễ hiểu sai: **luật đếm theo tài khoản, không phải theo vai trò.** Nên đây là những trường hợp
FE dễ đoán sai:

| Thao tác | FE hay đoán | Backend thật sự |
|---|---|---|
| Gỡ `AdminUsers` khỏi `Admin`, trong khi `Secretary` cũng có `AdminUsers` **và có người** | ❌ chặn | ✅ **cho phép** |
| Gỡ `AdminUsers` khỏi `Admin`, trong khi `Secretary` có `AdminUsers` nhưng **không ai mang vai trò Secretary** | ✅ cho phép | ❌ **chặn** |
| Cấp `AdminUsers` cho `Member` rồi gỡ khỏi `Admin` (không ai là Member) | ✅ cho phép | ❌ **chặn** |

FE **không biết được** ai đang mang vai trò nào từ endpoint này — nó không trả số lượng tài khoản.
Nên:

- **Đừng** tự tính luật này ở FE rồi khoá ô checkbox theo phán đoán của mình. Bạn sẽ khoá nhầm
  những thao tác hợp lệ.
- **Nên** cho người dùng bấm, rồi hiện lỗi `errors.adminUsers` nếu backend từ chối. Message backend
  trả về đã viết sẵn bằng tiếng Việt, đủ để hiện thẳng cho người dùng.
- Muốn tử tế hơn thì hiện cảnh báo mềm ("thao tác này có thể khiến không ai quản trị được") khi
  người dùng bỏ tick ô `AdminUsers` cuối cùng **mà bạn nhìn thấy** — nhưng vẫn để họ bấm, và vẫn
  phải xử lý lỗi từ backend.

Tiêu chí nghiệm thu ghi rõ *"Kiểm ở server, không phải ẩn nút"*. Ẩn nút là tiện lợi, không phải
bảo vệ.

---

## 6. Sau khi lưu thành công, người đang đăng nhập **nhận quyền mới, không bị đăng xuất**

> **Đổi theo SCRUM-105.** Trước đây backend huỷ refresh token của mọi tài khoản mang vai trò vừa
> sửa, nên họ bị đăng xuất. Giờ backend không huỷ nữa.

Đổi ma trận xong, backend gửi sự kiện **`capabilitiesChanged`** (không payload) trên
`/hubs/notifications` tới mọi tài khoản đang hoạt động mang vai trò vừa sửa, ở cả hai chiều.
`NotificationsProvider` nhận được thì gọi `reloadSession()`: đọc `/me`, thấy quyền khác token thì
`/refresh`, rồi sidebar và `RequireCapability` render lại theo quyền mới. Mỗi lần hub kết nối lại,
FE cũng gọi `reloadSession()`, phòng trường hợp sự kiện đã phát lúc mất kết nối.

Hệ quả FE phải lường:

1. **Nếu quản trị viên sửa chính vai trò của mình** → họ cũng nhận sự kiện, phiên giữ nguyên.
   `RolesPage` tự gọi `reloadSession()` ngay sau khi lưu, không chờ sự kiện, và không còn banner
   "hãy đăng nhập lại". Gọi hai lần không sao vì refresh là single-flight.
2. **Nếu họ gỡ `AdminUsers` khỏi chính vai trò mình đang mang** → request **kế tiếp** tới
   `/api/roles/capabilities` hay `/api/users` sẽ nhận **`403` ngay lập tức**, không chờ token hết
   hạn. Đây **không phải bug** — backend đọc lại quyền từ database ở từng request.

Gặp `403` giữa phiên thì xử lý như [note SCRUM-13](02-quan-ly-nguoi-dung-note-cho-fe.md): đưa về
màn hình phù hợp, đừng hiện "lỗi hệ thống".

---

## 7. Route phải trả `404`, không phải `403`

Tiêu chí nghiệm thu: *"Chỉ người có quyền `admin_users` vào được; route thiếu quyền trả 404"*.

**Phần này chưa ai làm — là việc của bạn.** Bọc route bằng `RequireCapability` như các màn hình
quản trị khác, để người không có quyền thấy **404 giống hệt URL không tồn tại**, chứ không phải
403. Màn hình bị hạn chế thì không được lộ ra là nó tồn tại (quy ước ở `AGENTS.md`).

Đừng nhầm với tầng API: `/api/roles/capabilities` trả **`403`** khi thiếu quyền, và như vậy là
đúng — FE cần phân biệt "hết quyền giữa chừng" với "gọi sai URL". Các test backend đang assert
`403` là cho tầng API, **không** chứng minh tiêu chí `404` đã xong.

---

## 8. Không có nút "Tạo quyền mới"

`Capability` là enum trong code — quản trị viên sửa **mapping**, không thêm quyền. Tiêu chí nghiệm
thu ghi rõ *"Màn hình không được có nút Tạo quyền mới"*.

**Vai trò thì tạo được** từ `SCRUM-63` ([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)):
`POST/PUT/DELETE /api/roles`, xem [04-quan-ly-vai-tro.md](04-quan-ly-vai-tro.md). `isBuiltIn = true`
là một trong bốn vai trò mặc định — không đổi tên, không xoá được, nhưng **vẫn sửa quyền được** ở
màn hình này. Vai trò tự tạo có `isBuiltIn = false`.

Cũng **chưa có** chức năng "Khôi phục mặc định" (`resetRolePermissionsToDefault` trên class diagram
là phần chưa làm, xem PHẦN F của [07-classdiagram/README.md](../07-classdiagram/README.md)). Đừng
làm nút đó rồi để treo.

---

## 9. `MSG18` là `500` nhưng **có body**

| Mã | HTTP | Nghĩa | FE nên làm |
|---|---|---|---|
| `MSG16` | `200` | Lưu thành công, kèm `matrix` mới | Vẽ lại lưới từ `matrix` trong response, **không cần gọi lại `GET`** |
| `MSG17` | `400` | Cấu hình không hợp lệ | Hiện lỗi theo key (mục 4) |
| `MSG18` | `500` | Database từ chối ghi | *"Không lưu được, vui lòng thử lại"* — **không** phải lỗi người dùng |

`MSG18` khác các lỗi `500` thông thường ở chỗ **có body JSON** với `code` và `message`. Nếu
`ApiError` của bạn không giữ body cho status `5xx` thì sẽ mất thông tin này — cân nhắc bắt riêng,
hoặc cứ hiện thông báo "thử lại" chung cho mọi `5xx` cũng chấp nhận được.

Điểm khác biệt đáng nói: `MSG17` là "sửa lại rồi gửi", `MSG18` là "gửi lại y nguyên".
