# Kho tài liệu cá nhân — thư mục (SCRUM-110), tài liệu (SCRUM-111)

Thuộc SCRUM-58. UC-72a–d *Manage Library Folders*; UC-34 *View Personal Document Library*, UC-36 *Upload Document to
Personal Library*, UC-71a–c *Manage Personal Documents*. Mô hình theo [ADR-0006](../05-decisions/0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md)
và [ADR-0018](../05-decisions/0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md): dòng kho (`Document`) chỉ tham chiếu
`StoredFile` bất biến, không phiên bản, không chép byte. Sequence: `docs/06-sequencediagram/document-management/UC34_…`,
`UC36_…`, `UC71a–c_…`, `UC72a–d_…` (nhánh `docs/scrum-91-92-fe07-kho-tai-lieu`). Mã thông báo:
[danh mục](00-danh-muc-ma-thong-bao.md#kho-tài-liệu-cá-nhân--contract-backend-scrum-110-scrum-111-scrum-58).

Gắn tài liệu trong kho vào cuộc họp, gỡ, gắn mục nghị sự: [11-tai-lieu-cuoc-hop.md](11-tai-lieu-cuoc-hop.md#scrum-58--gắn-và-gỡ-tài-liệu-của-cuộc-họp).

## Quyền

- Mọi endpoint cần bearer token của tài khoản đang hoạt động (`401`/`403` body rỗng nếu không).
- Kho **chỉ chủ sở hữu** đọc và sửa. Thư mục hay tài liệu của người khác trả **`404 MSG175`, cùng body với mục không tồn
  tại** (BR-242); thư mục đích của người khác trả `404 MSG179`.
- Guest không có kho (BR-204): Guest chưa có phiên đăng nhập (UC-04), nên không gọi được các endpoint này.
- **Quản trị viên đọc kho của tài khoản đã khoá** (PO 27/10/2026), xem [mục cuối](#quản-trị-viên-đọc-kho-của-tài-khoản-đã-khoá).

Mọi thao tác ghi trên kho của một người chạy trong một transaction có khoá theo chủ kho (`pg_advisory_xact_lock`), nên
hai lần chuyển thư mục cùng lúc không tạo được vòng lặp hay cấp thứ 6, và hai lần tải lên cùng lúc không cùng lọt dưới
hạn mức.

## Xem kho — `GET /api/documents?folderId=&q=&type=` (UC-34)

| Tham số | Ý nghĩa |
|---|---|
| `folderId` | Thư mục đang mở; bỏ trống = gốc kho |
| `q` | Tìm theo tên tài liệu, không phân biệt hoa thường, **trong thư mục đang mở** (AF-1). `%` và `_` là ký tự thường |
| `type` | `pdf` hoặc `docx`; giá trị khác bị bỏ qua |

`200`:

```json
{
  "folderId": null,
  "folders": [
    { "id": "…", "name": "Họp giao ban – 24/10/2026", "parentId": null, "meetingId": "…", "createdAt": "…" }
  ],
  "documents": [
    {
      "id": "…", "fileName": "Tờ trình.pdf", "contentType": "application/pdf", "byteSize": 81234, "pageCount": 12,
      "folderId": null, "createdAt": "…", "meetingCount": 2
    }
  ],
  "usedBytes": 81234,
  "quotaBytes": 1073741824
}
```

- `folders`: **mọi** thư mục của kho (để FE dựng cây và hộp chọn đích khi chuyển), xếp theo tên. `meetingId` khác `null`
  là thư mục tự tạo khi tải từ máy vào cuộc họp (BR-192); đổi tên, chuyển, xoá như mọi thư mục.
- `documents`: tài liệu **ở đúng vị trí** `folderId`, xếp theo tên. Rỗng là kết quả bình thường (AF-2), không phải lỗi.
- `meetingCount` ("Đang dùng trong n cuộc họp"): số cuộc họp **người xem được** còn đang dùng tài liệu này (BR-251) —
  đếm theo `MeetingDocument.DocumentId`, không tính bản đã gỡ.
- `usedBytes` / `quotaBytes`: dung lượng cả kho đã dùng và hạn mức 1 GB (PO 27/10/2026). Tài liệu tự vào kho khi tải
  từ máy lên cuộc họp cũng được tính.

Lỗi: `404 MSG175` (thư mục không còn / của người khác), `500 MSG176` (lỗi đọc DB).

## Tải lên kho — `POST /api/documents/upload?folderId=` (UC-36)

Multipart giống hệt `POST /api/meetings/{id}/documents/upload` (part `files` + `pageCounts`, xem
[11-tai-lieu-cuoc-hop.md](11-tai-lieu-cuoc-hop.md#endpoint)). Thứ tự kiểm:

1. Thư mục đích (nếu có) của chính mình — **trước khi đọc body**; không thì `404 MSG179`. Kho tệp chưa cấu hình → `503 MSG178`.
2. Giới hạn mỗi lần như UC-33a: ≤ 10 tệp, tổng ≤ 200 MB (`400 MSG288`, PO 27/10/2026 áp cho cả UC-36); từng tệp PDF/DOCX
   theo nội dung, ≤ 50 MB (`400 MSG164`, `errors["files[i]"]`).
3. Hạn mức kho: dung lượng đã dùng + lần tải ≤ 1 GB, không thì `409 MSG345`, **không tệp nào** được thêm (BR-286).
4. Ghi `StoredFile` → đẩy R2 → trong một transaction có khoá chủ kho: kiểm lại thư mục đích và hạn mức, ghi `Document`.
   Lỗi ở bước nào thì xoá object đã đẩy trong lần đó (ADR-0024 §4) → `503 MSG178`.

`201`:

```json
{ "code": "MSG177", "message": "Đã tải tài liệu lên kho của bạn.", "addedCount": 2, "documents": [ { "id": "…", "fileName": "…", "…": "…" } ] }
```

`documents[]` cùng dạng với `documents[]` của `GET /api/documents`. Tải trùng tên hoặc trùng nội dung vẫn là tài liệu mới (BR-229).

## Mở tệp trong kho — `GET /api/documents/{documentId}/content[?download=true]`

Như `GET …/meetings/{id}/documents/{meetingDocumentId}/content` (UC-32c): đúng byte gốc, luôn `attachment`,
`private, no-store`, `nosniff`. Không có quy tắc Mật ở đây (kho không thuộc cuộc họp nào), không ghi kiểm toán khi
chủ kho tự mở. Lỗi: `404 MSG175`; `503 MSG158` (xem) / `503 MSG160` (tải về) khi object thiếu, kho tệp lỗi hoặc DB lỗi.

## Đổi tên — `PUT /api/documents/{documentId}/name` (UC-71b)

Body `{ "fileName": "Tờ trình (bản cuối).pdf" }`. Tên được cắt khoảng trắng hai đầu và bỏ phần đường dẫn; phải
**giữ đúng phần đuôi** của tệp (`.pdf` cho PDF, `.docx` cho DOCX, không phân biệt hoa thường), không trống, ≤ 255 ký tự,
không có ký tự điều khiển. Sai → `400 MSG346`. Tên đã chép vào cuộc họp **không đổi theo** (BR-276).

`200 { "code": "MSG183", "message": "…", "document": { …như GET… } }`. Lỗi: `404 MSG175`, `503 MSG184`.

## Chuyển tài liệu — `PUT /api/documents/{documentId}/folder` (UC-71c)

Body `{ "folderId": "…" }`; `null` = về gốc kho (AF-1). Chỉ dòng kho đổi chỗ, cuộc họp không ảnh hưởng (BR-245).
`200 MSG185` kèm `document`. Lỗi: `404 MSG175` (tài liệu), `404 MSG179` (thư mục đích), `503 MSG186`.

## Xoá khỏi kho — `DELETE /api/documents/{documentId}` (UC-71a)

Chỉ xoá **dòng kho**. Cuộc họp đang dùng tệp vẫn đọc bình thường — `MeetingDocument.DocumentId` thành `null`, vẫn trỏ
cùng `StoredFile` — kể cả khi chủ kho bị vô hiệu hoá sau đó (BR-186, BR-232). Byte chỉ bị job dọn xoá khi không còn
dòng kho nào và không còn cuộc họp nào đang dùng (BR-213).

`200 MSG180`. Lỗi: `404 MSG175`, `503 MSG181`.

## Thư mục (UC-72a–d)

| Endpoint | Body | Thành công | Lỗi |
|---|---|---|---|
| `POST /api/folders` | `{ "name", "parentId"? }` | `201 MSG187` + `folder` | `404 MSG179` thư mục cha; `400 MSG188` tên trống; `400 MSG347` tên > 400 ký tự; `409 MSG189` quá 5 cấp; `503 MSG190` |
| `PUT /api/folders/{folderId}/name` | `{ "name" }` | `200 MSG191` + `folder` | `404 MSG175`; `400 MSG188`; `400 MSG347`; `503 MSG192` |
| `PUT /api/folders/{folderId}/parent` | `{ "parentId" }` (`null` = về gốc) | `200 MSG194` + `folder` | `404 MSG175`; `404 MSG179` đích; `409 MSG193` vào chính nó / thư mục con; `409 MSG189` quá 5 cấp; `503 MSG195` |
| `DELETE /api/folders/{folderId}` | — | `200 MSG197` | `404 MSG175`; `409 MSG196` còn tài liệu hoặc thư mục con; `503 MSG198` |

`folder` = `{ "id", "name", "parentId", "meetingId", "createdAt" }`, cùng dạng phần tử `folders` của `GET /api/documents`.

- Tên được cắt khoảng trắng hai đầu; **được trùng** tên (BR-221).
- Cấp: thư mục ở gốc là cấp 1, tối đa cấp 5 (BR-191). Khi chuyển, cả cây con đi theo, nên kiểm **thư mục con sâu nhất**
  sau khi chuyển, không chỉ thư mục được chuyển.
- Thứ tự kiểm khi chuyển theo UC-72c: thư mục → thư mục đích → vào chính nó / con cháu → độ sâu.
- Xoá chỉ khi rỗng (BR-223); khoá ngoại `Restrict` là lớp chặn thứ hai. Xoá thư mục tự tạo của cuộc họp thì lần tải từ
  máy tiếp theo vào cuộc họp đó tạo lại thư mục ở gốc (BR-228).

## Quản trị viên đọc kho của tài khoản đã khoá

PO chốt 27/10/2026: để lấy lại tài liệu khi người dùng nghỉ, Quản trị viên đọc được kho của **tài khoản đã khoá**; chủ kho
không đăng nhập được nên không tự đọc. **Phạm vi mới, chưa có UC/BR** — BA bổ sung ở BA Final Review.

| Endpoint | Ghi chú |
|---|---|
| `GET /api/users/{userId}/library?folderId=` | Cùng body với `GET /api/documents`; `meetingCount` đếm theo cuộc họp Quản trị viên xem được |
| `GET /api/users/{userId}/library/documents/{documentId}/content[?download=true]` | Như mở tệp của chủ kho |

- Cổng quyền `AdminUsers` như `/api/users` (thiếu quyền → `403` body rỗng).
- Tài khoản còn hoạt động hoặc không tồn tại → `404 MSG175`, như mục không có trong kho.
- **Mỗi lần đọc ghi một dòng kiểm toán** `library.read`, `objectType` `User`, `objectId` là chủ kho, `details` có
  `folderId` (xem danh sách) hoặc `documentId` + `fileName` (mở tệp). Không ghi được kiểm toán thì không trả dữ liệu
  (`500 MSG176` / `503 MSG158`). Chỉ đọc; Quản trị viên không sửa, chuyển hay xoá được gì trong kho này.
- Dữ liệu cá nhân (Luật 91/2025/QH15): tài liệu trong kho có thể chứa dữ liệu cá nhân của người đã nghỉ và người khác;
  việc đọc phải có mục đích (bàn giao công việc) và để lại vết — dòng kiểm toán là vết đó. Chưa có cơ chế xoá kho của
  người đã nghỉ; BA/PO quyết khi bổ sung UC.

## Note cho FE

- Màn kho gọi `GET /api/documents` một lần cho mỗi vị trí; cây thư mục lấy từ `folders`, không cần endpoint riêng.
- Đổi tên tài liệu: gợi ý FE chỉ cho sửa phần tên, giữ nguyên đuôi, để không gặp `MSG346`.
- `MSG175` / `MSG179` giữa chừng (mục vừa bị xoá ở tab khác): tải lại kho.
- Hiện "Đã dùng x / 1 GB" từ `usedBytes` / `quotaBytes`; kiểm hạn mức ở FE trước khi gửi để khỏi đẩy 200 MB rồi mới
  nhận `MSG345`. Server vẫn kiểm lại.
- Tải lên: giống hộp tải lên của cuộc họp (`api.upload`, `FormData`, kiểm 10 tệp / 200 MB / 50 MB trước khi gửi).

## Kiểm thử

`tests/TrustCabinet.Web.Tests/Documents/PersonalLibraryEndpointsTests.cs` (PostgreSQL thật, kho tệp trong bộ nhớ): tạo
thư mục lồng nhau trùng tên; tên trống / quá dài; thư mục, tài liệu của người khác ở mọi endpoint → `MSG175`/`MSG179`;
cấp 6 khi tạo và khi chuyển cây con; chuyển vào chính nó / cháu, về gốc; đổi tên giữ nội dung; chỉ xoá thư mục rỗng; lỗi
DB → `MSG190`/`MSG192`/`MSG195`/`MSG198`/`MSG176`; tải lên vào thư mục (không đụng cuộc họp, object có trên kho, `usedBytes`);
thư mục đích của người khác, ảnh đổi đuôi, 11 tệp; vượt hạn mức → `MSG345` không object nào còn lại; R2 lỗi ở tệp thứ hai,
kho chưa cấu hình → `MSG178`; tìm không phân biệt hoa thường, lọc loại, `%`/`_` là ký tự thường, chỉ trong thư mục đang
mở; `meetingCount` bỏ cuộc họp không xem được và bản đã gỡ; đổi tên giữ đuôi, bản trong cuộc họp giữ tên cũ; chuyển;
xoá khỏi kho rồi khoá chủ kho, thành viên vẫn mở được bản trong cuộc họp; lỗi DB → `MSG184`/`MSG186`/`MSG181`; mở tệp,
object thiếu → `MSG158`/`MSG160`; Quản trị viên đọc kho đã khoá (2 dòng kiểm toán), kho còn hoạt động → `MSG175`, người
thường → `403`.
