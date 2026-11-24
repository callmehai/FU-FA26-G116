# Bộ test report TrustCabinet theo mẫu

Sáu file Office trong thư mục này được sinh **bằng tiếng Anh**, theo yêu cầu của người dùng.
Nguồn định dạng là sáu file mẫu của môn học, đặt trong một thư mục trên máy (`--template-dir`, mặc định
là thư mục Downloads của người chạy); script giữ style, theme, bố cục bảng và các phần cố định của mẫu.
Nội dung của dự án mẫu, người kiểm thử và kết quả mẫu được thay bằng nội dung TrustCabinet.

| File | Nội dung |
|---|---|
| `Report5.0_Test_Documentation.docx` | Phạm vi, chiến lược, kế hoạch, testcase và báo cáo, theo thứ tự mục của Word mẫu |
| `Report5.1_Unit_Test.xlsx` | Test unit theo hàng: mỗi production function một sheet, mỗi sheet các nhóm `Class.Method` và các dòng `UTCnnn`; sinh từ `cases/FE-xx/unit/` |
| `Report5.2_Integration_Test.xlsx` | Test tích hợp qua giao diện, mỗi Feature một sheet; sinh từ `cases/FE-xx/ui.json` |
| `Report5.2.2_Integration_Test_API_Test.xlsx` | Test API, mỗi Feature một sheet, mỗi *endpoint* một nhóm; sinh từ `cases/FE-xx/api.json` |
| `round1/` | Tài liệu làm việc Round 1 (checklist, bug, kế hoạch, prompt, ánh xạ AC, báo cáo agent); mục lục ở [round1/README.md](round1/README.md). Không phải nguồn của workbook |
| `../../../scripts/test_reports/apply_trx.py` | Đọc file `.trx` của `dotnet test` và ghi `round1`/`date1`/`tester1` vào `cases/FE-xx/api.json` cho case có `Automated:`
| `Report5.3_System_Test.xlsx` | Các workflow kiểm thử toàn hệ thống; bảng ba vòng theo mẫu |
| `Report5.4_Acceptance_Test.xlsx` | Bốn sheet gốc: Cover, Statistics, Customer Feedback Statistics, Customers Feedbacks |

**5.2 / 5.2.2 viết lại theo Feature (FE-01 → FE-16), test case nghiệp vụ.** Nguồn là JSON trong
[cases/](cases/README.md), không phải file Excel: sửa JSON rồi chạy

```powershell
python scripts/test_reports/integration_cases.py check   # kiểm tra
python scripts/test_reports/integration_cases.py build   # sinh lại 5.2 và 5.2.2 (đóng Excel trước)
```

Agent đọc [AGENTS.md](AGENTS.md) trước — cấu trúc sheet, cách viết case và cách chia việc.

**Cả 5.2 và 5.2.2 sinh từ `cases/`** (nhóm thống nhất ngày 20/11/2026; bản 1.2 làm tay — 13 sheet, 411 case `XXX_IT01` — đã bỏ, ID cũ không còn). Kết quả chạy test ghi vào JSON (`round1`, `date1`, `tester1`…) chứ không gõ vào Excel, vì mỗi lần `build` workbook được dựng lại từ file mẫu.

Số case trong `cases/` sau khi thu gọn (commit `03594e25` ngày 23/11/2026; nguồn của 5.2.2 và của bản 5.2 chia theo Feature; FE-14 không có sheet vì UC-69 đã bỏ). API: 825 case, 825 Passed (TRX 24/11 trên build local). UI: 563 case, 563 Passed, 0 Pending (REC_FT24–27 test tay 25/11, không ảnh evidence). Round 1 Báo cáo chạy lại 24/11 trên màn mới: 13/13 desktop + 13/13 phone. Mọi vòng đều `Pending` cho tới khi chạy thật:

| Feature | Sheet | 5.2 UI | 5.2.2 API |
|---|---|---:|---:|
| FE-01 | Authentication | 26 | 34 |
| FE-02 | User Management | 33 | 36 |
| FE-03 | Access Control | 28 | 33 |
| FE-04 | Meeting Management | 44 | 66 |
| FE-05 | Agenda | 29 | 46 |
| FE-06 | Participants | 50 | 84 |
| FE-07 | Documents | 51 | 91 |
| FE-08 | Online Room | 54 | 73 |
| FE-09 | Recording | 48 | 60 |
| FE-10 | Transcription & Summary | 64 | 101 |
| FE-11 | Voting | 21 | 44 |
| FE-12 | Minutes | 32 | 51 |
| FE-13 | Notifications | 24 | 31 |
| FE-15 | Reporting | 18 | 24 |
| FE-16 | Calendar | 41 | 51 |
| | **Tổng** | **563** | **825** |

`generate-test-reports.cmd` bên dưới không còn đụng tới 5.1 / 5.2 / 5.2.2; nó chỉ sinh 5.0, 5.3, 5.4.

**5.1 viết lại theo production function (mỗi function một sheet), test case nghiệp vụ.**
Nguồn là JSON trong [cases/](cases/README.md), không phải file Excel: sửa JSON rồi chạy
5.1 mô tả code nhánh `app` (kiến trúc bốn project, ADR-0065), không phải cây làm việc hiện tại:
`cases/functions.json` (`source.ref`) ghim SHA commit cố định của `app` mà UTC được viết theo;
đổi ref khi rebase sang commit `app` mới và chạy `relink` để ghim lại `file:line`.

```powershell
python scripts/test_reports/unit_cases.py check            # kiểm tra
python scripts/test_reports/unit_cases.py relink           # ghim lại file/line sau khi refactor dời code (check báo dòng thật khi lệch)
python scripts/test_reports/unit_cases.py sync-back        # gom kết quả Round/Test date/Tester đã chạy thật từ workbook cũ vào JSON (theo mã UTC)
python scripts/test_reports/unit_cases.py build            # dry-run: kiểm tra rồi in diff workbook cũ so với JSON (không ghi file)
python scripts/test_reports/unit_cases.py build --overwrite  # sao lưu bản cũ thành .bak-YYYYMMDD-HHMMSS rồi sinh lại workbook (đóng Excel trước)
```

Quy trình bắt buộc khi sinh lại workbook: chạy `build` (dry-run) → trình diff cho người dùng → chờ OK → chạy `sync-back` (nếu workbook cũ có kết quả) → chạy `build --overwrite`.

Mỗi UTC gọi trực tiếp production function (in-process), không qua browser hay HTTP.
`Expected Results` luôn có ba dòng `Return: …`, `Exception: …`, `Log Message: …`.
Mọi vòng đều bắt đầu là `Pending`; chỉ một lần chạy thật mới được đánh `Passed`/`Failed`.

UTC được viết từ chính production function — chữ ký hàm, code tại `file:line`,
`docs/03-api/` — không sao chép input/giá trị/tên method từ file dưới `tests/`.
Cột `Note` chỉ ghi truy xuất `UC-xx · BR-xx · MSGnn` (kèm `Deviation:` khi code lệch UC);
tuyệt đối không ghi `Automated:`, đường dẫn file test hay tên test method vào bất kỳ trường nào.

Trong Acceptance Test, giữ nguyên bảng tiêu chí/phản hồi ngắn của mẫu. Di chuột hoặc mở
Notes tại ô tiêu chí để xem precondition, procedure, expected result và source của từng kịch bản.

## Sinh lại

Tại thư mục gốc repository:

```powershell
.\scripts\generate-test-reports.cmd --overwrite
```

Đổi thư mục mẫu hoặc nơi xuất:

```powershell
.\scripts\generate-test-reports.cmd --template-dir "<thư mục chứa file mẫu>" --output-dir "artifacts\test-report-copy"
```

Script cần .NET SDK theo `global.json`, Python với dependency trong
`scripts/test_reports/requirements.txt`, Node và `@oai/artifact-tool`. File `.cmd` tự chọn
runtime Codex đã có trên máy; máy khác có thể truyền `--node` và `--artifact-module`.
Máy sinh báo cáo cần Windows có Microsoft Excel để render hình minh họa theo đúng mẫu.
Microsoft Word dùng để tự động xuất PDF, lấy số trang và cập nhật cache mục lục sau khi sinh.
Các file mẫu gốc chỉ được đọc và không được đưa vào repository.

Roslyn đọc cú pháp C# để lấy test method, dữ liệu Theory, fixture, assertion và vị trí source
cho 5.0/5.3/5.4. Script phân loại theo dependency; BA/Tester cần rà phân loại và mapping Jira/UC.
Workbook 5.1 mới không đếm định nghĩa test method và không suy số method thành code coverage:
mỗi UTC là một hành vi của production function, gọi trực tiếp. Dữ liệu kiểm thử và đoạn code
trích nguyên văn có thể chứa chuỗi tiếng Việt của sản phẩm; phần diễn giải báo cáo là tiếng Anh.

## Kết quả và evidence

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
