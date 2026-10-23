# SCRUM-107 · Tải tài liệu từ máy lên cuộc họp

UC-33a AF-1 *Upload files from the computer*. Byte lưu trên Cloudflare R2, mọi lượt tải lên đi qua
backend ([ADR-0024](../05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md)); mô hình dữ liệu theo
[ADR-0018](../05-decisions/0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md) và
[ERD §2](../02-design/02-erd.md). Sequence: [UC33a_UploadDocumentsToMeeting](../06-sequencediagram/document-management/UC33a_UploadDocumentsToMeeting.txt).

Danh sách tài liệu, xem trước và tải về thuộc SCRUM-21; tải vào kho và chọn từ kho thuộc SCRUM-58.

## Endpoint

`POST /api/meetings/{meetingId}/documents/upload` — `multipart/form-data`, cần bearer token.

| Part | Lặp | Ý nghĩa |
|---|---|---|
| `files` | 1–10 | Tệp. Server **không** tin tên đuôi hay `Content-Type` của part; chỉ đọc nội dung |
| `pageCounts` | mỗi `files` một lần, cùng thứ tự | Số trang PDF FE đếm bằng pdf.js. Chuỗi rỗng nếu không đếm được hoặc là DOCX |

Server cũng nhận tên `files[]` / `pageCounts[]`. `pageCounts[i]` ứng với part tệp thứ `i` trong `files`; part tệp mang
tên khác bị bỏ qua. Một request tối đa 64 part (tệp + giá trị).

### Thứ tự kiểm tra

1. Token, tài khoản đang hoạt động — không đạt: `401` / `403` body rỗng.
2. **Quyền, trước khi đọc body** (để người không có quyền không đẩy được 200 MB lên đĩa server):
   xem được cuộc họp, đang là Chủ toạ hoặc Thư ký hiện tại, cuộc họp chưa `Ended`.
3. `Content-Length` > 201 MB → `MSG288` ngay, không đọc body.
4. Số tệp ≤ 10, tổng ≤ 200 MB (BR-319).
5. Từng tệp: tên hợp lệ có đuôi `.pdf` hoặc `.docx`, không rỗng, ≤ 50 MB, và **nội dung** đúng loại mà đuôi
   khai (BR-187, BR-214, BR-215). Đuôi phải khớp nội dung vì tệp sẽ được tải về dưới chính tên đó (SCRUM-21):
   một trang HTML không được quay về từ origin của mình dưới tên `x.html`.
6. Đẩy từng tệp lên R2, rồi ghi DB trong một transaction có khoá cuộc họp, **kiểm lại quyền và trạng
   thái dưới khoá**. Lỗi ở bất kỳ bước nào thì xoá các object đã đẩy trong lần đó (BR-194, BR-286).

### Nhận diện loại tệp

| Loại | Điều kiện | `contentType` lưu |
|---|---|---|
| PDF | Tệp **bắt đầu** bằng `%PDF-` (offset 0; không chấp nhận rác phía trước dù đặc tả PDF cho phép) | `application/pdf` |
| DOCX | zip có central directory ≤ 5.000 entry và ≤ 1 MB, có `word/document.xml`, và trong `[Content_Types].xml` chính `Override` của `/word/document.xml` khai `…wordprocessingml.document.main+xml` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |

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
| 503 | `MSG166` | Không lưu được (R2 lỗi, DB lỗi, hoặc server chưa cấu hình kho tệp). Không tệp nào được thêm |

`errors` của `MSG164`:

```json
{ "code": "MSG164", "message": "…", "errors": { "files[1]": ["anh.pdf: không phải tệp PDF hoặc DOCX hợp lệ."] } }
```

Khoá là `files[i]` theo thứ tự part tệp; key `files` dùng cho lỗi cả request (chưa chọn tệp, sai content type).

## Note cho FE

- Gửi `FormData`: với mỗi tệp `append('files', file)` rồi `append('pageCounts', n ?? '')`. Không đặt
  header `Content-Type` tay — trình duyệt tự thêm boundary.
- Chặn sớm trong hộp thoại (10 tệp, 200 MB, 50 MB, đuôi `.pdf`/`.docx`), nhưng vẫn xử lý `MSG164`/`MSG288`
  từ server: server kiểm theo nội dung, FE chỉ thấy đuôi.
- Đọc `errors["files[i]"]` để đánh dấu đúng tệp trong danh sách đã chọn.
- `MSG162`/`MSG172`/`MSG174` có thể đến **sau** khi người dùng đã bấm tải (mất vai trò, cuộc họp vừa kết
  thúc): đóng hộp thoại và tải lại cuộc họp.
- Tải 200 MB qua server 1 GB mất thời gian — hiện tiến độ bằng `XMLHttpRequest.upload.onprogress`
  (hoặc tương đương). Request bị huỷ giữa chừng thì không tệp nào được thêm.
- Nút **Tải lên** chỉ hiện cho Chủ toạ/Thư ký hiện tại khi cuộc họp chưa `Ended`; đây chỉ là hiển thị,
  server luôn kiểm lại.
- `documentIds` của Agenda (`editor-options`, POST/PUT item) giờ là **MeetingDocument ID**, cùng loại
  `id` trả ở đây.

## Lưu trữ và cấu hình

Section `FileStorage` (ADR-0024). `ServiceUrl`, `BucketName`, `AccessKeyId`, `SecretAccessKey` là bí
mật — `dotnet user-secrets` khi dev, biến môi trường `FileStorage__*` khi deploy, không đặt trong
`appsettings.json`. Thiếu một trong bốn giá trị thì app vẫn chạy, upload trả `503/MSG166` và job dọn
đứng yên. Hướng dẫn: [03-co-so-du-lieu.md §5](../02-design/03-co-so-du-lieu.md#5-kho-tệp-tài-liệu) và
[deploy/README.md](../../deploy/README.md).

- Tệp trên 64 KB được ASP.NET Core đệm ra tệp tạm trên đĩa, không giữ trong RAM; part nhỏ hơn nằm trong RAM nên
  số part bị giới hạn 64. Giới hạn body chỉ nâng lên 201 MB cho riêng endpoint này (Kestrel mặc định 30 MB).
- Tên tệp được lưu nguyên văn (chỉ bỏ phần đường dẫn). Endpoint tải về của SCRUM-21 phải mã hoá tên trong
  `Content-Disposition` (`filename*=UTF-8''…`) thay vì chèn thẳng.
- Kho tệp có thêm `OpenReadAsync(key)` đọc object theo luồng cho SCRUM-21. CSP của server test không đổi.

### Job dọn file mồ côi (BR-213)

`OrphanFilePurgeWorker` chạy lúc khởi động rồi mỗi `FileStorage:OrphanPurgeIntervalMinutes` (mặc định
60). Mỗi lượt lấy tối đa 100 `StoredFile` thoả: `PurgedAt` rỗng, không còn `Document`, không còn
`MeetingDocument` có `RemovedAt` rỗng, `UploadedAt` cũ hơn `FileStorage:OrphanGracePeriodHours` (mặc
định 24). Xoá object rồi ghi `PurgedAt`; **giữ** dòng `StoredFile`. Xoá lỗi thì để lượt sau thử lại.

Ticket này chưa có đường xoá khỏi kho hay gỡ khỏi cuộc họp (SCRUM-58), nên job được kiểm bằng
`OrphanFilePurgerTests`.

## Dữ liệu cá nhân (Nghị định 13/2023/NĐ-CP)

Tài liệu cuộc họp có thể chứa dữ liệu cá nhân, mà R2 lưu ngoài Việt Nam. Server test chỉ dùng dữ liệu
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

Agenda giữ hợp đồng cũ: mỗi tài liệu vẫn gắn tối đa một mục khi lập chương trình. Bảng nối cho phép
nhiều mục (ADR-0020 §5); UC-33b/UC-33d (SCRUM-58) mở rộng phần đó.

## Kiểm thử

`tests/TrustCabinet.Web.Tests/Documents/`:

- `MeetingDocumentEndpointsTests` (PostgreSQL thật, kho tệp trong bộ nhớ): 3 PDF + 1 DOCX; Chủ toạ;
  ảnh đổi đuôi, DOCM, zip thiếu `word/document.xml`; 11 tệp; body khai > 200 MB; tệp 51 MB; không
  tệp / không multipart; Thành viên, Người tổ chức, người ngoài, cuộc họp không tồn tại; Thư ký đã bị
  gỡ, tài khoản bị khoá; cuộc họp đã kết thúc và kết thúc giữa lúc tải; R2 lỗi ở tệp thứ hai; DB lỗi
  sau khi object đã lên; xoá dọn cũng lỗi; chưa cấu hình kho; dùng lại thư mục sau khi đổi tên/chuyển,
  tạo lại sau khi xoá; Agenda liệt kê tài liệu vừa tải.
- `DocumentFileRulesTests`: nhận diện theo nội dung (HTML giả PDF, DOCM giả DOCX, central directory quá lớn),
  đuôi phải khớp nội dung, tên tệp, ngày thư mục theo giờ Việt Nam, giới hạn tổng kiểm trước khi đọc tệp,
  `pageCount` chỉ cho PDF.
- Thêm ở endpoint: mất vai trò Thư ký giữa lúc tải → `MSG162` và object bị xoá; Thành viên gửi tệp sai → `MSG162`
  (quyền kiểm trước); HTML giả PDF và PDF mang tên `.html` → `MSG164`.
- `OrphanFilePurgerTests`: chỉ dọn tệp không còn tham chiếu và quá thời gian chờ; xoá lỗi thì để lượt sau.
