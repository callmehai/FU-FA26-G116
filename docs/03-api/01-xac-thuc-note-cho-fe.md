# Note cho FE — Đăng nhập và đăng xuất, của Ticket SCRUM-11

Backend của SCRUM-11 đã xong và đã merge vào nhánh `feat/authentication`. File này ghi những gì FE
cần biết để làm phần còn lại (SCRUM-46), tập trung vào **những chỗ dễ làm sai mà lỗi lại trông như
bug của backend**.

Hợp đồng API đầy đủ: [01-xac-thuc.md](01-xac-thuc.md). File này không nhắc lại, chỉ nói phần FE.

> **Đổi từ `SCRUM-63`** ([04-quan-ly-vai-tro.md](04-quan-ly-vai-tro.md)): object `user` không còn
> `"role": "Secretary"` mà trả **`roleId`** (Guid) + **`roleName`** (tên hiển thị, ví dụ `"Thư ký"`).
> Bỏ bảng `ROLE_LABEL` ở FE, hiển thị `roleName`. Nhánh "không có quyền truy cập hệ thống" (trước
> đây `MSG04` ở `/login`, `MSG05` ở `/google`) **không còn được trả về**. Phần về `role` ở mục 1 dưới đây là lịch sử.

> **Đổi: refresh token chuyển sang cookie `HttpOnly`** (comment SCRUM-11, 24/09 — cả ba bước gộp
> một PR): response **không còn `refreshToken`**, `/refresh` và `/logout` **không nhận body**. FE đã
> sửa theo, xem [mục 0](#0-refresh-token-nằm-trong-cookie). Mục 4, 5, 7 đã cập nhật theo.

---

## 0. Refresh token nằm trong cookie

Hợp đồng đầy đủ ở [01-xac-thuc.md](01-xac-thuc.md#refresh-token-nằm-trong-cookie-tc_refresh_token).
Cách FE đang làm, trong [`session.ts`](../../frontend/src/features/auth/session.ts):

- **FE không đọc, không ghi cookie.** Nó `HttpOnly`, JavaScript không thấy. Trình duyệt tự lưu khi
  `/login`, `/google`, `/refresh` trả về, và tự gửi kèm mọi request tới `/api/auth/*` (cookie có
  `Path=/api/auth`). `fetch` cùng origin mặc định `credentials: 'same-origin'` nên không cần thêm
  option gì; Vite proxy chuyển tiếp header `Cookie` bình thường.
- **`/refresh` và `/logout` gọi không body.** Gửi `refreshToken` trong body cũng bị bỏ qua.
- **`localStorage` chỉ giữ cờ `trustcabinet.signedIn`**, không phải credential. Nó cho biết có nên
  thử `/refresh` khi F5 hay không (khách chưa đăng nhập khỏi tốn một request `401`), phân biệt
  "phiên hết hạn" với "chưa từng đăng nhập", và báo cho tab khác khi một tab đăng xuất (sự kiện
  `storage`). Không đọc được `localStorage` thì cứ thử `/refresh`.
- **Key cũ** `trustcabinet.refreshToken` và `trustcabinet.session` bị xoá khi khởi động. Ai đang
  đăng nhập từ trước thay đổi này chỉ có RT trong `localStorage`, không có cookie, nên phải đăng
  nhập lại **một lần**.
- **Giữ single-flight + Web Lock cho refresh.** Backend **phát hiện RT bị dùng lại**: gửi lại một RT
  đã dùng quá 60 giây sau khi dùng thì **mọi phiên của tài khoản đó bị huỷ**, trên mọi thiết bị;
  trong 60 giây thì chỉ `401` và cookie bị xoá. Hai tab refresh song song với cùng một cookie là
  đúng tình huống đó. Đừng bỏ Web Lock, và dev chỉ mở `:5173` — cookie không tách theo port, còn
  Web Lock thì có, nên mở thêm `:5225` là hai bên không còn chờ nhau.
- **Cookie có `Secure`.** Chrome và Firefox chấp nhận trên `http://localhost` (coi là ngữ cảnh an
  toàn). Safari có thể không lưu cookie `Secure` qua `http://localhost` — nếu phiên mất sau F5 trên
  Safari lúc dev thì đó là lý do, test bằng Chrome/Firefox. HTTP ở địa chỉ khác localhost (mở bằng
  IP LAN khi chạy `vite --host`, ví dụ từ điện thoại) thì cookie luôn bị bỏ: đăng nhập được, F5 là
  mất phiên.
- **Đăng xuất phải tới được server, và chạy trong Web Lock của refresh** — xem mục 5.
- **Đồng bộ giữa các tab qua cờ `trustcabinet.signedIn`** (sự kiện `storage`): tab này đăng xuất
  thì tab khác về màn đăng nhập; tab này đăng nhập thì tab khác đang không có phiên (ví dụ đứng ở
  `/login`) tự gọi `/refresh` bằng cookie vừa được đặt và vào luôn, không phải F5.
- **`/refresh` thất bại** trả `MSG01` với câu "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
  (khác câu của `/login`). FE hiện đang tự hiển thị thông báo hết phiên nên không dùng câu này.

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
| `POST /api/auth/google` | **Có** | `{"code":"MSG03","message":"Đăng nhập bằng Google không thành công."}` |
| `POST /api/auth/refresh` | **Có** | `{"code":"MSG01","message":"Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."}` |
| `GET /api/auth/me` | **KHÔNG** | `Content-Length: 0` (middleware JWT trả, không qua code mình) |

`api/client.ts` hiện tại throw `ApiError` khi `!res.ok` và **không đọc body**, nên FE mất mất `code`.
Cần sửa `client.ts` để đính kèm body khi có, nhưng phải chịu được trường hợp body rỗng —
`res.json()` trên body rỗng sẽ throw và che mất lỗi thật.

**Khớp theo `code`, không khớp theo `message`.** Backend có thể sửa lại câu chữ tiếng Việt bất cứ
lúc nào; `code` là hợp đồng.

---

## 3. Mã thông báo: đánh số chung toàn dự án

Mỗi mã chỉ mang một nghĩa trên mọi endpoint — xem [danh mục mã](00-danh-muc-ma-thong-bao.md).
Trước đây UC-01 và UC-02 mỗi bên tự đánh số từ `MSG01`, nên cùng một mã mang hai nghĩa; nay đã
đánh lại.

| Tình huống | `/api/auth/login` | `/api/auth/google` | `/api/auth/refresh` |
|---|---|---|---|
| Sai email hoặc mật khẩu / phiên hết hạn | `MSG01` | — | `MSG01` |
| Tài khoản bị vô hiệu hoá | `MSG02` | `MSG02` | `MSG02` |
| Token Google không verify được | — | `MSG03` | — |
| Tài khoản Google chưa được liên kết | — | `MSG05` | — |
| ~~Không có quyền truy cập hệ thống~~ | bỏ từ `SCRUM-63` | bỏ từ `SCRUM-63` | — |

`MSG04` ("Không xác minh được tài khoản Google") có trong đặc tả nhưng backend chưa trả riêng: token
nào không verify được đều là `MSG03`. `MSG06` ("Không thể đăng xuất") do FE tự hiển thị.

Backend đã trả sẵn `message` tiếng Việt đúng cho từng trường hợp, nên cách an toàn nhất là **hiển
thị `message` từ response** và chỉ dùng `code` khi cần rẽ nhánh hành vi (ví dụ: `MSG05` ở `/google`
thì hiện thêm hướng dẫn "liên hệ quản trị viên để liên kết tài khoản").

---

## 4. Vòng đời token

```
login/google  →  accessToken (JWT, 15 phút, trong body)  +  cookie tc_refresh_token (opaque, 7 ngày)
                 accessTokenExpiresAt: "2026-09-21T08:14:48.381003+00:00"
```

- **Access token** gắn vào mọi request: `Authorization: Bearer <accessToken>`.
- **Refresh token** chỉ dùng cho `/api/auth/refresh` và `/api/auth/logout`. Nằm trong cookie
  `tc_refresh_token` (xem mục 0), trình duyệt tự gửi.

**Refresh token dùng một lần.** Mỗi lần `/refresh` thành công trả về RT **mới** và RT cũ chết ngay.
Hai hệ quả bắt buộc phải xử lý:

- **RT mới thay RT cũ** sau mỗi lần refresh — trình duyệt tự ghi đè cookie, FE không phải làm gì.
- **Không được để hai request refresh chạy song song.** Nếu 3 request cùng nhận 401 và cùng gọi
  `/refresh` với cùng một RT, thì backend coi là RT bị đánh cắp và **huỷ mọi phiên** → đăng xuất
  oan trên mọi thiết bị. Cần một
  promise refresh dùng chung (single-flight): request đầu tiên gọi refresh, các request còn lại chờ
  cùng promise đó.

Gợi ý: refresh **chủ động** dựa trên `accessTokenExpiresAt` (ví dụ còn 2 phút thì refresh trước),
thay vì đợi 401 rồi mới retry. Ít race hơn nhiều.

---

## 5. Đăng xuất (UC-03) — phần này gần như toàn bộ là việc của FE

Sequence diagram [UC03_SignOut.png](../06-sequencediagram/auth/UC03_SignOut.png) chỉ có `User` và
`Authenticated Page`: hộp xác nhận, xoá JWT, `MSG06` nếu xoá lỗi. Backend không có participant nào.

Nhưng **vẫn phải gọi `POST /api/auth/logout`**, và gọi **trước** khi xoá token cục bộ:

```
1. Hiện hộp xác nhận            ← theo diagram
2. POST /api/auth/logout (không body)   ← huỷ phiên phía server + xoá cookie
3. Xoá token cục bộ
4. Điều hướng về /login
```

Bỏ bước 2 thì refresh token **vẫn sống tới 7 ngày** dù trình duyệt đã sạch — ai có bản copy của RT
đó vẫn đăng nhập lại được. Đây là tiêu chí nghiệm thu của SCRUM-11 ("đăng xuất huỷ phiên ở phía
server, không chỉ xoá ở trình duyệt"), nên đừng bỏ.

`/logout` **luôn** trả `204` không body, kể cả khi token đã hết hạn hoặc không tồn tại, và gọi hai
lần cũng không lỗi.

**Bước 2 thất bại vì mạng thì dừng, hiện `MSG06`.** Cookie `HttpOnly` chỉ server xoá được: nếu FE
vẫn xoá phía mình rồi chuyển về `/login`, cookie còn nguyên và F5 là đăng nhập lại ngay — người
dùng tưởng đã đăng xuất mà không phải. `signOut()` trong `session.ts` trả `false` trong trường hợp
này và giữ nguyên phiên để người dùng bấm lại. Mọi chỗ gọi `signOut()` phải xử lý `false` — màn
Vai trò (nút "Đăng nhập lại") hiện toast `MSG06`, menu tài khoản hiện lỗi trong hộp xác nhận, trang
`/reset-password` (đặt lại thành công trên trình duyệt đang đăng nhập) hiện toast `MSG06` sau toast `MSG92`.

**`signOut()` chờ refresh đang chạy và giữ Web Lock của refresh.** Nếu `/logout` đua với một
`/refresh` cùng RT, server xoay RT mà logout định huỷ, và `Set-Cookie` của refresh có thể tới sau
lệnh xoá cookie của logout — người dùng thấy màn đăng nhập nhưng F5 là vào lại.

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
(JWT, ba phần cách nhau bởi dấu `.`). Gửi cái đó. Access token dạng `ya29...` sẽ luôn `MSG03` vì
không phải JWT.

**b) `client_id` phải đúng cái này**, nếu không token có `aud` khác và backend từ chối (`MSG03`):

```
1024662324975-ma0lj2lpl48kdl3bb56aj53nk3hsnd5f.apps.googleusercontent.com
```

Đã có trong `appsettings.json` (không phải secret). Nếu cần, xin backend expose qua một endpoint
config thay vì hardcode ở FE — hiện **chưa có** endpoint đó, cần thì nói.

**c) Phải chạy ở `http://localhost:5173`.** Origin khác thì Google chặn ngay trước khi tới backend.
Gọi đường dẫn tương đối `/api/auth/google` (Vite proxy sẵn), **không** hardcode `localhost:5225`.

### Quan trọng: lần đầu chắc chắn nhận MSG05

Backend tra tài khoản theo claim `sub` của Google. Chưa có tài khoản nào mang `sub` đó thì nó tự
liên kết tài khoản **có email trùng Gmail của bạn** (đang hoạt động, chưa liên kết) — xem
[ADR-0009](../05-decisions/0009-tu-lien-ket-google-lan-dau-theo-email.md). Cách nhanh nhất: vào
Quản lý người dùng, tạo tài khoản bằng đúng Gmail của bạn, rồi bấm nút Google — vào luôn, **không**
cần SQL.

Tài khoản seed (`thuky@trustcabinet.local`, …) thì email không phải Gmail của bạn, nên lần đầu bấm
nút Google sẽ nhận `MSG05` — **đúng, không phải bug**. Muốn đăng nhập Google vào tài khoản seed:

1. Bấm nút Google, đăng nhập (Gmail của bạn phải nằm trong danh sách *test users* của Google Console)
2. Nhận `MSG05`
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

- **Access token**: giữ trong memory (React state / context). Nó chỉ sống 15 phút, mất khi F5 thì
  refresh lại là có ngay.
- **Refresh token**: cookie `HttpOnly` do backend đặt — xem mục 0. FE không lưu ở đâu cả. Đừng lưu
  vào `localStorage` bất cứ thứ gì dùng được như credential.
- **`AuthProvider` hiện lưu cả object user vào `localStorage`** dưới key `trustcabinet.session`.
  Bỏ cách đó: user thật lấy từ `/api/auth/me` hoặc từ response `login`, không tin dữ liệu trong
  localStorage vì người dùng sửa được (sửa `role` thành `admin` chẳng hạn) — UI sẽ hiện menu admin
  dù backend vẫn chặn. Không phải lỗ hổng thật, nhưng là bug UI khó hiểu.

---

## 8. Khôi phục phiên sau khi F5

`GET /api/auth/me` với `Authorization: Bearer <accessToken>` trả về đúng object `user` như trong
response login:

```json
{ "id":"...", "email":"...", "fullName":"...",
  "roleId":"01a0ce78-7203-7c7f-8326-7a0baebf4c22", "roleName":"Thư ký",
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

| Email | `roleName` trả về | `capabilities` trả về |
|---|---|---|
| `chutoa@trustcabinet.local` | `Chủ toạ` | `["MeetingCreator","MeetingApprover"]` |
| `thuky@trustcabinet.local` | `Thư ký` | `["MeetingCreator","MinutesEditor"]` |
| `thanhvien@trustcabinet.local` | `Thành viên` | `[]` |
| `quantri@trustcabinet.local` | `Quản trị viên` | `["AdminUsers"]` |

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
