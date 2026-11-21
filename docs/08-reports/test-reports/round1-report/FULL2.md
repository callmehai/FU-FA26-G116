# Round 1 — Chạy lại chính thức lần 2 (FULL2)

Ngày chạy: 2026-11-21. Người chạy: Agent FULL2. Tester Round 1: Phạm Gia Khánh.
Mã nguồn: cùng một bản mã với lần F1 (không sửa code production, không sửa file test có sẵn,
không commit, không động tới git).

## 1. Lệnh đã chạy và kết quả từng bước

| # | Lệnh | Kết quả |
|---|---|---|
| 0 | `New-Item -ItemType Directory -Path C:\tc-temp -Force` | Tạo thư mục `C:\tc-temp`, ghi thử đọc lại thành công dưới user `admin`. |
| 1 | `docker ps`, kiểm tra cổng 55432/5432/6379/8333 | Redis (`trustcabinet-redis`, 6379) và SeaweedFS (`trustcabinet-files`, 127.0.0.1:8333) đang `Up`. Cổng 55432 mở — cụm PostgreSQL 18 của F1 trong `%TEMP%\tc-pg-f1` vẫn chạy (tiến trình `postgres` còn sống, `psql` kết nối được). Cổng 5432 cũng mở (service `postgresql-x64-18` của máy) nên không bật service `postgres` của compose. Không dừng, không tạo container nào. |
| 2 | `dotnet build TrustCabinet.sln -warnaserror` | Exit 0. **0 Warning, 0 Error** (lần F1 còn 1 warning RDG002; lần này build tăng dần không báo lại, kết quả vẫn đạt vì exit 0). |
| 3 | `dotnet test TrustCabinet.sln --logger "trx;LogFileName=round1-full2.trx" --results-directory TestResults -p:_MSTestEnableParentProcessQuery=false` với `TEMP=TMP=C:\tc-temp`, `TRUSTCABINET_TEST_CONNECTION_STRING=Host=localhost;Port=55432;…`, `TRUSTCABINET_TEST_REDIS=localhost:6379`, `Logging__EventLog__LogLevel__Default=None` | Web.Tests: **2800 passed / 153 failed / 0 skipped (tổng 2953)**, thời gian khoảng 4 phút 43 giây. File `TestResults/round1-full2.trx` (lưu ý `dotnet test` cả solution ghi đè trx giữa các project — file này giữ kết quả Web.Tests, giống hiện tượng F1 đã ghi). |
| 3b | Cùng biến môi trường, `dotnet test tests/TrustCabinet.Application.Tests --logger "trx;LogFileName=round1-app-full2.trx" …` | Application.Tests: **365/365/0**. File `TestResults/round1-app-full2.trx`. |
| 4 | `python scripts/test_reports/apply_trx.py TestResults/round1-full2.trx --dry-run`, rồi chạy thật (không giới hạn `--features`) | Dry-run: Passed 1334, Failed 203, untouched 0. Chạy thật: 15/15 Feature đều được ghi, tổng **1334 Passed / 203 Failed / 0 untouched / 0 Pending**. |
| 5 | `python scripts/test_reports/integration_cases.py check` | **0 error(s); 0 group(s) still without cases.** |
| 6 | `python scripts/test_reports/integration_cases.py build` | Xong: `Report5.2_Integration_Test.xlsx` (15 sheets, 875 case), `Report5.2.2_Integration_Test_API_Test.xlsx` (15 sheets, 1537 case), kèm preview. |

Biến môi trường và cờ dòng lệnh ở bước 3 đều là cấu hình chạy (được phép theo nhiệm vụ):
`Logging__EventLog__LogLevel__Default=None` tắt EventLog logger mà tiến trình Low-IL không ghi được;
`-p:_MSTestEnableParentProcessQuery=false` tắt watchdog "theo dõi tiến trình cha" của VSTest
(chính watchdog này crash trên máy này vì testhost Low-IL không mở được handle tiến trình cha).
Không sửa file test nào, không sửa fixture chung.

## 2. Tổng số test (lần chạy FULL2)

| Project (file trx) | Total | Passed | Failed | Skipped |
|---|---|---:|---:|---:|
| `TrustCabinet.Web.Tests` (`TestResults/round1-full2.trx`) | 2953 | 2800 | 153 | 0 |
| `TrustCabinet.Application.Tests` (`TestResults/round1-app-full2.trx`) | 365 | 365 | 0 | 0 |
| **Tổng Round 1 (FULL2)** | **3318** | **3165** | **153** | **0** |

## 3. So sánh với F1: tập fail trùng khớp 100%

Đã parse hai file trx (`round1-full.trx` của F1 và `round1-full2.trx`) và đối chiếu tên test fail:

- FULL2 fail 153 test, F1 fail 153 test; **phép trừ hai chiều đều rỗng** — không có test nào mới fail,
  không có test nào hết fail.
- Đếm message lỗi trong FULL2: **0 fail EventLog, 0 fail Redis**.
- Điểm khác duy nhất so với F1 là đường dẫn trong message TEMP-denial: F1 là
  `C:\Users\admin\AppData\Local\Temp\…`, FULL2 là **`C:\tc-temp\…`**. Tức là việc trỏ `TEMP`/`TMP`
  sang thư mục ghi được **không làm hết denial** — tiến trình test vẫn bị từ chối ngay cả trên
  thư mục mới tạo mà `pwsh` (cùng user `admin`) ghi đọc bình thường. Đây là bằng chứng mới củng cố
  kết luận của F1: lớp chặn nằm dưới DACL/MIC (hook/filter tiêm vào tiến trình, nghi vấn Windhawk),
  không phải do đường dẫn hay quyền thư mục, và cần quyền admin mới xác minh tiếp.

## 4. Nguyên nhân 153 test fail — toàn bộ là môi trường, không ghi nhận lỗi sản phẩm

| Nhóm | Số test | Dấu hiệu (lần FULL2) | Kết luận |
|---|---:|---|---|
| TEMP / file đĩa | 138 | `UnauthorizedAccessException: Access to the path 'C:\tc-temp\tmpXXXX.tmp' is denied.`, `'C:\tc-temp\tc-recordings-…\2026-11' is denied.`, `QuestPDF … Unable to find a suitable temporary storage location` (`TypeInitializationException` ở `GenerateExtensions`), fixture `SpaFallbackTests` ném ngay trong constructor. Tái hiện xác định, kể cả chạy lẻ. | Môi trường. Sản phẩm ghi file đúng logic; chỉ tiến trình chạy test bị chặn. |
| Knock-on từ TEMP (HTTP 500 khi export/ban hành biên bản) | 6 | `Expected: OK, Actual: InternalServerError` ở các test gọi `GET /api/minutes/{id}/export` (docx/pdf) hoặc ban hành (sinh PDF qua QuestPDF). Các assert 400/403 đứng trước trong cùng test vẫn pass. | Môi trường (hệ quả của nhóm TEMP). |
| Knock-on từ TEMP (bản ghi/speech-clip) | 6 | `Assert.NotNull: Value is Null`, null `AudioPath` trong DB, `ArgumentNullException path2`, `Expected 200, got 404` — vì bước upload trước đó 500 do TEMP-denial nên không có audio path để đọc. | Môi trường (hệ quả của nhóm TEMP). |
| MinutesText trên Windows | 3 | Xem riêng mục 5. | Môi trường (CRLF của Windows). |
| EventLog | 0 | Không còn fail nào (nhờ `Logging__EventLog__LogLevel__Default=None`). | Đã xử lý bằng cấu hình chạy. |
| Redis | 0 | Không còn fail nào (container `trustcabinet-redis` sẵn sàng, biến `TRUSTCABINET_TEST_REDIS=localhost:6379`). | Đủ hạ tầng. |
| Hub flake | 0 | Hai test `AgendaHub`/`AgendaGuest` từng flake ở lần 1 của F1 lần này pass. | Timeout dưới tải, không lặp lại. |

Phân bố 153 fail theo class (trích từ trx): `MeetingDocumentEndpointsTests` 20,
`TranscriptEndpointTests` 14, `TranscriptCoverageTests` 14, `MinutesFileTests` 11,
`RecordingEndpointTests` 11, `RoomTakeEndpointTests` 10, `SpaFallbackTests` 10,
`VoiceIdEndpointTests` 10, `PersonalLibraryEndpointsTests` 10, `MinutesEndpointTests` 7,
`CombinedRecordingTests` 5, `SpeechClipEndpointTests` 4, `RecordingRound1Tests` 4,
`ExportEndpointTests` 4, `MeetingDocumentChangeEndpointsTests` 3, `VoteDocumentRound1Tests` 3,
`MinutesTextTests` 3, `PdfPageCounterTests` 2, `VoteDocumentEndpointTests` 2,
`GuestSpeechClipTests` 2, `MinutesApprovalEndpointTests` 1, `DocumentRound1GapTests` 1,
`MeetingDocumentReadEndpointsTests` 1, `VoiceIdOneConsentTests` 1.

Không có test fail nào cho thấy logic sản phẩm sai: mọi fail đều trỏ về TEMP-denial/CRLF hoặc là
hệ quả trực tiếp của chúng. **Không có test nào fail do code sai, nên không giữ case Failed nào
vì lý do sản phẩm** — toàn bộ 203 case Failed trong JSON đều mang hậu tố môi trường do script ghi.

## 5. Ba test MinutesTextTests — lỗi môi trường (CRLF Windows), không phải lỗi sản phẩm

Ba test fail: `Ai_text_cannot_turn_into_a_title_a_table_row_or_a_centred_line`,
`Without_a_reviewed_draft_the_agenda_conclusions_and_votes_fill_the_sections_and_blanks_remain`,
`A_title_with_marks_or_line_breaks_stays_one_plain_line` (đều trong
`TrustCabinet.Web.Tests/Summaries/MinutesTextTests.cs`).

Ví dụ message: `Assert.Contains() … Not found: "^ **Kết quả cuộc họp Họp # chen @@ x | y**"`
trong khi Collection chứa `"^ **Kết quả cuộc họp Họp # chen @@ x | y**\r"`.
Test so khớp cả ký tự kết dòng: kỳ vọng dòng kết thúc kiểu LF (`\n`) nhưng runtime trên Windows
trả về dòng kết thúc CRLF (`\r\n`) nên `Contains` trượt đúng một ký tự `\r`.

Phân biệt:

- **Sản phẩm đúng:** bộ dựng văn bản biên bản (`MinutesText`) vẫn tách dòng, giữ tiêu đề, hàng bảng
  và căn giữa đúng luật — nội dung so khớp từng chữ, chỉ lệch ký tự xuống dòng của nền tảng.
- **Môi trường sai:** checkout/compile trên Windows sinh chuỗi `\r\n`; cùng test đó trên Linux
  (LF) pass. Đây chính là 3 test "fail sẵn trên Windows" mà checklist đã ghi nhận.
- **Cách xử lý đúng:** không sửa sản phẩm; khi chạy chính thức trên máy khác thì chạy 3 test này
  trên Linux, hoặc chuẩn hoá `\r\n`→`\n` trong assert của test (việc của chủ test, ngoài phạm vi FULL2).

## 6. Số case mỗi Feature sau apply

Snapshot trước apply: 1317 Passed / 0 Failed / 220 Pending (khớp checklist).
Sau apply (FULL2): **1334 Passed / 203 Failed / 0 N/A / 0 Pending** — trùng khớp từng con số với F1.

| Feature | Passed | Failed | N/A | Pending |
|---|---:|---:|---:|---:|
| FE-01 | 57 | 0 | 0 | 0 |
| FE-02 | 61 | 0 | 0 | 0 |
| FE-03 | 63 | 0 | 0 | 0 |
| FE-04 | 116 | 0 | 0 | 0 |
| FE-05 | 83 | 0 | 0 | 0 |
| FE-06 | 195 | 0 | 0 | 0 |
| FE-07 | 187 | 30 | 0 | 0 |
| FE-08 | 136 | 0 | 0 | 0 |
| FE-09 | 39 | 87 | 0 | 0 |
| FE-10 | 116 | 65 | 0 | 0 |
| FE-11 | 47 | 6 | 0 | 0 |
| FE-12 | 68 | 15 | 0 | 0 |
| FE-13 | 47 | 0 | 0 | 0 |
| FE-15 | 21 | 0 | 0 | 0 |
| FE-16 | 98 | 0 | 0 | 0 |

Ánh xạ Failed về nguyên nhân (giữ nguyên như F1 vì tập fail trùng khớp):
FE-07 30 case do TEMP-denial khi upload (`tmp*.tmp`);
FE-09 87 case do nhóm TEMP bản ghi (`tc-recordings-*`) và knock-on của nó;
FE-10 65 case bản ghi/phiên âm/voice-id chạm TEMP;
FE-11 6 case file đính kèm biểu quyết chạm TEMP;
FE-12 15 case gồm knock-on export/ban hành QuestPDF-temp và 3 test MinutesText CRLF.

### Danh sách case còn Pending: không còn case nào

Sau apply: **0 Pending, 0 untouched** — mọi case `Automated:` đều đã có `round1`.
Không có case nào cần viết test mới hay đánh N/A.

## 7. Hạ tầng đã dùng (không chạm code, chỉ để test chạy được)

- `TEMP`/`TMP` của tiến trình test trỏ về **`C:\tc-temp`** (tạo mới, user `admin` ghi đọc bình thường).
  Kết quả: denial vẫn xảy ra tại `C:\tc-temp` → bằng chứng bổ sung rằng lỗi nằm ở lớp chặn tiến trình,
  không phải ở đường dẫn hay quyền thư mục.
- PostgreSQL: dùng cụm 18 của F1 trong `%TEMP%\tc-pg-f1`, cổng **55432** (role/database `trustcabinet`),
  trỏ test qua `TRUSTCABINET_TEST_CONNECTION_STRING` (cơ chế có sẵn của `PostgresTestDatabase`,
  không sửa code). Không bật service `postgres` của compose vì cổng 5432 đã có service khác chiếm;
  không dừng container của người khác.
- Redis: container `trustcabinet-redis` sẵn sàng ở **6379**, biến `TRUSTCABINET_TEST_REDIS=localhost:6379`.
- SeaweedFS: container `trustcabinet-files` ở **127.0.0.1:8333**.
- Hai cấu hình chạy chỉ để vượt qua lỗi môi trường, không đổi ngữ nghĩa test:
  `Logging__EventLog__LogLevel__Default=None` và `-p:_MSTestEnableParentProcessQuery=false`.

## 8. Hạn chế và việc nên làm tiếp (không thuộc phạm vi FULL2)

1. `dotnet test TrustCabinet.sln` với một file `--logger trx` duy nhất vẫn **ghi đè** kết quả project
   chạy sau lên project chạy trước (đã thấy cảnh báo `Overwriting results file`). Số liệu
   Application.Tests (365/365) lấy từ `TestResults/round1-app-full2.trx` chạy riêng cùng bản mã.
   Muốn một file duy nhất phải đặt tên logger khác nhau cho từng project.
2. 203 case Failed trong JSON đều mang hậu tố `| R1 FAIL:` kèm nguyên nhân môi trường. Khi chạy lại
   trên máy sạch (testhost Medium, TEMP ghi được, Redis sẵn), toàn bộ nhóm này được kỳ vọng pass
   mà không cần sửa sản phẩm — trừ 3 test MinutesText cần máy Linux hoặc chuẩn hoá CRLF.
3. Cơ chế testhost chạy Low-IL và hook/filter chặn ghi TEMP trên máy này cần quyền admin mới kết luận
   dứt điểm. Thí nghiệm `C:\tc-temp` của FULL2 đã loại trừ nguyên nhân đường dẫn/quyền thư mục.
4. Không còn case Pending nào; hai workbook đã build xong từ JSON hiện tại.
