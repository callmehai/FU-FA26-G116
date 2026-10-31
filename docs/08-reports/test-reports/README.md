# Bộ test report TrustCabinet theo mẫu

Sáu file Office trong thư mục này được sinh **bằng tiếng Anh**, theo yêu cầu của người dùng.
Nguồn định dạng là sáu file mẫu trong `C:\Users\nguye\Downloads`; script giữ style, theme,
bố cục bảng và các phần cố định của mẫu. Nội dung dự án ACCE, người kiểm thử và kết quả mẫu
được thay bằng nội dung TrustCabinet. Riêng **phông chữ được thống nhất về Times New Roman** (theo
yêu cầu, 30/10/2026): các mẫu gốc dùng lẫn Tahoma, Calibri, MS PGothic, Cambria, Arial…, nên sau khi
sinh, `scripts/test_reports/fonts.py` đổi mọi phông trong ô, đoạn văn, theme, biểu đồ và hình
phản hồi về một phông; cỡ chữ, màu và bố cục giữ nguyên. Ảnh chụp mã nguồn trong 5.0 vẫn dùng
Consolas vì là mã.

| File | Nội dung |
|---|---|
| `Report5.0_Test_Documentation.docx` | Phạm vi, chiến lược, kế hoạch, testcase và báo cáo, theo thứ tự mục của Word mẫu |
| `Report5.1_Unit_Test.xlsx` | Theo mẫu mới `Report-5.1_Unit Test.xls`: Cover, Test Cases, Test Statistics (kèm hai biểu đồ) và một sheet cho mỗi module |
| `Report5.2_Integration_Test.xlsx` | Test chức năng qua giao diện web (FE + BE), một sheet mỗi chức năng; bảng ba vòng theo mẫu |
| `Report5.2.2_Integration_Test_API_Test.xlsx` | Test API cho người chạy bằng Postman, nhóm theo endpoint; bảng ba vòng theo mẫu |
| `Report5.3_System_Test.xlsx` | Các workflow kiểm thử toàn hệ thống; bảng ba vòng theo mẫu |
| `Report5.4_Acceptance_Test.xlsx` | Bốn sheet gốc: Cover, Statistics, Customer Feedback Statistics, Customers Feedbacks |

**Report 5.1 theo mẫu mới (29/10/2026).** Mẫu là file `.xls`; script nhờ Excel lưu một bản `.xlsx`
vào `artifacts/test-report-build/template-edition/templates/` rồi sinh từ bản đó, file mẫu gốc không
bị sửa. Mỗi module (`Access`, `Meetings`, …) là một sheet. Trong sheet, mỗi test method là một dòng
tiêu đề in đậm, bên dưới là các dòng `UTCnnn`: một dòng cho `Fact`, một dòng cho mỗi `InlineData`
của `Theory`. *Input parameters* ghi **giá trị thật** truyền vào, mỗi tham số một dòng
`tên = giá trị` (`email = "thuky@trustcabinet.local"`, `roleId = 01a0ce78-7204-…`):
`scripts/test_reports/values.py` lần theo biến, field, hằng số (`BuiltInRoles.Member`) và helper
(`NewUser()`, `ValidInput(...)`) trong source tới giá trị gốc, và thay giá trị `InlineData` vào lời gọi.
Id mà test sinh lúc chạy (`Guid.CreateVersion7()`) không có giá trị cố định nên ghi một id mẫu kèm
`(generated)`; giờ tính từ `DateTimeOffset.UtcNow` được tính theo ngày sinh báo cáo và ghi chú như vậy.
*Expected Results* theo mẫu `Return / Exception / Log Message` và chỉ ghi kết quả.
Cột *Type* (Normal/Abnormal/Boundary) **được suy ra từ tên test và dữ liệu** — tester cần rà lại.
Danh sách chọn trạng thái thêm `Failed` (mẫu gốc thiếu) để khớp công thức đếm. Test Statistics đếm
theo **test case (UTC)**, không phải theo method, nên tổng của 5.1 lớn hơn số method UNIT trong
`manifest.json` (`unitCases`).

**5.2 và 5.2.2 là testcase viết tay cho người test thật**, theo mẫu G158, nằm trong
`scripts/test_reports/cases/`:

- `api-*.json` — mỗi test API xUnit có đúng một case (khoá theo id `TC-…` trong `inventory.json`),
  mã đọc được `AUTH_API_IT01`… Dạng gọn `req` (method, URL, người gửi, body) tự sinh bốn bước
  request · headers · query · body như mẫu. Người test dùng tài khoản seed (`thuky`, `chutoa`,
  `thanhvien`, `quantri` `@trustcabinet.local`, mật khẩu demo `TrustCabinet@2026`) và câu thông báo
  thật trong [danh mục mã](../../03-api/00-danh-muc-ma-thong-bao.md).
- `ui-*.json` — test chức năng qua trình duyệt, mỗi file một sheet của 5.2 (`AUTH_FT01`…), dùng đúng
  nhãn nút và câu trên màn hình.

Test xUnit mức INTEGRATION (migration, Redis, model EF…) là kiểm tra hạ tầng tự động: chạy bằng
`dotnet test`, ghi trong `inventory.json`, không đưa vào 5.2 vì người không chạy tay được.
`verify_reports.py` kiểm mỗi test API có case và mỗi case viết tay trỏ tới test có thật.

**Cách viết các ô còn lại (5.1, 5.3).** `scripts/test_reports/readable.py` đọc source test và viết
lại thành câu; *Pre-conditions*, *Procedure* và *Expected Results* đánh số `1.`, `2.`, …; không dùng
dấu `` ` ``. Không có comment/Notes và cột *Note* để trống.

## Sinh lại

Tại thư mục gốc repository:

```powershell
.\scripts\generate-test-reports.cmd --overwrite
```

Đổi thư mục mẫu hoặc nơi xuất:

```powershell
.\scripts\generate-test-reports.cmd --template-dir "C:\Users\nguye\Downloads" --output-dir "C:\github\TrustCabinet\artifacts\test-report-copy"
```

Script cần .NET SDK theo `global.json`, Python với dependency trong
`scripts/test_reports/requirements.txt`, Node và `@oai/artifact-tool`. File `.cmd` tự chọn
runtime Codex đã có trên máy; máy khác có thể truyền `--node` và `--artifact-module`.
Máy sinh báo cáo cần Windows có Microsoft Excel để render hình minh họa theo đúng mẫu.
Microsoft Word dùng để tự động xuất PDF, lấy số trang và cập nhật cache mục lục sau khi sinh.
Các file mẫu gốc chỉ được đọc và không được đưa vào repository.

Roslyn đọc cú pháp C# để lấy test method, dữ liệu Theory, fixture, assertion và vị trí source.
Script phân loại theo dependency; BA/Tester cần rà phân loại và mapping Jira/UC. Một method
là một định nghĩa, có thể sinh nhiều execution. Workbook Unit đếm định nghĩa; không suy số
method thành code coverage. Dữ liệu kiểm thử và đoạn code trích nguyên văn có thể chứa chuỗi
tiếng Việt của sản phẩm; phần diễn giải báo cáo là tiếng Anh.

## Kết quả và evidence

**Bản hiện tại (30/10/2026):** sinh từ commit `1579fe72` (đã gồm `main` tới `94427943`) với 1129 test
method (447 Unit — 653 test case, 203 Integration, 479 API) và 89 case UI, 10 SYS, 8 UAT, **chưa ghi kết quả**: mọi Round đều `Pending`, cột Test date và
Tester để trống cho tới khi form được chốt và chạy test thật. Khi có kết quả, tester là HuyNT
(`TESTER` trong `readable.py`) và ngày test chỉ ghi `dd/mm/yyyy`.

Mặc định không chạy test và không đánh dấu Passed. Có thể chạy trên hạ tầng kiểm thử riêng:

```powershell
.\scripts\generate-test-reports.cmd --overwrite --run-tests
```

Test database có thể bị fixture tạo/xóa; chỉ cấu hình PostgreSQL/Redis dành cho test.
Lệnh này lưu TRX và source fingerprint dưới `artifacts/test-report-build/template-edition`.
Để nhập một lần chạy đã lưu:

```powershell
.\scripts\generate-test-reports.cmd --overwrite --trx "C:\path\run.trx" --evidence-manifest "C:\path\evidence-manifest.json"
```

Manifest evidence phải chứa `sourceDigest` và `trxSha256` khớp. Theory chỉ Passed khi đủ
biến thể đã biết và không trùng execution; dữ liệu động chưa xác định tổng số giữ Partial.
Kết quả chưa xác thực không được chuyển thành Passed.

Kết quả thủ công nhập qua CSV với cột `id,status,tester,executedAt,actual,evidence,defect`
và tham số `--manual-results`. Passed/Failed cần người chạy, thời điểm, actual và evidence.
Chỉ nhập vòng đầu; vòng 2/3 trong workbook để trống cho các đợt kiểm thử tiếp theo.
Pending trong tổng hợp bao gồm NotRun/Partial/Blocked; N/A bao gồm Skipped. Những tỷ lệ trong
mẫu là tỷ lệ thực thi và đạt trên tổng định nghĩa, không phải code coverage.

`inventory.json` lưu dữ liệu đầy đủ; `execution-evidence.json` lưu execution đã nhập;
`manifest.json` lưu commit, fingerprint và hash báo cáo. Sửa trực tiếp workbook sẽ không
được nhập ngược vào source khi sinh lại.

## Kiểm tra hiển thị

Preview và mốc đối chiếu template nằm dưới `artifacts/test-report-build/template-edition`.
Word được mở chỉ đọc để cập nhật field trong bộ nhớ và xuất PDF kiểm tra; script chỉ đưa số
trang mục lục đã xác minh trở lại DOCX, giữ các phần OOXML còn lại theo mẫu. Có thể dùng
`scripts/test_reports/render_word.ps1` để kiểm tra lại bằng Microsoft Word. Các field cũng
được yêu cầu cập nhật khi mở tài liệu.
