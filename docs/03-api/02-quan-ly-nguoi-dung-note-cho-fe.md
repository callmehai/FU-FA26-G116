# Note cho FE — Quản lý người dùng, của Ticket SCRUM-13

Backend của SCRUM-13 đã xong (UC-04 … UC-07). File này ghi những gì FE cần biết để thay
placeholder ở [`frontend/src/features/access/UsersPage.tsx`](../../frontend/src/features/access/UsersPage.tsx),
tập trung vào **những chỗ dễ làm sai mà lỗi lại trông như bug của backend**.

Hợp đồng API đầy đủ: [02-quan-ly-nguoi-dung.md](02-quan-ly-nguoi-dung.md). File này không nhắc
lại, chỉ nói phần FE.

**UC-08 (đặt lại mật khẩu) chưa có endpoint**, dù bản vẽ
[ResetUserPassword.png](../06-sequencediagram/ResetUserPassword.png) đã có. Đừng làm nút "Đặt lại
mật khẩu" rồi để đó — chưa có gì để gọi. Chờ SCRUM-15 (hạ tầng email).

---

## 1. Cái sẽ xoá nhầm tài khoản của người ta: `isActive` bắt buộc phải gửi

`PUT /api/users/{id}/status` nhận đúng một field:

```json
{ "isActive": false }
```

**camelCase, và không được thiếu.** Gửi sai sẽ nhận `400` kèm `errors.isActive`:

| Body gửi lên | Kết quả |
|---|---|
| `{"isActive": true}` | ✅ kích hoạt lại |
| `{"isActive": false}` | ✅ vô hiệu hoá |
| `{"is_active": true}` | ❌ `400` — snake_case không khớp |
| `{"active": true}` | ❌ `400` — sai tên field |
| `{}` | ❌ `400` |

Trước đây backend để kiểu `bool` không nullable, nghĩa là cả ba dòng ❌ ở trên đều **bind thành
`false`** — tức là vô hiệu hoá người ta, huỷ hết phiên đăng nhập của họ, ghi `user.deactivated` vào
audit log vĩnh viễn, rồi trả về `200 MSG11 "thành công"`. FE không có cách nào biết. Đã sửa thành
bắt buộc khai báo tường minh, nhưng **bạn vẫn phải gửi đúng tên field** — `400` bây giờ là bạn đang
gửi sai.

---

## 2. Vẫn là chữ hoa chữ thường, y như SCRUM-11

`role` trả về **PascalCase**: `"Admin"`, `"Chair"`, `"Secretary"`, `"Member"`.

Xem lại mục 1 của [01-xac-thuc-note-cho-fe.md](01-xac-thuc-note-cho-fe.md) — cùng một cái bẫy,
cùng một cách xử lý. Nếu đã chọn cách nào ở SCRUM-46 thì giữ nguyên cách đó ở đây.

Khi **gửi lên** thì backend nhận không phân biệt hoa thường (`"chair"` cũng được), nhưng vẫn nên
gửi đúng PascalCase cho nhất quán. Role không nằm trong enum → `400` kèm `errors.role`, **không**
phải `400` trống từ model binder.

---

## 3. Ba kiểu lỗi, ba cách hiển thị khác nhau

Cột `message` dưới đây là **chuỗi backend trả về nguyên văn**, không phải mô tả — nhưng đừng khớp
theo nó, xem cuối mục này.

| HTTP | `code` | `message` backend trả | FE nên làm gì |
|---|---|---|---|
| `400` | `MSG09` | `Thông tin tài khoản không hợp lệ.` | Đọc `errors`, tô đỏ **từng field** |
| `409` | `MSG08` | `Email đã được sử dụng.` | Báo ở field email |
| `409` | `MSG12` | `Không thể đổi trạng thái tài khoản này.` | Toast / dialog, xem mục 4 |
| `404` | *(không có body)* | — | Refresh lại danh sách |
| `403` | *(không có body)* | — | Xem mục 5 |

`errors` là object `field → câu tiếng Việt`, chỉ có ở `400`:

```json
{
  "code": "MSG09",
  "message": "Thông tin tài khoản không hợp lệ.",
  "errors": {
    "fullName": "Họ tên không được để trống.",
    "email": "Email không hợp lệ.",
    "password": "Mật khẩu phải có ít nhất 12 ký tự."
  }
}
```

Tên key trong `errors` khớp đúng tên field trong request (`fullName`, `email`, `password`, `role`,
`isActive`), nên map thẳng vào form được.

Backend trả **tất cả field sai cùng lúc** — đừng chỉ hiện cái đầu tiên.

**Khớp theo `code`, không khớp theo `message`** — như SCRUM-11.

---

## 4. `MSG12` có ba nguyên nhân khác hẳn nhau

Cùng một `code`, nhưng người dùng cần hiểu khác nhau. Backend cố tình không tách mã (đặc tả UC-07
chỉ có một mã cho nhánh này), nên **FE tự suy ra từ ngữ cảnh** — bạn đã có sẵn dữ liệu để biết:

| Nguyên nhân | FE biết được bằng cách nào | Nên hiển thị |
|---|---|---|
| Tài khoản **đã ở đúng trạng thái đó** | So với `isActive` trong danh sách đang hiển thị | "Tài khoản đã ở trạng thái này rồi." + refresh danh sách |
| Quản trị viên **nhắm vào chính mình** | `id` trùng với người đang đăng nhập | Tốt nhất là **ẩn/disable nút** ngay từ đầu |
| Sẽ **không còn quản trị viên nào** | Đếm số tài khoản `Admin` đang `isActive` trong danh sách | "Không thể vô hiệu hoá quản trị viên cuối cùng." |

Cái thứ ba cũng áp dụng cho `PUT /api/users/{id}` khi **đổi vai trò** (không phải khi sửa tên/email):
hạ vai trò của quản trị viên cuối cùng cũng trả `MSG12`.

Gợi ý: disable sẵn nút ở hai trường hợp đầu thì người dùng không bao giờ chạm vào lỗi. Nhưng
**vẫn phải xử lý `MSG12`** — hai quản trị viên cùng mở một danh sách thì FE không thể biết trước.

---

## 5. `403` giữa chừng phiên làm việc là chuyện bình thường, đừng coi là bug

Mọi endpoint `/api/users` đọc lại quyền của **người đang gọi** từ database ở mỗi request. Nghĩa là
đang dùng dở mà bị quản trị viên khác vô hiệu hoá hoặc hạ vai trò thì request tiếp theo trả `403`
ngay, **không chờ** access token hết hạn.

Đây là chủ ý (chi tiết ở mục "Người gọi được kiểm lại mỗi request" trong
[02-quan-ly-nguoi-dung.md](02-quan-ly-nguoi-dung.md)).

Hiểu `403` là **"tài khoản này không còn được vào đây"**, không phải "phiên đã chết". Ba nguyên
nhân, và chúng khác nhau ở chỗ phiên còn sống hay không:

| Nguyên nhân | Refresh token | Đăng nhập lại có vào được `/api/users` không? |
|---|---|---|
| Bị vô hiệu hoá | đã bị huỷ | Không — đăng nhập cũng bị chặn |
| Bị đổi vai trò | đã bị huỷ | Tuỳ vai trò mới |
| `role_capabilities` bị cấu hình lại, `AdminUsers` chuyển sang role khác | **vẫn còn** | **Không** — đăng nhập vẫn được nhưng `/api/users` vẫn `403` |

Dòng thứ ba là chỗ dễ làm FE rơi vào vòng lặp khó chịu: đá về trang đăng nhập → đăng nhập thành
công → vào lại màn hình → `403` → lại đá ra. Nên:

- `403` trên `/api/users/*` → **đá về trang chủ / màn hình họ còn quyền**, kèm thông báo
  "Quyền truy cập đã thay đổi." Chỉ xoá phiên và bắt đăng nhập lại khi `/api/auth/me` cũng hỏng.
- **Đừng** retry. **Đừng** gọi `/api/auth/refresh` để cứu — refresh không cấp thêm quyền, và ở hai
  nguyên nhân đầu thì refresh token cũng đã bị huỷ rồi.
- Sau khi đăng nhập lại, lọc menu bằng `can()` trên `capabilities` mới — nếu không còn `AdminUsers`
  thì đừng hiện mục quản lý người dùng nữa.

Phân biệt với `401`: `401` là token hỏng/hết hạn (xử lý bằng refresh như SCRUM-11), `403` là tài
khoản không còn được phép.

**`403` và `404` đều không có body** — `res.json()` sẽ throw. Cùng cái bẫy đã ghi ở mục 2 của
[01-xac-thuc-note-cho-fe.md](01-xac-thuc-note-cho-fe.md): chỉ parse body khi thật sự có.

---

## 6. Shape của response

`GET /api/users` trả **mảng phẳng**, không bọc trong `{ data: ... }`, **không phân trang**:

```json
[
  {
    "id": "01999f2e-…",
    "fullName": "Lê Thành Viên",
    "email": "thanhvien@trustcabinet.local",
    "role": "Member",
    "isActive": true,
    "hasPassword": true,
    "hasGoogleLink": false,
    "createdAt": "2026-09-19T15:07:18+00:00"
  }
]
```

Đã **sắp xếp sẵn** theo họ tên (collation tiếng Việt của PostgreSQL — "Đ" đúng chỗ, khác với
`Array.prototype.sort()` của JS). Sort lại ở FE sẽ làm hỏng thứ tự đó.

Danh sách **bao gồm cả tài khoản đã vô hiệu hoá** — UC-07 kích hoạt lại từ chính danh sách này. FE
tự lọc/gắn badge, đừng mong backend ẩn đi.

`hasPassword` / `hasGoogleLink` chỉ cho biết **có tồn tại hay không**, đủ để hiện "cách đăng nhập".
Backend không bao giờ trả `passwordHash` hay `googleSubjectId` — đừng đi tìm.

`POST` và `PUT` trả về **khác shape** với `GET`, có bọc:

```json
{ "code": "MSG07", "message": "…", "user": { …như trên… } }
```

`user` là trạng thái **sau** thay đổi, dùng thẳng để cập nhật danh sách, không cần gọi lại `GET`.

---

## 7. Mấy thứ vụn nhưng mất thời gian

- **`POST` trả `201`**, không phải `200` — nếu check `res.status === 200` thì sẽ trượt. Header
  `Location` có sẵn đường dẫn tài khoản mới.
- **Mật khẩu tối thiểu 12 ký tự.** Validate ở FE cho đỡ round trip, nhưng backend vẫn kiểm lại.
- **`PUT /api/users/{id}` không nhận `password`.** Gửi lên cũng bị bỏ qua. Đổi mật khẩu là UC-08.
- **`PUT /api/users/{id}` không nhận `isActive`.** Gửi lên cũng bị bỏ qua — đổi trạng thái phải
  dùng endpoint `/status` riêng. Đây là cố ý, có test chặn.
- **Email so sánh không phân biệt hoa thường** (cột `citext`). `An@x.vn` và `an@x.vn` là trùng
  nhau → `MSG08`.
- **Khoảng trắng đầu/cuối được cắt tự động** ở `fullName` và `email`.
- **Toàn bộ `/api/users` cần capability `AdminUsers`.** Người không có sẽ nhận `403`. Nhớ lọc menu
  bằng `can()`, nhưng nhớ luôn: ẩn nút không phải là bảo mật — backend vẫn kiểm lại mọi request.
