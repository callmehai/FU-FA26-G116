# Note cho FE — Quản lý người dùng, của Ticket SCRUM-13

Backend của SCRUM-13 đã xong (UC-05 … UC-06c), cùng UC-07 (đặt lại mật khẩu) và UC-08 (gán vai trò). File này ghi những gì FE cần biết để thay
placeholder ở [`frontend/src/features/access/UsersPage.tsx`](../../frontend/src/features/access/UsersPage.tsx),
tập trung vào **những chỗ dễ làm sai mà lỗi lại trông như bug của backend**.

Hợp đồng API đầy đủ: [02-quan-ly-nguoi-dung.md](02-quan-ly-nguoi-dung.md). File này không nhắc
lại, chỉ nói phần FE.

**UC-07 (đặt lại mật khẩu) đã xong cả BE lẫn FE** — nút trên màn hình này và trang công khai
`/reset-password`. Xem [mục 8](#8-uc-07--đặt-lại-mật-khẩu). UC-08 không có endpoint mới, xem
[mục 9](#9-uc-08--gán--đổi-vai-trò).

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

## 2. Vai trò là `roleId` + `roleName` (đổi từ `SCRUM-63`)

Trước `SCRUM-63` vai trò là chuỗi enum PascalCase (`"role": "Admin"`). Giờ vai trò là dòng của bảng
`roles` ([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)), nên:

- **Response** trả `roleId` (Guid) và `roleName` (tên hiển thị, ví dụ `"Thành viên"`). Hiển thị
  `roleName`, bỏ bảng `ROLE_LABEL` ở FE — vai trò quản trị viên tự tạo không có trong bảng đó.
- **Request** (`POST`, `PUT /api/users/{id}`) gửi `roleId`. Danh sách để chọn lấy từ
  `GET /api/roles` ([04-quan-ly-vai-tro.md](04-quan-ly-vai-tro.md)), không hardcode bốn vai trò.
- `roleId` thiếu, không phải Guid, hoặc không tồn tại → `400` `MSG09` kèm `errors.roleId`
  (`Vai trò không tồn tại.`), **không** phải `400` trống từ model binder.

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

Tên key trong `errors` khớp đúng tên field trong request (`fullName`, `email`, `password`, `roleId`,
`isActive`), nên map thẳng vào form được.

Backend trả **tất cả field sai cùng lúc** — đừng chỉ hiện cái đầu tiên.

**Khớp theo `code`, không khớp theo `message`** — như SCRUM-11.

---

## 4. `MSG12` có ba nguyên nhân khác hẳn nhau

Cùng một `code`, nhưng người dùng cần hiểu khác nhau. Backend cố tình không tách mã (đặc tả UC-06c
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
| Bị đổi vai trò | **vẫn còn** (SCRUM-105) — FE nhận `capabilitiesChanged` và tự ẩn menu | Tuỳ vai trò mới |
| `role_capabilities` bị cấu hình lại, `AdminUsers` chuyển sang role khác | **vẫn còn** | **Không** — đăng nhập vẫn được nhưng `/api/users` vẫn `403` |

Dòng thứ ba là chỗ dễ làm FE rơi vào vòng lặp khó chịu: đá về trang đăng nhập → đăng nhập thành
công → vào lại màn hình → `403` → lại đá ra. Nên:

- `403` trên `/api/users/*` → **đá về trang chủ / màn hình họ còn quyền**, kèm thông báo
  "Quyền truy cập đã thay đổi." Chỉ xoá phiên và bắt đăng nhập lại khi `/api/auth/me` cũng hỏng.
- **Đừng** retry. **Đừng** gọi `/api/auth/refresh` để cứu — refresh không cấp thêm quyền, và khi bị vô
  hiệu hoá thì refresh token cũng đã bị huỷ rồi.
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
    "roleId": "01a0ce78-7204-7558-b181-e908a08e83a3",
    "roleName": "Thành viên",
    "isActive": true,
    "hasPassword": true,
    "hasGoogleLink": false,
    "createdAt": "2026-09-19T15:07:18+00:00"
  }
]
```

Đã **sắp xếp sẵn** theo họ tên (collation tiếng Việt của PostgreSQL — "Đ" đúng chỗ, khác với
`Array.prototype.sort()` của JS). Sort lại ở FE sẽ làm hỏng thứ tự đó.

Danh sách **bao gồm cả tài khoản đã vô hiệu hoá** — UC-06c kích hoạt lại từ chính danh sách này. FE
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
- **`PUT /api/users/{id}` không nhận `password`.** Gửi lên cũng bị bỏ qua. Đổi mật khẩu là UC-07,
  và quản trị viên không bao giờ tự đặt mật khẩu cho người khác — chỉ gửi liên kết (mục 8).
- **`PUT /api/users/{id}` không nhận `isActive`.** Gửi lên cũng bị bỏ qua — đổi trạng thái phải
  dùng endpoint `/status` riêng. Đây là cố ý, có test chặn.
- **Email so sánh không phân biệt hoa thường** (cột `citext`). `An@x.vn` và `an@x.vn` là trùng
  nhau → `MSG08`.
- **Khoảng trắng đầu/cuối được cắt tự động** ở `fullName` và `email`.
- **Toàn bộ `/api/users` cần capability `AdminUsers`.** Người không có sẽ nhận `403`. Nhớ lọc menu
  bằng `can()`, nhưng nhớ luôn: ẩn nút không phải là bảo mật — backend vẫn kiểm lại mọi request.

---

## 8. UC-07 — Đặt lại mật khẩu

Backend và FE đều đã xong. FE nằm ở
[`UsersPage.tsx`](../../frontend/src/features/access/UsersPage.tsx) (nút, mục 8.1) và
[`ResetPasswordPage.tsx`](../../frontend/src/features/auth/ResetPasswordPage.tsx) (trang công khai,
mục 8.2). Những chỗ FE làm khác gợi ý bên dưới được ghi trong khung **FE đã làm** ở cuối mỗi mục.

### 8.1 Nút "Đặt lại mật khẩu" trên màn hình quản lý người dùng

- Gọi `POST /api/users/{id}/password-reset`, **không có body**. Đặc tả bắt **hộp xác nhận trước**
  (bước 2–3): mỗi lần gọi là một email gửi đi và liên kết trước đó chết.
- Ẩn/disable nút với tài khoản `isActive: false` **hoặc** `hasPassword: false` (chỉ đăng nhập bằng
  Google — BA chốt trong SCRUM-13: không có mật khẩu thì không có gì để đặt lại) — backend sẽ trả
  `409 MSG14` nếu vẫn gọi.
- **Có thể mất tới vài chục giây** khi SMTP chậm hoặc không kết nối được (backend gửi đồng bộ, timeout
  theo `Email:SendAttemptTimeoutSeconds`). Hiện trạng thái loading trên nút, chặn bấm lặp.

| HTTP | `code` | FE nên làm gì |
|---|---|---|
| `200` | `MSG13` | Toast thành công. `user` trong body là tài khoản như shape UC-05 — không có gì đổi để cập nhật |
| `409` | `MSG14` | Toast: tài khoản đã bị vô hiệu hoá hoặc chưa có mật khẩu. Refresh lại dòng đó |
| `503` | `MSG15` | Toast lỗi "thử lại" — đây là lỗi máy chủ mail, không phải lỗi người dùng |
| `404` | *(không body)* | Refresh danh sách, như các endpoint khác |

`409 MSG14` **khác** `409 MSG12` — khớp theo `code`, đừng gom theo status.

> **FE đã làm:** nút là icon chìa khoá ở cột "Thao tác", **disable** (không ẩn) với tài khoản đã bị vô
> hiệu hoá — tooltip nói phải kích hoạt lại trước — và với tài khoản chưa có mật khẩu — tooltip nói
> tài khoản chỉ đăng nhập bằng Google; cột giữ nguyên độ rộng. Lỗi `MSG15`, `MSG14` và
> `404` hiện **trong hộp xác nhận** chứ không phải toast — đúng quy ước của `ConfirmDialog` (hộp thoại
> che toast); `MSG15` giữ nút "Gửi liên kết" để thử lại ngay. `MSG14` đọc lại tài khoản
> (`GET /api/users/{id}`), cập nhật dòng đó và khoá nút xác nhận, kèm lý do (đã bị vô hiệu hoá /
> chưa có mật khẩu). Quản trị viên gửi cho chính mình cũng được (backend cho phép), hộp xác nhận cảnh báo
> là mọi phiên của họ sẽ kết thúc khi dùng liên kết.

### 8.2 Trang công khai `/reset-password`

Email gửi liên kết dạng:

```
http://localhost:5173/reset-password#token=AbC…xyz
```

- Route **công khai** (không cần đăng nhập), nằm ngoài `AppShell`, cạnh trang đăng nhập. Thêm vào
  `lib/routes.ts`.
- **Token nằm trong fragment** (`window.location.hash`), không phải query string. Đọc bằng
  `new URLSearchParams(location.hash.slice(1)).get('token')`. Fragment không bao giờ được gửi lên
  server, nên đừng "chuyển" nó sang query string khi điều hướng. Đọc xong thì có thể xoá khỏi thanh
  địa chỉ bằng `history.replaceState`.
- Form: mật khẩu mới + nhập lại (so khớp ở FE), tối thiểu 12 ký tự. Gửi:

```json
POST /api/auth/password-reset
{ "token": "<từ fragment>", "newPassword": "…" }
```

| HTTP | `code` | FE nên làm gì |
|---|---|---|
| `200` | `MSG92` | Báo thành công, chuyển tới `/login`. **Không có token đăng nhập trong response** — người dùng tự đăng nhập bằng mật khẩu mới |
| `400` | `MSG94` | Tô đỏ field mật khẩu từ `errors.newPassword`. **Liên kết vẫn còn dùng được** — giữ form, cho sửa và gửi lại |
| `400` | `MSG93` | Liên kết chết (hết hạn 60 phút, đã dùng, có liên kết mới hơn, hoặc tài khoản bị khoá / không có mật khẩu). Ẩn form, hiện hướng dẫn liên hệ quản trị viên |

Hai mã `400` khác nhau hoàn toàn về cách xử lý — khớp theo `code`.

- Không có fragment `token` thì FE tự hiện `MSG129` ("bấm lại liên kết trong email"), khỏi gọi API.
- Đặt lại thành công sẽ **huỷ mọi refresh token** của người đó. Nếu chính trình duyệt này đang đăng
  nhập tài khoản đó, nó vẫn chạy tới khi access token hết hạn (tối đa 15 phút), rồi lần refresh tiếp
  theo ra `401 MSG01` — luồng đăng nhập lại như bình thường.
- Một liên kết đã gửi sẽ chết (→ `MSG93`) nếu quản trị viên vô hiệu hoá tài khoản, đổi email hoặc đổi
  vai trò sau đó, hoặc bấm gửi lại **thành công**. Gửi lại mà ra `503 MSG15` thì liên kết cũ vẫn
  dùng được — toast MSG15 không cần nói gì về liên kết cũ.
- Gọi qua `src/api/` (`api.post`), thêm type trong `src/types/` — như mọi endpoint khác.

> **FE đã làm:** trang nằm ngoài `RequireAuth` và `AppShell`, dùng khung của trang đăng nhập. Token
> đọc từ fragment rồi xoá khỏi thanh địa chỉ (`navigate(..., { replace: true })`); dán một liên kết mới
> vào **cùng tab** (chỉ đổi fragment, không tải lại trang) cũng nhận token mới. FE kiểm ≥ 12 ký tự và
> "nhập lại khớp" trước khi gửi. `MSG92` → toast thông điệp của backend rồi chuyển tới `/login`;
> nếu trình duyệt này đang giữ một phiên đăng nhập thì đăng xuất phiên đó trước, không thì `/login`
> sẽ đưa thẳng vào ứng dụng; đăng xuất không tới được server thì vẫn báo `MSG92`, kèm toast lỗi `MSG06`
> (phiên đó còn nguyên). `MSG93` → ẩn form, hiện `message` của backend, hướng dẫn liên hệ quản trị
> viên và nút về trang đăng nhập (focus vào nút). **Không có token → `MSG129`** (mã FE, không gọi API),
> khung vàng "hãy bấm lại liên kết trong email", không phải câu "hết hạn" của `MSG93`: token chỉ giữ
> trong bộ nhớ, giống access token, nên tải lại trang hoặc khôi phục thẻ sẽ rơi vào trường hợp này
> trong khi liên kết vẫn còn dùng được. `MSG94` → lỗi dưới ô mật khẩu mới, giữ nguyên form. Lỗi khác (5xx, mất mạng) → alert trên
> form, giữ nguyên những gì đã nhập.

---

## 9. UC-08 — Gán / đổi vai trò

**Không có endpoint mới.** Gán hay đổi vai trò là `PUT /api/users/{id}` với `roleId` mới, giữ nguyên
`fullName` và `email` hiện tại — chính form sửa của UC-06b, đã nối API. Nếu muốn có nút "Gán vai trò"
riêng như đặc tả mô tả, đó chỉ là một dialog chọn vai trò (`GET /api/roles`) rồi gọi đúng endpoint
này với hai field kia lấy từ `GET /api/users/{id}`.

Mã trả về giống UC-06b: `MSG10` thành công, `MSG09` kèm `errors.roleId` khi vai trò vừa bị xoá, `MSG12`
khi đó là quản trị viên cuối cùng (mục 4). Đổi vai trò **không** đăng xuất người được đổi (SCRUM-105):
họ nhận sự kiện `capabilitiesChanged`, thấy toast `MSG287` và menu đổi theo ngay. Liên kết đặt lại mật
khẩu đang chờ của họ vẫn dùng được. Đổi email thì vẫn đăng xuất và huỷ liên kết đó.
