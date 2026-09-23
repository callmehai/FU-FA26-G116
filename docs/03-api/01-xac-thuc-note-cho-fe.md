# Note cho FE — Đăng nhập và đăng xuất, của Ticket SCRUM-11

Backend của SCRUM-11 đã xong và đã merge vào nhánh `feat/authentication`. File này ghi những gì FE
cần biết để làm phần còn lại (SCRUM-46), tập trung vào **những chỗ dễ làm sai mà lỗi lại trông như
bug của backend**.

Hợp đồng API đầy đủ: [01-xac-thuc.md](01-xac-thuc.md). File này không nhắc lại, chỉ nói phần FE.

---

## 1. Cái sẽ làm bạn mất buổi chiều nếu không đọc: chữ hoa chữ thường

Backend trả `role` và `capabilities` theo **PascalCase**, còn
[`frontend/src/types/user.ts`](../../frontend/src/types/user.ts) hiện khai **snake_case**:

| | Backend trả về (thật) | FE đang khai |
|---|---|---|
| `role` | `"Secretary"` | `'secretary'` |
| `capabilities` | `["MeetingCreator", "MinutesEditor"]` | `['meeting_creator', 'minutes_editor']` |

Không khớp thì `ROLE_CAPABILITIES[role]` trả `undefined`, `can()` luôn `false`, sidebar trống rỗng
và **mọi route có `RequireCapability` render ra 404**. Nhìn y như backend cấp sai quyền, trong khi
backend hoàn toàn đúng.

Chọn một trong hai cách, đừng làm nửa vời:

- **Đổi type ở FE sang PascalCase** cho khớp backend (khuyến nghị — backend là nguồn sự thật, và
  enum trong DB cũng lưu dạng này), hoặc
- **Map một lần ở tầng API** (`api/auth.ts`), FE bên trong giữ snake_case.

Đừng map rải rác trong component. Chọn xong thì xoá `ROLE_CAPABILITIES` trong `types/user.ts` —
backend đã trả `capabilities` sẵn trong response, không cần bảng tra ở FE nữa.

---

## 2. Hai loại 401 khác nhau — parse mù là throw

| Endpoint | 401 có body? | Body |
|---|---|---|
| `POST /api/auth/login` | **Có** | `{"code":"MSG01","message":"Email hoặc mật khẩu không đúng."}` |
| `POST /api/auth/google` | **Có** | `{"code":"MSG01","message":"Đăng nhập bằng Google không thành công."}` |
| `POST /api/auth/refresh` | **Có** | như trên |
| `GET /api/auth/me` | **KHÔNG** | `Content-Length: 0` (middleware JWT trả, không qua code mình) |

`api/client.ts` hiện tại throw `ApiError` khi `!res.ok` và **không đọc body**, nên FE mất mất `code`.
Cần sửa `client.ts` để đính kèm body khi có, nhưng phải chịu được trường hợp body rỗng —
`res.json()` trên body rỗng sẽ throw và che mất lỗi thật.

**Khớp theo `code`, không khớp theo `message`.** Backend có thể sửa lại câu chữ tiếng Việt bất cứ
lúc nào; `code` là hợp đồng.

---

## 3. Mã thông báo: UC-01 và UC-02 đánh số KHÁC nhau

Đây là chủ ý theo đặc tả use case, không phải backend nhầm. Đừng "dọn" cho giống nhau.

| Tình huống | `/api/auth/login` | `/api/auth/google` |
|---|---|---|
| Sai thông tin / token Google không verify được | `MSG01` | `MSG01` |
| Tài khoản Google chưa được liên kết | — | `MSG03` |
| Tài khoản bị vô hiệu hoá | `MSG03` | **`MSG04`** |
| Không có quyền truy cập hệ thống | `MSG04` | **`MSG05`** |

Nghĩa là **không dùng chung một hàm map `code` → câu thông báo cho cả hai luồng.** `MSG03` ở
`/login` nghĩa là "tài khoản bị vô hiệu hoá", còn `MSG03` ở `/google` nghĩa là "chưa liên kết
Google" — hai câu hoàn toàn khác nhau. Tách hai bảng map.

Backend đã trả sẵn `message` tiếng Việt đúng cho từng trường hợp, nên cách an toàn nhất là **hiển
thị `message` từ response** và chỉ dùng `code` khi cần rẽ nhánh hành vi (ví dụ: `MSG03` ở `/google`
thì hiện thêm hướng dẫn "liên hệ quản trị viên để liên kết tài khoản").

---

## 4. Vòng đời token

```
login/google  →  accessToken (JWT, 15 phút)  +  refreshToken (opaque, 7 ngày)
                 accessTokenExpiresAt: "2026-09-21T08:14:48.381003+00:00"
```

- **Access token** gắn vào mọi request: `Authorization: Bearer <accessToken>`.
- **Refresh token** chỉ dùng cho `/api/auth/refresh` và `/api/auth/logout`, gửi trong **body**:
  `{"refreshToken":"..."}`.

**Refresh token dùng một lần.** Mỗi lần `/refresh` thành công trả về RT **mới** và RT cũ chết ngay.
Hai hệ quả bắt buộc phải xử lý:

- **Phải lưu lại RT mới** sau mỗi lần refresh. Quên là lần refresh sau 401.
- **Không được để hai request refresh chạy song song.** Nếu 3 request cùng nhận 401 và cùng gọi
  `/refresh` với cùng một RT, thì một cái thắng và hai cái kia nhận 401 → đăng xuất oan. Cần một
  promise refresh dùng chung (single-flight): request đầu tiên gọi refresh, các request còn lại chờ
  cùng promise đó.

Gợi ý: refresh **chủ động** dựa trên `accessTokenExpiresAt` (ví dụ còn 2 phút thì refresh trước),
thay vì đợi 401 rồi mới retry. Ít race hơn nhiều.

---

## 5. Đăng xuất (UC-03) — phần này gần như toàn bộ là việc của FE

Sequence diagram [SignOut.png](../06-sequencediagram/SignOut.png) chỉ có `User` và
`Authenticated Page`: hộp xác nhận, xoá JWT, `MSG01` nếu xoá lỗi. Backend không có participant nào.

Nhưng **vẫn phải gọi `POST /api/auth/logout`**, và gọi **trước** khi xoá token cục bộ:

```
1. Hiện hộp xác nhận            ← theo diagram
2. POST /api/auth/logout { refreshToken }   ← huỷ phiên phía server
3. Xoá token cục bộ
4. Điều hướng về /login
```

Bỏ bước 2 thì refresh token **vẫn sống tới 7 ngày** dù trình duyệt đã sạch — ai có bản copy của RT
đó vẫn đăng nhập lại được. Đây là tiêu chí nghiệm thu của SCRUM-11 ("đăng xuất huỷ phiên ở phía
server, không chỉ xoá ở trình duyệt"), nên đừng bỏ.

`/logout` **luôn** trả `204` không body, kể cả khi token đã hết hạn hoặc không tồn tại, và gọi hai
lần cũng không lỗi. Nên bước 3 và 4 cứ chạy dù bước 2 có thất bại vì mạng — đừng khoá người dùng
trong trạng thái không đăng xuất được.

Lưu ý: `/logout` chỉ huỷ phiên **của thiết bị đó**. Đăng xuất trên laptop không đăng xuất trên điện
thoại. Chưa có endpoint "đăng xuất mọi thiết bị".

---

## 6. Đăng nhập Google — ba chỗ dễ sai

Dùng Google Identity Services. Luồng: browser lấy ID token → POST lên backend → backend verify.

```
POST /api/auth/google
{ "idToken": "<credential từ GIS>" }
```

**a) Phải gửi ID token, không phải access token.** GIS trả về field `credential` — đó là ID token
(JWT, ba phần cách nhau bởi dấu `.`). Gửi cái đó. Access token dạng `ya29...` sẽ luôn `MSG01` vì
không phải JWT.

**b) `client_id` phải đúng cái này**, nếu không token có `aud` khác và backend từ chối (`MSG01`):

```
1024662324975-ma0lj2lpl48kdl3bb56aj53nk3hsnd5f.apps.googleusercontent.com
```

Đã có trong `appsettings.json` (không phải secret). Nếu cần, xin backend expose qua một endpoint
config thay vì hardcode ở FE — hiện **chưa có** endpoint đó, cần thì nói.

**c) Phải chạy ở `http://localhost:5173`.** Origin khác thì Google chặn ngay trước khi tới backend.
Gọi đường dẫn tương đối `/api/auth/google` (Vite proxy sẵn), **không** hardcode `localhost:5225`.

### Quan trọng: lần đầu chắc chắn nhận MSG03

Backend tra tài khoản theo claim `sub` của Google. Chưa có tài khoản nào mang `sub` đó thì nó tự
liên kết tài khoản **có email trùng Gmail của bạn** (đang hoạt động, chưa liên kết) — xem
[ADR-0009](../05-decisions/0009-tu-lien-ket-google-lan-dau-theo-email.md). Cách nhanh nhất: vào
Quản lý người dùng, tạo tài khoản bằng đúng Gmail của bạn, rồi bấm nút Google — vào luôn, **không**
cần SQL.

Tài khoản seed (`thuky@trustcabinet.local`, …) thì email không phải Gmail của bạn, nên lần đầu bấm
nút Google sẽ nhận `MSG03` — **đúng, không phải bug**. Muốn đăng nhập Google vào tài khoản seed:

1. Bấm nút Google, đăng nhập (Gmail của bạn phải nằm trong danh sách *test users* của Google Console)
2. Nhận `MSG03`
3. Log ID token ra, dán vào [jwt.io](https://jwt.io), copy field `sub` (chuỗi ~21 số)
4. Chạy SQL:
   ```sql
   update users set google_subject_id = '<sub>'
   where email = 'thuky@trustcabinet.local';
   ```
5. Bấm lại → `200`

Liên kết bằng SQL thì Gmail **không cần** trùng email tài khoản nội bộ: bạn link `sub` vào tài
khoản nào thì đăng nhập vào tài khoản đó. Đã liên kết rồi thì backend chỉ so theo `sub`.

---

## 7. Lưu token ở đâu

Hiện backend trả refresh token **trong response body**, chưa phải cookie `HttpOnly` — đây là món nợ
đã ghi nhận, sẽ chuyển sang cookie khi FE xong.

Trong lúc đó, khuyến nghị:

- **Access token**: giữ trong memory (React state / context). Nó chỉ sống 15 phút, mất khi F5 thì
  refresh lại là có ngay.
- **Refresh token**: `localStorage` là lựa chọn thực tế duy nhất nếu muốn giữ phiên qua F5 — nhưng
  hiểu rõ đánh đổi: bất kỳ lỗi XSS nào trên trang cũng đọc được nó. Đừng lưu vào đó bất cứ thứ gì
  khác liên quan phiên.
- **`AuthProvider` hiện lưu cả object user vào `localStorage`** dưới key `trustcabinet.session`.
  Bỏ cách đó: user thật lấy từ `/api/auth/me` hoặc từ response `login`, không tin dữ liệu trong
  localStorage vì người dùng sửa được (sửa `role` thành `admin` chẳng hạn) — UI sẽ hiện menu admin
  dù backend vẫn chặn. Không phải lỗ hổng thật, nhưng là bug UI khó hiểu.

---

## 8. Khôi phục phiên sau khi F5

`GET /api/auth/me` với `Authorization: Bearer <accessToken>` trả về đúng object `user` như trong
response login:

```json
{ "id":"...", "email":"...", "fullName":"...", "role":"Secretary",
  "capabilities":["MeetingCreator","MinutesEditor"] }
```

Đọc trực tiếp từ claim trong token, không truy vấn database — nên gọi nó khi khởi động app là rẻ.

`AuthProvider` hiện đọc `localStorage` đồng bộ nên không có trạng thái "đang khôi phục". Khi chuyển
sang gọi API thật thì **sẽ có**, và `RequireAuth` cần xử lý: trong lúc chờ `/me` trả về thì
`user === null`, nếu điều hướng ngay về `/login` thì mỗi lần F5 người dùng bị đẩy ra trang đăng nhập
rồi mới quay lại. Cần trạng thái thứ ba (`loading`) chứ không chỉ `user | null`.

---

## 9. Những gì phải xoá

Toàn bộ phần giả lập trong [`frontend/src/features/auth/`](../../frontend/src/features/auth/):

- `AuthProvider.tsx` — `signIn(email, role)` hiện bịa ra user và nhận mọi thông tin. Đổi thành gọi
  `/api/auth/login`.
- `authContext.ts` — chữ ký `signIn: (email, role) => void` phải thành
  `(email, password) => Promise<...>`, và cần thêm `signInWithGoogle`, trạng thái `loading`, cách
  báo lỗi (`code` + `message`).
- `LoginPage.tsx` — **ô chọn vai trò phải xoá**. Vai trò do backend quyết định, không phải người
  dùng chọn. Cả đoạn cảnh báo "đăng nhập đang được giả lập" cũng xoá.
- `types/user.ts` — `ROLE_CAPABILITIES` không cần nữa (xem mục 1).

---

## 10. Chưa có ở backend, cần thì nói

- **Rate limit cho `/api/auth/login`** — chưa có, nên đừng dựa vào backend để chặn thử sai liên
  tục. FE có thể tự làm mềm (disable nút vài giây sau nhiều lần sai) nhưng đó **không phải** bảo
  mật, chỉ là UX.
- **Endpoint trả Google `client_id`** — chưa có.
- **Đăng xuất mọi thiết bị** — chưa có.
- **Đổi mật khẩu / quên mật khẩu** — không thuộc SCRUM-11.

---

## Tài khoản để test

Mật khẩu chung (chỉ môi trường Development): `TrustCabinet@2026`

| Email | `role` trả về | `capabilities` trả về |
|---|---|---|
| `chutoa@trustcabinet.local` | `Chair` | `["MeetingCreator","MeetingApprover"]` |
| `thuky@trustcabinet.local` | `Secretary` | `["MeetingCreator","MinutesEditor"]` |
| `thanhvien@trustcabinet.local` | `Member` | `[]` |
| `quantri@trustcabinet.local` | `Admin` | `["AdminUsers"]` |

**`Member` có `capabilities` rỗng nhưng vẫn đăng nhập được bình thường** — đó là chủ ý (thành viên
dự họp, đọc tài liệu, biểu quyết, không cần capability nào). Đừng coi mảng rỗng là lỗi hoặc là
"chưa có quyền vào hệ thống".

Chạy backend:

```bash
docker compose up -d
dotnet run --project backend/TrustCabinet.Web   # :5225
cd frontend && npm run dev                      # :5173
```

Mở `http://localhost:5173`, không phải `:5225`.
