# Tài liệu cuộc họp — tải lên (SCRUM-107), xem và tải về (SCRUM-21), gắn và gỡ (SCRUM-58), góp ý (SCRUM-23)

UC-33a AF-1 *Upload files from the computer* (SCRUM-107); UC-32a *View Meeting Documents* và UC-32c *Preview
Meeting Document* ở [mục SCRUM-21](#scrum-21--xem-và-tải-tài-liệu). Byte lưu trên Cloudflare R2, mọi lượt tải lên đi qua
backend ([ADR-0024](../05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md)); mô hình dữ liệu theo
[ADR-0018](../05-decisions/0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md) và
[ERD §2](../02-design/02-erd.md). Sequence: [UC33a_UploadDocumentsToMeeting](../06-sequencediagram/document-management/UC33a_UploadDocumentsToMeeting.txt).

Chọn từ kho, gắn / gỡ mục nghị sự, gỡ khỏi cuộc họp: [mục SCRUM-58](#scrum-58--gắn-và-gỡ-tài-liệu-của-cuộc-họp). Góp ý: [mục SCRUM-23](#scrum-23--góp-ý-trên-tài-liệu). Kho cá nhân (thư mục, tải vào kho): [14-kho-tai-lieu-ca-nhan.md](14-kho-tai-lieu-ca-nhan.md).

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
   xem được cuộc họp, đang là Chủ toạ hoặc Thư ký hiện tại, cuộc họp chưa `Ended` và chưa `Cancelled`.
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
| 409 | `MSG346` | Bộ nhớ tài liệu của cả hệ thống không đủ chỗ (ADR-0034, mặc định 10 GB). Kiểm dưới khoá chung trước khi đẩy byte lên R2; không tệp nào được thêm |
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
- Đọc `errors["files[i]"]` để đánh dấu đúng tệp. FE hiện gửi mỗi tệp một request (ADR-0033), nên chỉ có `files[0]`.
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
- `general`: mọi tài liệu của cuộc họp, kể cả tài liệu đang gắn mục, **trừ** tài liệu Chủ toạ/Thư ký đã gỡ khỏi
  nhóm chung (`DELETE …/documents/{id}/general`). Tài liệu không gắn mục nào luôn ở đây (BR-280, ADR-0043). Một tài
  liệu có thể vừa ở `general` vừa ở một hay nhiều mục.
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
chậm không giữ phản hồi tải lên. Đổi mục đang bàn đã có `agendaChanged` (`Started` / `Completed`). Thêm từ kho, gỡ,
gắn và gỡ mục (SCRUM-58) phát cùng event này.

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
- Tải lên đi qua hàng đợi ở trình duyệt ([ADR-0033](../05-decisions/0033-hang-doi-tai-len-o-trinh-duyet.md)): hộp thoại
  chỉ chọn và kiểm tệp, mỗi tệp một request `XMLHttpRequest` (`api.upload`, có tiến độ), chạy lần lượt; khay ở góc theo
  dõi, tự thử lại khi chắc chưa lưu, rồi chờ "Thử lại". Đếm số trang PDF bằng pdf.js ngay khi chọn tệp.
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

## SCRUM-58 · Gắn và gỡ tài liệu của cuộc họp

SCRUM-112 (58c): UC-33a luồng chính (chọn từ kho), UC-33b (gắn vào mục nghị sự, ba nguồn), UC-33c (gỡ khỏi cuộc họp),
UC-33d (gỡ khỏi một mục). Sequence: `UC33a_AddDocumentsFromLibrary`, `UC33b_…`, `UC33c_…`, `UC33d_…` (nhánh
`docs/scrum-91-92-fe07-kho-tai-lieu`). Theo phương án (b) BA chốt 27/10/2026: chức năng riêng, không đi qua form sửa
mục nghị sự.

**Quyền, chung cho mọi endpoint dưới đây**: Chủ toạ hoặc Thư ký **hiện tại** của chính cuộc họp đó (`MeetingRole`,
không phải quyền toàn hệ thống), tới khi cuộc họp `Ended` hoặc `Cancelled` (BR-184, BR-185, BR-279, BR-299); trạng thái mục nghị sự không khoá
liên kết. Thứ tự: không xem được cuộc họp → `404 MSG174`; xem được nhưng không phải Chủ toạ/Thư ký → `403` (mã theo
thao tác); đã kết thúc → `409` (mã theo thao tác); đã huỷ → `409 MSG349` (mọi thao tác). Mỗi thao tác chạy trong **một** transaction có khoá cuộc họp và kiểm
quyền dưới khoá, nên mất vai trò hay cuộc họp vừa kết thúc giữa chừng vẫn bị chặn đúng mã. Thành công thì phát
`documentsChanged` như khi tải lên.

`PUT /api/meetings/{id}/agenda/{itemId}` vẫn nhận `documentIds` (chỉ Thư ký, mã của Agenda), nhưng từ 30/10/2026 FE không
gửi nữa và thiếu field thì giữ nguyên liên kết ([08-chuong-trinh-nghi-su.md](08-chuong-trinh-nghi-su.md)). FE gắn tài liệu
cho một mục bằng các endpoint dưới đây, từ nút ghim trên dòng của mục ở tab Nội dung (hiện khi `actions.canLinkDocuments`)
và ở tab Tài liệu.

### `POST /api/meetings/{id}/documents` — thêm từ kho (UC-33a)

Body `{ "documentIds": ["<id tài liệu trong kho>", …] }`. Mỗi tài liệu phải **của chính người gọi** và còn trong kho;
không chọn gì hoặc có tài liệu không hợp lệ → `400 MSG249` (BR-209). Tệp đã có trong cuộc họp (cùng `StoredFile`, chưa
gỡ) → `409 MSG165` (BR-193). Tất cả hoặc không (BR-286). ID trùng trong mảng được gộp.

Mỗi tài liệu thành một `MeetingDocument` **chung** (chưa gắn mục), trỏ cùng `StoredFile` của dòng kho, `DocumentId` là
dòng kho, `fileName` chép **tên hiện tại** trong kho (BR-197, BR-276). Không chép byte (ADR-0018).

`201` cùng dạng tải lên: `{ "code": "MSG161", "message", "meetingId", "addedCount", "documents": [ { "id", "fileName",
"contentType", "byteSize", "pageCount" } ] }`. Lỗi khác: `403 MSG162`, `409 MSG172`, `503 MSG166`.

### `DELETE /api/meetings/{id}/documents/{meetingDocumentId}` — gỡ khỏi cuộc họp (UC-33c)

Đặt `RemovedAt`, `RemovedById`, xoá mọi liên kết mục nghị sự của tài liệu. Dòng và **góp ý giữ lại làm lịch sử** nhưng
không còn hiện ở danh sách, trình xem hay góp ý (BR-281, BR-284). Thêm lại cùng tệp sau đó là một lần thêm mới: dòng mới,
không có góp ý cũ (BR-212). Cuộc họp khác dùng cùng tệp không bị ảnh hưởng. Dòng kho không đổi.

`200 { "code": "MSG167", "message", "meetingId", "meetingDocumentId" }`. Lỗi: `403 MSG169`, `409 MSG248`; tài liệu đã gỡ
hoặc của cuộc họp khác → `404 MSG170` (**cùng body**, BR-211); `503 MSG171`.

Đặc tả cũ chặn khi "đã có biên bản" (BR-185); biên bản chỉ có sau khi cuộc họp kết thúc, nên chặn ở `Ended` đã bao gồm.

### `POST /api/meetings/{id}/agenda/{itemId}/documents[…]` — gắn vào mục nghị sự (UC-33b)

Ba nguồn, ba endpoint, cùng mã thành công `200 MSG238`:

| Nguồn | Endpoint | Body |
|---|---|---|
| Tài liệu đã có trong cuộc họp (luồng chính) | `POST …/agenda/{itemId}/documents` | `{ "documentIds": ["<MeetingDocument ID>", …] }` |
| Chọn từ kho (AF-2) | `POST …/agenda/{itemId}/documents/from-library` | `{ "documentIds": ["<id tài liệu trong kho>", …] }` |
| Tải từ máy (AF-1) | `POST …/agenda/{itemId}/documents/upload` | multipart như `POST …/documents/upload` |

`200 { "code": "MSG238", "message", "meetingId", "agendaItemId", "documents": [ … ] }` — `documents` là các tài liệu
**mới thêm vào cuộc họp** (AF-1, AF-2); luồng chính không thêm tài liệu nên mảng rỗng.

- Mục nghị sự phải thuộc cuộc họp này, không thì `404 MSG242` (BR-278) — với AF-1 kiểm **trước khi đọc body**, cùng lúc
  với quyền (như UC-33a AF-1; đặc tả UC-33b AF-1 bước 2 còn ghi kiểm tệp trước).
- Luồng chính: mọi tài liệu phải còn trong cuộc họp, không thì `404 MSG170` (cả khi mảng rỗng); tài liệu đã gắn mục này
  → `409 MSG243` (BR-277); liên kết với mục **khác** giữ nguyên. Lỗi lưu `503 MSG244`.
- AF-2: như UC-33a (`400 MSG249`, `409 MSG165`), rồi thêm vào cuộc họp và gắn mục trong cùng transaction. Lỗi lưu
  `503 MSG166` (theo sequence).
- AF-1: như UC-33a AF-1 — giới hạn `MSG288` (PO 27/10/2026 áp cho UC-33b AF-1), kiểm tệp `MSG164`, dòng kho vào thư mục
  `<tên cuộc họp> – dd/mm/yyyy`, cùng thứ tự ghi R2; thêm liên kết mục trong cùng transaction. Lỗi lưu `503 MSG166`.
- Tất cả hoặc không (BR-304). Lỗi chung: `403 MSG240`, `409 MSG247`.

### `DELETE /api/meetings/{id}/agenda/{itemId}/documents/{meetingDocumentId}` — gỡ khỏi một mục (UC-33d)

Xoá **đúng một** liên kết; tài liệu vẫn ở cuộc họp, giữ liên kết với mục khác, hết liên kết thì về nhóm chung (BR-280)
và xoá cờ "đã gỡ khỏi nhóm chung", nên gắn lại sau đó tài liệu vẫn ở nhóm chung (ADR-0043).
Thứ tự theo sequence: tài liệu còn trong cuộc họp (`404 MSG170`) → mục thuộc cuộc họp (`404 MSG242`) → liên kết tồn tại
(`404 MSG246`). `200 { "code": "MSG239", "message", "meetingId", "agendaItemId", "meetingDocumentId" }`. Lỗi khác:
`403 MSG241`, `409 MSG247`, `503 MSG245`.

### `DELETE /api/meetings/{id}/documents/{meetingDocumentId}/general` — gỡ khỏi nhóm tài liệu chung (ADR-0043)

Đặt `excluded_from_general = true`: tài liệu không còn ở `general` nhưng vẫn ở cuộc họp và ở mọi mục đang gắn. Cùng
quyền và cùng mã lỗi chung như UC-33d (`403 MSG241`, `409 MSG247`, `409 MSG349`, `404 MSG174`, `503 MSG245`).

| Tình huống | Mã |
|---|---|
| Thành công, phát `documentsChanged` | `200 { "code": "MSG368", "message", "meetingId", "meetingDocumentId" }` |
| Tài liệu đã gỡ khỏi cuộc họp hoặc thuộc cuộc họp khác | `404 MSG170` |
| Đã gỡ khỏi nhóm chung trước đó | `404 MSG370` |
| Không gắn mục nào (nhóm chung là chỗ duy nhất của nó) | `409 MSG369` |

FE hiện nút này ở nhóm "Tài liệu chung" cho tài liệu đang gắn ít nhất một mục.

### Note cho FE (SCRUM-58)

- Menu ⋯ của tài liệu: "Gỡ khỏi cuộc họp" (UC-33c, hộp xác nhận) và, trong nhóm của một mục, "Gỡ khỏi mục này" (UC-33d).
  Chỉ hiện cho Chủ toạ/Thư ký hiện tại khi cuộc họp chưa `Ended` và chưa `Cancelled` (`myMeetingRole`, `status` từ
  `GET /api/meetings/{id}`).
- Hộp thêm tài liệu có tab "Từ kho của tôi" (UC-33a) gọi `GET /api/documents` để duyệt kho; nút thêm ở từng mục mở cùng
  hộp đó với ba nguồn (UC-33b).
- `MSG170`/`MSG246`/`MSG242` nghĩa là dữ liệu đã cũ: tải lại danh sách. `MSG162`/`MSG169`/`MSG240`/`MSG241`/`MSG172`/
  `MSG247`/`MSG248`/`MSG349` có thể đến sau khi mở hộp thoại: đóng hộp và tải lại cuộc họp.
- Tab Nội dung cuộc họp hiện tài liệu theo mục từ `GET …/agenda`; sau gắn/gỡ chỉ có `documentsChanged`, nên tab đó cần
  nghe thêm event này nếu muốn tự làm mới.

## SCRUM-23 · Góp ý trên tài liệu

UC-35 (AF-1 gắn trang PDF, AF-2 trả lời). Sequence: `UC35_CommentOnMeetingDocument`. Góp ý gắn vào **tài liệu trong
một cuộc họp** (`MeetingDocument`): cùng tệp ở hai cuộc họp có hai luồng góp ý riêng.

**Quyền**: ai xem được cuộc họp thì đọc và viết được góp ý (`MeetingReadPolicy`, BR-249, BR-275) — kể cả Người duyệt và
Người tổ chức. Không xem được cuộc họp, tài liệu đã gỡ, hoặc tài liệu của cuộc họp khác → `404 MSG156` (cùng body).

### `GET /api/meetings/{id}/documents/{meetingDocumentId}/comments`

`200`:

```json
{
  "meetingDocumentId": "…",
  "canComment": true,
  "comments": [
    {
      "id": "…", "body": "Xem lại số liệu trang này", "pageNumber": 3,
      "author": { "userId": "…", "guestInvitationId": null, "name": "Nguyễn Văn A", "isGuest": false },
      "createdAt": "2026-10-27T03:15:00+00:00",
      "replies": [ { "id": "…", "body": "Đã sửa", "pageNumber": null, "author": { "…": "…" }, "createdAt": "…", "replies": [] } ]
    }
  ]
}
```

- Góp ý gốc xếp cũ trước; trả lời nằm trong `replies` của góp ý gốc, cũ trước, **một cấp** (BR-317).
- `canComment`: `false` khi cuộc họp đã kết thúc hoặc đã huỷ — ẩn ô viết (BR-282).
- `author.isGuest` + `guestInvitationId`: góp ý của Guest đã xác minh, `name` là tên tự khai (ADR-0016) — FE hiện kèm nhãn
  Khách mời.
- Lỗi: `404 MSG156`, `500 MSG204`.

### `POST /api/meetings/{id}/documents/{meetingDocumentId}/comments`

Body `{ "body": "…", "pageNumber": 3, "parentCommentId": null }`. Thứ tự kiểm theo sequence:

1. Nội dung cắt khoảng trắng, không trống (`400 MSG200`, BR-246), ≤ 4.000 ký tự (`400 MSG348`).
2. Dưới khoá chia sẻ (`FOR SHARE`) trên dòng cuộc họp: xem được cuộc họp và tài liệu còn trong cuộc họp (`404 MSG156`),
   cuộc họp chưa kết thúc (`409 MSG251`) và chưa huỷ (`409 MSG349`), kể cả khi vừa kết thúc/huỷ lúc đang gửi. Các góp ý
   không phải chờ nhau.
3. Trả lời (AF-2): `parentCommentId` phải là góp ý của chính tài liệu này (không thì `404 MSG156`); trả lời một câu trả
   lời thì vào luồng của góp ý gốc.
4. `pageNumber` chỉ giữ với **PDF**, ở góp ý gốc, và trong khoảng `1…pageCount` nếu biết số trang; ngoài ra lưu `null`
   (DOCX không có trang, BR-248). Trả lời không mang trang riêng.
5. Ghi tác giả và thời điểm (BR-250). Lỗi lưu `503 MSG202`.

`201 { "code": "MSG199", "message": "Đã gửi góp ý.", "comment": { …như một phần tử của GET, replies rỗng… } }`.

### Note cho FE (SCRUM-23)

- `commentCount` ở danh sách tài liệu (tab Tài liệu, panel phòng họp) đếm cả góp ý và trả lời; gửi góp ý **không** phát
  `documentsChanged`, nên số này cập nhật ở lần tải lại danh sách.
- Trang PDF: gửi `pageNumber` là trang đang xem nếu người dùng chọn gắn trang; DOCX không gửi.
- `MSG251`/`MSG349` giữa chừng: tải lại góp ý (`canComment` sẽ là `false`) và giữ nội dung đang soạn để người dùng chép lại.

### Guest

Guest chưa có phiên đăng nhập (UC-04), nên **chưa gọi được** hai endpoint này. Mô hình đã có tác giả Guest
(`DocumentComment.GuestInvitationId`, check đúng một tác giả) và `GET` đã trả tên + nhãn Guest; khi UC-04 có phiên,
endpoint cần nhận danh tính Guest qua `IMeetingGuestIdentityResolver` như Agenda và kiểm lời mời còn hiệu lực ở mỗi lần
gửi (thu hồi lời mời → `MSG156`, góp ý cũ giữ tác giả).

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

Từ SCRUM-58, xoá khỏi kho (UC-71a) và gỡ khỏi cuộc họp (UC-33c) là hai đường đưa một tệp về trạng thái không còn ai
tham chiếu; job vẫn được kiểm bằng `OrphanFilePurgerTests`.

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
mục chỉ đổi liên kết của mục đó. UC-33b/UC-33d (SCRUM-58) có endpoint gắn/gỡ riêng; SCRUM-58 không thêm migration.

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
- `MeetingDocumentChangeEndpointsTests` (SCRUM-58): thêm từ kho (tên hiện tại, không chép byte, ID trùng gộp); không
  chọn / tài liệu của người khác → `MSG249`, tệp đã có → `MSG165`; Thành viên `MSG162`, người ngoài `MSG174`, lỗi lưu
  `MSG166`, đã kết thúc `MSG172`; gỡ giữ góp ý, xoá liên kết mục, phát `documentsChanged`, gỡ lại → `MSG170`; thêm lại tệp
  đã gỡ là dòng mới không có góp ý cũ; gỡ: Thành viên `MSG169`, tài liệu của cuộc họp khác `MSG170`, lỗi lưu `MSG171`, đã
  kết thúc `MSG248`; gắn mục giữ liên kết mục khác, gắn lại `MSG243`; mục của cuộc họp khác `MSG242`, có tài liệu đã gỡ /
  mảng rỗng `MSG170` không gắn gì; Thành viên `MSG240`, lỗi lưu `MSG244`, đã kết thúc `MSG247`; từ kho thêm và gắn một
  lần, `MSG165`, `MSG249`, lỗi lưu `MSG166`; tải từ máy vào mục (thư mục cuộc họp, liên kết), mục không có → `MSG242`
  trước khi đọc body, tệp sai `MSG164`, Thành viên `MSG240`; gỡ khỏi mục từng liên kết rồi về nhóm chung, gỡ lại `MSG246`;
  Thành viên `MSG241`, tài liệu đã gỡ `MSG170`, mục lạ `MSG242`, lỗi lưu `MSG245`, đã kết thúc `MSG247`. Cuộc họp đã
  huỷ: cả 7 thao tác (kể cả `/upload`) → `MSG349`, không gì thay đổi; thêm, tải lên, gắn, từ kho, tải vào mục, gỡ liên
  kết đều phát `documentsChanged`; Chủ toạ đã bị loại và Chủ toạ cuộc họp khác → `MSG174`, Thư ký bị khoá tài khoản → `403`.
- `DocumentCommentEndpointsTests` (SCRUM-23): góp ý trang 3 trên PDF, DOCX và trang ngoài số trang không giữ trang,
  `commentCount`; trả lời một câu trả lời vào luồng gốc, cha lạ → `MSG156`; trống `MSG200`, quá dài `MSG348`; đã kết thúc
  `MSG251` và `canComment: false`; đã huỷ `MSG349` và `canComment: false`; Người tổ chức và người duyệt góp ý được; người ngoài, tài liệu của cuộc họp khác, tài liệu đã gỡ → `MSG156` (góp ý cũ vẫn trong
  DB); cùng tệp ở hai cuộc họp không thấy góp ý của nhau; lỗi lưu `MSG202`, lỗi đọc `MSG204`.
- `OrphanFilePurgerTests`: chỉ dọn tệp không còn tham chiếu và quá thời gian chờ; xoá lỗi thì để lượt sau;
  100 tệp xoá lỗi không chặn tệp thứ 101; thời gian chờ cấu hình 0 vẫn giữ tệp mới tải 30 phút.
