# Tài liệu cuộc họp — tải lên (SCRUM-107), xem và tải về (SCRUM-21)

UC-33a AF-1 *Upload files from the computer* (SCRUM-107); UC-32a *View Meeting Documents* và UC-32c *Preview
Meeting Document* ở [mục SCRUM-21](#scrum-21--xem-và-tải-tài-liệu). Byte lưu trên Cloudflare R2, mọi lượt tải lên đi qua
backend ([ADR-0024](../05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md)); mô hình dữ liệu theo
[ADR-0018](../05-decisions/0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md) và
[ERD §2](../02-design/02-erd.md). Sequence: [UC33a_UploadDocumentsToMeeting](../06-sequencediagram/document-management/UC33a_UploadDocumentsToMeeting.txt).

Tải vào kho, chọn từ kho, gắn / gỡ thuộc SCRUM-58.

## Endpoint

`POST /api/meetings/{meetingId}/documents/upload` — `multipart/form-data`, cần bearer token.

| Part | Lặp | Ý nghĩa |
|---|---|---|
| `files` | 1–10 | Tệp. Server **không** tin tên đuôi hay `Content-Type` của part; chỉ đọc nội dung |
| `pageCounts` | mỗi `files` một lần, cùng thứ tự | Số trang PDF FE đếm bằng pdf.js. Chuỗi rỗng nếu không đếm được hoặc là DOCX |

Server cũng nhận tên `files[]` / `pageCounts[]`. `pageCounts[i]` ứng với part tệp thứ `i` trong `files`; part tệp mang
tên khác bị bỏ qua, part tên khác cũng vậy (không đọc vào bộ nhớ). Một request tối đa 64 part (tệp + giá trị),
mỗi `pageCounts` tối đa 16 byte; vượt một trong hai → `MSG164`.

### Thứ tự kiểm tra

1. Token, tài khoản đang hoạt động — không đạt: `401` / `403` body rỗng.
2. **Quyền, trước khi đọc body** (để người không có quyền không đẩy được 200 MB lên đĩa server):
   xem được cuộc họp, đang là Chủ toạ hoặc Thư ký hiện tại, cuộc họp chưa `Ended`.
3. `Content-Length` > 201 MB → `MSG288` ngay, không đọc body.
4. Số tệp ≤ 10, tổng ≤ 200 MB (BR-319).
5. Từng tệp: tên hợp lệ có đuôi `.pdf` hoặc `.docx`, không rỗng, ≤ 50 MB, và **nội dung** đúng loại mà đuôi
   khai (BR-187, BR-214, BR-215). Đuôi phải khớp nội dung vì tệp sẽ được tải về dưới chính tên đó (SCRUM-21):
   một trang HTML không được quay về từ origin của mình dưới tên `x.html`.
6. Ghi trước các dòng `StoredFile` (chưa có gì trỏ tới), đẩy từng tệp lên R2, rồi ghi `Document` và
   `MeetingDocument` trong một transaction có khoá cuộc họp, **kiểm lại quyền và trạng thái dưới khoá**. Lỗi ở
   bất kỳ bước nào thì xoá các object đã đẩy trong lần đó (BR-194, BR-286). Object nào còn sót (xoá lỗi, hay
   process chết giữa chừng) vẫn có dòng `StoredFile` không ai trỏ tới, nên job dọn xoá nó sau thời gian chờ.

### Nhận diện loại tệp

| Loại | Điều kiện | `contentType` lưu |
|---|---|---|
| PDF | Tệp **bắt đầu** bằng `%PDF-` (offset 0; không chấp nhận rác phía trước dù đặc tả PDF cho phép) | `application/pdf` |
| DOCX | zip có central directory ≤ 5.000 entry và ≤ 1 MB; main part là part mà quan hệ `officeDocument` trong `_rels/.rels` trỏ tới (kiểu Transitional, hoặc Strict như Word lưu "Strict Open XML Document"; thường là `word/document.xml`) và có trong zip; trong `[Content_Types].xml`, `Override` của chính part đó — không có thì `Default` theo đuôi của nó — khai `…wordprocessingml.document.main+xml`. Hai part XML này được đọc theo byte nên có BOM vẫn đọc được (Open XML SDK, `System.IO.Packaging` đều ghi BOM và chỉ dùng `Default`); không chấp nhận DTD | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |

DOCM (có macro), DOTX (mẫu), ảnh đổi đuôi, HTML có chuỗi `%PDF-` trong comment, zip hỏng, zip có central
directory quá lớn (để `ZipArchive` không phải nạp hàng trăm MB vào RAM), ZIP64 → không hợp lệ. Server không chuyển đổi tệp, lưu
đúng byte đã nhận (BR-183, BR-216).

### Kết quả thành công — `201`

```json
{
  "code": "MSG161",
  "message": "Đã thêm tài liệu vào cuộc họp.",
  "meetingId": "…",
  "addedCount": 2,
  "documents": [
    { "id": "…", "fileName": "Báo cáo Q3.pdf", "contentType": "application/pdf", "byteSize": 81234, "pageCount": 12 },
    { "id": "…", "fileName": "Dự thảo.docx", "contentType": "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "byteSize": 20480, "pageCount": null }
  ]
}
```

`documents[].id` là **MeetingDocument ID**. Mỗi tệp tạo:

- một `StoredFile` (khoá object `stored-files/{storedFileId}`, SHA-256, `pageCount` chỉ với PDF);
- một `Document` trong kho của người tải, ở thư mục `<tên cuộc họp> – dd/mm/yyyy` (ngày bắt đầu dự kiến,
  giờ Việt Nam). Lần đầu tạo thư mục ở gốc kho; lần sau tìm lại theo `Folder.MeetingId` nên đổi tên hay
  chuyển chỗ vẫn dùng lại; thư mục đã bị xoá thì tạo lại ở gốc (BR-192, BR-226–BR-228);
- một `MeetingDocument` dạng tài liệu chung (chưa gắn mục nghị sự).

`Document` và `MeetingDocument` trỏ **cùng một** `StoredFile` (BR-210). Tải lại cùng tên hoặc cùng nội
dung vẫn là tài liệu mới (BR-229).

### Lỗi

| HTTP | Mã | Khi nào |
|---|---|---|
| 400 | `MSG164` | Không có tệp, body không phải multipart, multipart hỏng (kể cả quá 64 part), hoặc ≥ 1 tệp không hợp lệ (đuôi khác `.pdf`/`.docx`, nội dung không khớp đuôi, rỗng, quá 50 MB). `errors` chỉ rõ tệp nào |
| 400 | `MSG288` | Quá 10 tệp, tổng quá 200 MB, hoặc `Content-Length` quá 201 MB |
| 403 | `MSG162` | Xem được cuộc họp nhưng không còn là Chủ toạ/Thư ký hiện tại (kể cả Người tổ chức không giữ hai vai trò đó) |
| 404 | `MSG174` | Không xem được cuộc họp hoặc cuộc họp không tồn tại — **cùng một body**, không lộ cuộc họp có tồn tại |
| 409 | `MSG172` | Cuộc họp đã kết thúc, kể cả khi vừa kết thúc trong lúc đang tải |
| 409 | `MSG349` | Cuộc họp đã huỷ (BR-299). FE ẩn nút tải lên khi cuộc họp `Cancelled` |
| 503 | `MSG166` | Không lưu được (R2 lỗi, DB lỗi, hoặc server chưa cấu hình kho tệp). Không tệp nào được thêm |

`errors` của `MSG164`:

```json
{ "code": "MSG164", "message": "…", "errors": { "files[1]": ["anh.pdf: không phải tệp PDF hoặc DOCX hợp lệ."] } }
```

Khoá là `files[i]` theo thứ tự part tệp; key `files` dùng cho lỗi cả request (chưa chọn tệp, sai content type).

## Note cho FE

- Gửi `FormData`: với mỗi tệp `append('files', file)` rồi `append('pageCounts', n ?? '')`. Không đặt
  header `Content-Type` tay — trình duyệt tự thêm boundary.
- **Bắt buộc** kiểm giới hạn ở FE trước khi gửi: ≤ 50 MB mỗi tệp, ≤ 10 tệp, ≤ 200 MB tổng, đuôi
  `.pdf`/`.docx`. Body quá lớn bị server từ chối ngay khi trình duyệt còn đang gửi, nên trình duyệt thường chỉ
  thấy lỗi mạng, không đọc được `MSG288`. Vẫn xử lý `MSG164`/`MSG288` từ server: server kiểm theo nội dung, FE
  chỉ thấy đuôi.
- Đọc `errors["files[i]"]` để đánh dấu đúng tệp trong danh sách đã chọn.
- `MSG162`/`MSG172`/`MSG174` có thể đến **sau** khi người dùng đã bấm tải (mất vai trò, cuộc họp vừa kết
  thúc): đóng hộp thoại và tải lại cuộc họp.
- Tải 200 MB qua server 1 GB mất thời gian — hiện tiến độ bằng `XMLHttpRequest.upload.onprogress`
  (hoặc tương đương). Request bị huỷ giữa chừng thì không tệp nào được thêm.
- Nút **Tải lên** chỉ hiện cho Chủ toạ/Thư ký hiện tại khi cuộc họp chưa `Ended`; đây chỉ là hiển thị,
  server luôn kiểm lại.
- `documentIds` của Agenda (`editor-options`, POST/PUT item) giờ là **MeetingDocument ID**, cùng loại
  `id` trả ở đây.

## SCRUM-21 · Xem và tải tài liệu

UC-32a, UC-32c. Sequence: [UC32a_ViewMeetingDocuments](../06-sequencediagram/document-management/UC32a_ViewMeetingDocuments.txt),
[UC32c_PreviewMeetingDocument](../06-sequencediagram/document-management/UC32c_PreviewMeetingDocument.txt).

**Quyền đọc** = quyền xem cuộc họp (`MeetingReadPolicy`, cùng luật với `GET /api/meetings/{id}`): Người tổ chức,
Người duyệt (từ khi gửi duyệt), Chủ toạ/Thư ký hiện tại, thành viên hiện tại khi cuộc họp đã `Published`,
`InProgress` hoặc `Ended`. Không có quyền đọc tài liệu riêng (BR-181, BR-275). Cả hai endpoint cần bearer token của
tài khoản đang hoạt động; **Guest chưa được hỗ trợ** vì chưa có xác thực Guest (UC-04).

### `GET /api/meetings/{meetingId}/documents` — UC-32a

`200`:

```json
{
  "meetingId": "…",
  "currentAgendaItemId": "…",
  "agendaItems": [
    {
      "id": "…", "position": 1, "title": "Báo cáo quý 3", "status": "InProgress",
      "documents": [
        {
          "id": "…", "fileName": "Tờ trình.pdf", "contentType": "application/pdf", "byteSize": 81234,
          "pageCount": 12, "commentCount": 2, "addedBy": { "userId": "…", "fullName": "Nguyễn Văn A" },
          "addedAt": "2026-10-24T02:15:00+00:00"
        }
      ]
    }
  ],
  "general": [ { "id": "…", "fileName": "Phụ lục.docx", "pageCount": null, "…": "…" } ]
}
```

- `agendaItems`: **mọi** mục nghị sự theo `position`, kể cả mục chưa có tài liệu (để FE đặt nút thêm tài liệu cho
  từng mục ở SCRUM-58). Tài liệu gắn nhiều mục xuất hiện ở từng mục (BR-277). `status`: `Pending` /
  `InProgress` / `Done`.
- `general`: tài liệu không gắn mục nào (BR-280).
- Trong mỗi nhóm, tài liệu xếp theo thời điểm thêm (cũ trước). Tài liệu đã gỡ không có mặt (BR-211).
- `currentAgendaItemId`: mục đang `InProgress`, hoặc `null`. Panel trong phòng họp dùng nó để đưa mục đang bàn
  lên đầu (BR-316) — server **không** đổi thứ tự theo nơi gọi.
- `id` là **MeetingDocument ID**, cùng loại với `documents[].id` khi tải lên và `documentIds` của Agenda.
- `pageCount` chỉ có ở PDF (FE đếm lúc tải lên); DOCX luôn `null`. `commentCount` đếm mọi góp ý và trả lời.

### `GET /api/meetings/{meetingId}/documents/{meetingDocumentId}/content[?download=true]` — UC-32c

Trả **đúng byte đã tải lên**, đọc theo luồng từ kho tệp qua backend, không chuyển đổi (BR-183, BR-216). Trình duyệt
tự dựng bản xem: PDF bằng pdf.js, DOCX bằng `docx-preview`.

| Header | Giá trị |
|---|---|
| `Content-Type` | `contentType` đã lưu lúc tải lên (nhận diện theo nội dung) |
| `Content-Length` | `byteSize` |
| `Content-Disposition` | luôn `attachment; filename=…; filename*=UTF-8''…` — origin của mình không bao giờ render tệp tải lên thành trang |
| `Cache-Control` | `private, no-store` |
| `X-Content-Type-Options` | `nosniff` |

`download=true` là lần **tải về** (AF-1): cuộc họp `Secret` bị chặn (`MSG159`, BR-188), kể cả khi mức mật vừa đổi
sau lúc mở trình xem. Không có tham số (hoặc `download=false`) là lần **xem**. Chỉ nhận `true`/`false`; giá trị khác bị
ASP.NET Core trả `400` không có `code`.

**Giới hạn của BR-188.** Trình duyệt phải tự dựng bản xem từ tệp gốc, nên lần xem vẫn đưa **đúng byte gốc** tới trình
duyệt. Với cuộc họp `Secret`, server chặn `download=true` và FE ẩn nút Tải về, nhưng người xem được vẫn có thể lưu
blob đó (hoặc gọi `content` không có `download`); lần đó được ghi kiểm toán là `document.viewed`, vì server không
phân biệt được. Đây là rủi ro đã biết khi không chuyển đổi tệp trên server (ADR-0018), không phải lỗ hổng sửa được ở
endpoint. PO chấp nhận rủi ro này ngày 2026-10-25 (không chuyển tệp thành ảnh trên server, không đóng dấu tên người
xem).

**Nhật ký kiểm toán** (BR-318): cuộc họp `Confidential` hoặc `Secret` ghi một dòng `AuditLogEntry` cho mỗi lần
xem / tải thành công — `action` `document.viewed` hoặc `document.downloaded`, `objectType` `MeetingDocument`,
`objectId` là MeetingDocument ID, `details` có `meetingId`, `fileName`, `sensitivity`. Dòng được ghi sau khi mở được
tệp và **trước** khi gửi byte; không ghi được thì không gửi tệp (`MSG158`/`MSG160`). Cuộc họp `Normal`/`Internal`
không ghi.

### Lỗi

| HTTP | Mã | Endpoint | Khi nào |
|---|---|---|---|
| 401 / 403 | — | cả hai | Chưa đăng nhập / tài khoản bị khoá (body rỗng) |
| 404 | `MSG156` | cả hai | Không xem được cuộc họp, cuộc họp không tồn tại, tài liệu đã gỡ, hoặc ID tài liệu của cuộc họp khác — **cùng một body** |
| 500 | `MSG157` | danh sách | Lỗi đọc DB; FE không được hiển thị như danh sách rỗng |
| 403 | `MSG159` | `content?download=true` | Cuộc họp `Secret` |
| 503 | `MSG158` | `content` | Không đưa được tệp: object thiếu trên kho, kho chưa cấu hình hoặc lỗi, DB lỗi, không ghi được nhật ký kiểm toán |
| 503 | `MSG160` | `content?download=true` | Như `MSG158`. Riêng lỗi DB ở bước kiểm tài khoản đang hoạt động (trước khi đọc tham số) luôn trả `MSG158` |

### Realtime

Sau mỗi lần tải lên thành công, hub `/hubs/meetings` gửi `documentsChanged` `{ "eventId", "meetingId", "occurredAt" }` cho mọi người
đang `JoinMeeting` và còn quyền xem cuộc họp (cùng nhóm người nhận với `agendaChanged`). Event chỉ báo "có thay
đổi", FE gọi lại `GET …/documents`. Mỗi event có `eventId` riêng như `agendaChanged`, nên lọc trùng theo
`eventId` trong `useMeetingEvents` không nuốt mất lần tải lên sau. Gửi event có giới hạn 2 giây (như agenda): hub hay DB
chậm không giữ phản hồi tải lên. Đổi mục đang bàn đã có `agendaChanged` (`Started` / `Completed`). Gỡ và gắn / gỡ
mục (SCRUM-58) nên phát cùng event này.

### Note cho FE (SCRUM-21)

- Token nằm trong JS (không có cookie), nên **không** mở thẳng URL `content` ở tab mới hay đặt vào `<a href>`. Gọi
  bằng client API (bearer) lấy `Blob`/`ArrayBuffer`, rồi đưa cho pdf.js / `docx-preview`; tải về thì
  `URL.createObjectURL` + `<a download>` với `fileName` từ danh sách. "Mở ở tab mới" từ phòng họp (BR-315) = mở route
  trình xem của FE ở tab mới, route đó tự gọi API.
- `client.ts` hiện đọc JSON; cần một biến thể trả `Blob` cho `content` nhưng vẫn đọc body JSON khi lỗi để lấy `code`.
- Nút Tải về ẩn khi `sensitivity === 'Secret'` (lấy từ `GET /api/meetings/{id}`); server vẫn chặn.
- Chỉ đọc sau khi kết thúc (BR-282), nút Tải lên chỉ cho Chủ toạ/Thư ký: lấy `status` và `myMeetingRole` từ
  `GET /api/meetings/{id}`, endpoint danh sách không lặp lại.
- Nhóm **Biểu quyết** (UC-32a AF-1) chưa có dữ liệu: mô hình chưa có bảng gắn tài liệu với biểu quyết. Ẩn nhóm này
  cho tới khi phần biểu quyết / UC-74 thêm bảng đó.
- `MSG158`/`MSG160` là lỗi tạm thời: hiện nút thử lại. `MSG156` giữa chừng (tài liệu vừa bị gỡ, mất quyền): đóng
  trình xem và tải lại danh sách.
- Lắng nghe `documentsChanged` và `agendaChanged` để làm mới tab Tài liệu và panel trong phòng.

### FE đã làm (SCRUM-21, SCRUM-107)

- `features/documents/DocumentsPanel.tsx` (`variant: 'page' | 'room'`, ADR-0011): tab Tài liệu ở chi tiết cuộc
  họp và panel "Tài liệu" trong phòng họp. Số thứ tự mục lấy theo thứ tự trong danh sách, không theo `position`
  (có thể nhảy số sau khi sắp xếp lại).
- Trình xem ở route `/mgmt/meetings/{id}/documents/{meetingDocumentId}`, ngoài khung ứng dụng: PDF bằng pdf.js
  (bản `legacy`, worker cùng origin, CSP không đổi), DOCX bằng `docx-preview`. Từ phòng họp mở ở tab mới, nút
  "‹ Tài liệu" đóng tab đó; ngoài phòng thì về `?tab=documents` của cuộc họp.
- Hộp thoại tải lên gửi một request duy nhất qua `XMLHttpRequest` (`api.upload` trong `client.ts`) để có tiến độ;
  tiến độ từng tệp tính theo số byte đã gửi. Đếm số trang PDF bằng pdf.js ngay khi chọn tệp.
- Kiểm trước khi gửi dùng lại câu của `MSG164` (sai đuôi, rỗng, quá 50 MB) và `MSG288` (quá 10 tệp / 200 MB), không
  cấp mã mới.
- DOCX là nội dung người dùng tải lên, được dựng ngay trong origin của mình: trình xem tắt `renderAltChunks` (HTML nhúng
  sẽ thành `iframe` cùng origin) và chỉ giữ link `http:`, `https:`, `mailto:`, `#…` — link `javascript:` bị bỏ `href`,
  link ngoài mở ở tab mới. CSP của server test là lớp chặn thứ hai, không phải lớp duy nhất.
- Giới hạn đã biết: không nạp `wasm` và font chuẩn của pdf.js (cần thêm `'wasm-unsafe-eval'` vào CSP), nên ảnh JPEG2000
  trong PDF scan có thể không hiện.
- Chưa có: nhóm Biểu quyết (chưa có dữ liệu), tab "Từ kho của tôi", ghim ở từng mục và menu Gỡ (SCRUM-58), cột góp ý
  trong trình xem (SCRUM-23).

### Kiểm thử (SCRUM-21)

`tests/TrustCabinet.Web.Tests/Documents/MeetingDocumentReadEndpointsTests.cs` (PostgreSQL thật, kho tệp trong bộ
nhớ): nhóm theo mục đúng thứ tự `position`, tài liệu gắn mục 1 và 3 hiện ở cả hai, tài liệu chung, tài liệu đã gỡ
không hiện, `currentAgendaItemId`, `pageCount`/`commentCount`/`addedBy`; cuộc họp rỗng; cuộc họp đã kết thúc vẫn đọc
được; người ngoài, cuộc họp không tồn tại, thành viên của cuộc họp `Draft` → cùng `404/MSG156`; lỗi đọc DB →
`MSG157`; byte trả về khớp, header `attachment` + `filename*`, `no-store`, `nosniff`, `Content-Length`; `Normal`
không ghi kiểm toán; `Secret` xem được và ghi `document.viewed`, tải về `403/MSG159` (cả Chủ toạ) không ghi gì thêm;
`Confidential` tải về ghi `document.downloaded`; tài liệu đã gỡ / của cuộc họp khác / không tồn tại → `MSG156`, kể cả
khi tải về từ cuộc họp `Secret` (không trả `403` để lộ ID); tải về `Secret` khi object thiếu vẫn `MSG159` (chặn trước khi
chạm kho); lỗi DB lúc tra tài liệu → `MSG158`/`MSG160`;
object thiếu hoặc kho chưa cấu hình → `MSG158`/`MSG160`, không ghi kiểm toán; lỗi ghi kiểm toán → không gửi tệp;
tải lên phát `documentsChanged` chỉ cho người đã join cuộc họp đó, tải lên lỗi không phát.

## Lưu trữ và cấu hình

Section `FileStorage` (ADR-0024). `ServiceUrl`, `BucketName`, `AccessKeyId`, `SecretAccessKey` là bí
mật — `dotnet user-secrets` khi dev, biến môi trường `FileStorage__*` khi deploy, không đặt trong
`appsettings.json`. Thiếu một trong bốn giá trị thì app vẫn chạy, upload trả `503/MSG166` và job dọn
đứng yên. Hướng dẫn: [03-co-so-du-lieu.md §5](../02-design/03-co-so-du-lieu.md#5-kho-tệp-tài-liệu) và
[deploy/README.md](../../deploy/README.md).

- Endpoint tự đọc multipart bằng `MultipartReader` thay vì `ReadFormAsync`: `ReadFormAsync` đọc trọn mọi part
  không phải tệp thành chuỗi, to bao nhiêu cũng vậy (`ValueLengthLimit` chỉ áp cho form urlencoded). Part tệp ghi
  ra tệp tạm trên đĩa, không giữ trong RAM, và bị xoá khi request kết thúc; `pageCounts` đọc tối đa 16 byte; part
  khác bị bỏ qua không đọc. Giới hạn body chỉ nâng lên 201 MB cho riêng endpoint này (Kestrel mặc định 30 MB).
- Tên tệp được lưu nguyên văn (chỉ bỏ phần đường dẫn). Endpoint `content` của SCRUM-21 mã hoá tên trong
  `Content-Disposition` (`filename*=UTF-8''…`) thay vì chèn thẳng.
- Kho tệp có `OpenReadAsync(key)` đọc object theo luồng; endpoint `content` của SCRUM-21 dùng nó. CSP của server test không đổi.

### Job dọn file mồ côi (BR-213)

`OrphanFilePurgeWorker` chạy lúc khởi động rồi mỗi `FileStorage:OrphanPurgeIntervalMinutes` (mặc định
60). Mỗi lượt duyệt hết, từng loạt 100, các `StoredFile` thoả: `PurgedAt` rỗng, không còn `Document`, không còn
`MeetingDocument` có `RemovedAt` rỗng, `UploadedAt` cũ hơn `FileStorage:OrphanGracePeriodHours` (mặc
định 24, tối thiểu 1 dù cấu hình thấp hơn: dòng `StoredFile` của một lần tải đang chạy cũng chưa ai trỏ tới cho
tới khi commit). Xoá object rồi ghi `PurgedAt`; **giữ** dòng `StoredFile`. Xoá lỗi thì tệp đó ngồi ngoài phần còn lại
của lượt và để lượt sau thử lại, nên vài tệp xoá mãi không được không chặn các tệp phía sau. Dòng `StoredFile`
của một lần tải không hoàn tất (ghi trước khi đẩy object, xem bước 6) cũng thoả điều kiện, nên object của nó được
dọn theo cùng đường này. Lỗi bất kỳ trong một lượt, kể cả timeout của SDK (ném `OperationCanceledException`), chỉ
được ghi log; chỉ việc tắt app mới dừng job.

Ticket này chưa có đường xoá khỏi kho hay gỡ khỏi cuộc họp (SCRUM-58), nên job được kiểm bằng
`OrphanFilePurgerTests`.

## Dữ liệu cá nhân (Luật 91/2025/QH15)

Tài liệu cuộc họp có thể chứa dữ liệu cá nhân, mà R2 lưu ngoài Việt Nam, tức là chuyển dữ liệu cá nhân xuyên
biên giới (Điều 20). Server test chỉ dùng dữ liệu
giả. Byte bị xoá khỏi R2 khi job dọn chạy (sau khi không còn ai tham chiếu); dòng `StoredFile` giữ tên
tệp và SHA-256 làm bằng chứng, không giữ nội dung. Nghĩa vụ khi đưa vào dùng thật ghi ở ADR-0024.

## Migration

`MeetingDocumentsOnStoredFiles` đưa cả cụm tài liệu về mô hình đích một lần: `stored_files`, `folders`,
`documents` (dòng kho), `meeting_documents`, `meeting_document_agenda_items`, `document_comments` trỏ
`meeting_document_id` (thêm `guest_invitation_id`, check đúng một tác giả). Migration **xoá** ba bảng
cũ (`documents`, `document_versions`, `document_comments`): chúng chưa bao giờ có endpoint tải lên nên
chỉ chứa fixture dev không có byte. Fixture Agenda chỉ được seed dưới dạng `MeetingDocument` (không có
object trên kho, nên liệt kê được nhưng chưa mở được) trên **DB mới**. Seeder bỏ qua cuộc họp đã tồn tại, nên
DB dev cũ và server test **mất hẳn** hai tài liệu fixture mỗi cuộc họp `[SCRUM-25]` sau migration; muốn có lại thì
seed lại từ đầu (`docker compose down -v`, [deploy/README.md](../../deploy/README.md) mục xoá dữ liệu).

Từ SCRUM-98, một tài liệu gắn được nhiều mục khi lập chương trình (bảng nối, ADR-0020 §5); sửa một
mục chỉ đổi liên kết của mục đó. UC-33b/UC-33d (SCRUM-58) thêm endpoint gắn/gỡ riêng.

## Kiểm thử

`tests/TrustCabinet.Web.Tests/Documents/`:

- `MeetingDocumentEndpointsTests` (PostgreSQL thật, kho tệp trong bộ nhớ): 3 PDF + 1 DOCX; Chủ toạ;
  ảnh đổi đuôi, DOCM, zip thiếu `word/document.xml`; 11 tệp; body khai > 200 MB; tệp 51 MB; không
  tệp / không multipart; Thành viên, Người tổ chức, người ngoài, cuộc họp không tồn tại; Thư ký đã bị
  gỡ, tài khoản bị khoá; cuộc họp đã kết thúc và kết thúc giữa lúc tải; R2 lỗi ở tệp thứ hai; DB lỗi
  sau khi object đã lên; xoá dọn cũng lỗi (object còn lại có dòng `StoredFile` cho job dọn); dòng `StoredFile`
  có trước khi object được đẩy; `pageCounts` dài quá 16 byte, quá 64 part, part lạ 1 MB bị bỏ qua; chưa cấu hình
  kho; dùng lại thư mục sau khi đổi tên/chuyển,
  tạo lại sau khi xoá; Agenda liệt kê tài liệu vừa tải.
- `DocumentFileRulesTests`: nhận diện theo nội dung (HTML giả PDF, DOCM giả DOCX, central directory quá lớn;
  DOCX có BOM và chỉ khai `Default` như `System.IO.Packaging` ghi, main part tên khác, DOCX Strict, thiếu
  `_rels/.rels`),
  tên tệp có ký tự định dạng (U+202E, U+200B),
  đuôi phải khớp nội dung, tên tệp, ngày thư mục theo giờ Việt Nam, giới hạn tổng kiểm trước khi đọc tệp,
  `pageCount` chỉ cho PDF.
- Thêm ở endpoint: mất vai trò Thư ký giữa lúc tải → `MSG162` và object bị xoá; Thành viên gửi tệp sai → `MSG162`
  (quyền kiểm trước); HTML giả PDF và PDF mang tên `.html` → `MSG164`.
- `OrphanFilePurgerTests`: chỉ dọn tệp không còn tham chiếu và quá thời gian chờ; xoá lỗi thì để lượt sau;
  100 tệp xoá lỗi không chặn tệp thứ 101; thời gian chờ cấu hình 0 vẫn giữ tệp mới tải 30 phút.
