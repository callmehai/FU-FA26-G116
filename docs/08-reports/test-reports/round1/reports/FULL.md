# Round 1 — Báo cáo chạy chính thức một lần (F1)

Ngày chạy: 2026-11-21. Người chạy: Agent F1. Tester Round 1: Phạm Gia Khánh.
Mã nguồn: cùng một bản mã cho toàn bộ lần chạy (không sửa code production, không sửa test có sẵn,
không commit, không động tới git).

## 1. Lệnh đã chạy và kết quả từng bước

| # | Lệnh | Kết quả |
|---|---|---|
| 1 | `docker ps` (và `docker ps -a`) | Daemon Docker lúc đầu chưa chạy (không có tiến trình/named pipe Docker; service `com.docker.service` không khởi động được vì tài khoản thiếu quyền admin) nên không có container nào để dừng. Cuối buổi user bật Docker mới: `docker ps` chạy được nhưng bảng rỗng — không có container `fe*-pg` nào, cũng không có Postgres của compose. Kết luận: không có gì để dừng, không có gì để giữ. |
| 2 | `dotnet build TrustCabinet.sln -warnaserror` | Exit 0. Một warning duy nhất RDG002 ở `PersonalLibraryEndpoints.cs:119` (bộ sinh endpoint không phân giải tĩnh được handler — build vẫn pass, CI coi là đạt vì exit 0). |
| 3a | `dotnet test TrustCabinet.sln --logger "trx;LogFileName=round1-full.trx" --results-directory TestResults` (lần 1, 16:07) | Web.Tests: 2642 passed / 311 failed / 0 skipped (tổng 2953). Application.Tests: 365/365 (ghi đè mất khi Web.Tests ghi cùng tên file — xem mục 7). Lần này thiếu Redis, thiếu SeaweedFS, EventLog còn bật. |
| 3b | Cùng lệnh test (lần 2 — lần chính thức, ~16:31–16:50) | Web.Tests: **2800 passed / 153 failed / 0 skipped (tổng 2953)**. Application.Tests riêng (`round1-app-final.trx`): **365/365**. Hạ tầng lần 2: Redis + SeaweedFS đã `docker compose up -d`, `Logging__EventLog__LogLevel__Default=None`. |
| 4 | `python scripts/test_reports/apply_trx.py TestResults/round1-full.trx --dry-run`, rồi chạy thật (không giới hạn `--features`) | Dry-run: Passed 1334, Failed 203, untouched 0. Chạy thật: 15/15 Feature đều được ghi, tổng **1334 Passed / 203 Failed / 0 untouched / 0 Pending**. |
| 5 | FE-08/FE-09 | Không phải sửa note nào (cả 139 tên test được dẫn đều tồn tại trong source, không trùng tên giữa các class). Không viết test mới, không đánh N/A (mọi case đều đã có test và test đã chạy). Xem chi tiết mục 5. |
| 6 | `python scripts/test_reports/integration_cases.py check` | **0 error(s); 0 group(s) still without cases.** |
| 7 | `python scripts/test_reports/integration_cases.py build` | Xong: `Report5.2_Integration_Test.xlsx` (15 sheets, 875 case), `Report5.2.2_Integration_Test_API_Test.xlsx` (15 sheets, 1537 case), kèm preview. |

## 2. Tổng số test (lần chạy chính thức)

| Project (file trx) | Total | Passed | Failed | Skipped |
|---|---|---:|---:|---:|
| `TrustCabinet.Web.Tests` (`TestResults/round1-full.trx`, lần 2) | 2953 | 2800 | 153 | 0 |
| `TrustCabinet.Application.Tests` (`TestResults/round1-app-final.trx`) | 365 | 365 | 0 | 0 |
| **Tổng Round 1** | **3318** | **3165** | **153** | **0** |

Tham khảo lần 1 (môi trường thiếu Redis/SeaweedFS, EventLog còn bật): Web.Tests 2642/311/0.
Chênh lệch 158 test giữa hai lần chạy đều là nhóm EventLog (141) + Redis (25) + Hub flake (2) trừ đi
10 test TEMP-family biến động — tất cả đều thuộc nhóm môi trường ở mục 3.

## 3. Nguyên nhân 153 test fail — toàn bộ là môi trường, không ghi nhận lỗi sản phẩm

Đo đạc trên máy chạy (bằng P/Invoke `GetTokenInformation(TokenIntegrityLevel)`):

- Tiến trình `testhost.exe` do `dotnet test`/`vstest` sinh ra chạy ở **mức toàn vẹn Low
  (`S-1-16-4096`)**, trong khi tiến trình cha `dotnet` và shell chạy ở Medium (`S-1-16-8192`).
- Ba hệ quả quan sát được, khớp hoàn toàn với ba nhóm fail lớn nhất (xem bằng chứng từng nhóm dưới đây).
- Ngoài ra máy có service **Windhawk (LocalSystem)** tiêm `windhawk.dll` cùng mod
  `explorer-details-better-file-sizes` vào mọi tiến trình `dotnet`/`testhost` (đã liệt kê module
  đang nạp — `pwsh.exe` thì không bị tiêm và ghi TEMP bình thường). Đây là nghi phạm thứ hai cho
  nhóm lỗi file; không tắt được vì thiếu quyền admin.

| Nhóm | Số test | Dấu hiệu | Kết luận |
|---|---:|---|---|
| EventLog (lần 1) | 141 | `Cannot open log for source '.NET Runtime'. You may not have write access.` Tiến trình Low không được ghi Event Log. | Môi trường. **Đã chứng minh sửa được:** đặt `Logging__EventLog__LogLevel__Default=None` rồi chạy lại mẫu `MeetingEndpointsTests.List_returns_MSG38` → 2/2 pass. Lần 2 bật biến này nên nhóm này hết hẳn (0). |
| Redis (lần 1) | 26 (25 `Redis*Tests` + 1 `AuthReuseRound1Tests`) | `These tests need Redis (TRUSTCABINET_TEST_REDIS, default localhost:6379)` / `RedisConnectionException`. Redis của compose chưa chạy trong lần 1. | Môi trường. **Đã chứng minh sửa được:** sau `docker compose up -d redis` (image `redis:8-alpine` có sẵn), chạy lại 78 test Redis/Minutes/Hub → toàn bộ 26 test Redis pass, 70/78 pass. |
| TEMP / file đĩa | 138 | `UnauthorizedAccessException: Access to the path 'C:\Users\admin\AppData\Local\Temp\tc-recordings-…\2026-11' is denied.` (thư mục bản ghi), `…\tmpXXXX.tmp is denied.` (tài liệu upload), `QuestPDF … Unable to find a suitable temporary storage location`, fixture `SpaFallbackTests` giải nén frontend, `ExportEndpointTests`. Tái hiện xác định ngay cả khi chạy lẻ một test. Cùng user `admin` (đã kiểm tra owner), cùng thư mục mà `pwsh` ghi bình thường. Đã thử thư mục Low-label + `Everyone:F` vẫn bị từ chối → không phải DACL/MIC mà là lớp chặn dưới (hook/filter), cần quyền admin mới xác minh tiếp (xem Windhawk ở trên). | Môi trường. Sản phẩm ghi file đúng logic (`LocalRecordingStorage`, QuestPDF, upload buffering); chỉ môi trường chạy mới từ chối. |
| Knock-on từ TEMP (HTTP 500 khi export/ban hành biên bản) | 6 | `An_unknown_export_format_is_refused` (html, xlsx), `A_row_of_line_text_reads_back_as_html_and_exports`, `Export_returns_a_pdf_of_the_draft_in_its_own_font`, `Minutes_past_draft_are_locked(Issued)`, `Secretary_submits_and_the_chair_issues…` — đều `Expected: OK, Actual: InternalServerError`. Đọc code test xác nhận mọi test này đều gọi `GET /api/minutes/{id}/export` (docx/pdf) hoặc ban hành (sinh file PDF qua QuestPDF). Dòng assert 400/403 đứng trước trong cùng test vẫn pass (ví dụ `MSG406` cho format lạ) — chỉ các bước chạm sinh file mới 500. | Môi trường (hệ quả của nhóm TEMP). |
| Knock-on từ TEMP (bản ghi/speech-clip) | 6 | `Deleting_a_sentence…` / `A_sentence_from_my_mic…` (`Assert.NotNull: Value is null`), `Everyone_who_reads_the_transcript…` (`requires String but target is Null`), `A_sentence_taken_out…` (`Assert.True→False`), `A_guests_sentences…` (`ArgumentNullException path2`), `A_guest_reads…` (`Expected 200, got 404`). Stack cho thấy null phát sinh ở phía test khi đọc `AudioPath` trong DB — vì bước upload trước đó 500 do TEMP-denial nên không có audio path để đọc. | Môi trường (hệ quả của nhóm TEMP). |
| Hub flake (lần 1) | 2 | `AgendaHubTests` + `AgendaGuestTests`: `Expected HubException, Actual TaskCanceledException`. Chạy lại ở lần verify (tải nhẹ hơn) thì pass. | Môi trường (timeout dưới tải chạy full). |
| MinutesText trên Windows | 3 | Xem riêng mục 4. | Môi trường (CRLF của Windows). |

Danh sách đầy đủ 153 test xem trong `TestResults/round1-full.trx` (lần 2). Không có test fail nào
cho thấy logic sản phẩm sai: mọi fail đều trỏ về EventLog/Redis/TEMP/CRLF hoặc là hệ quả trực tiếp
của chúng (đường dẫn code đã đọc xác nhận).

## 4. Ba test MinutesTextTests — lỗi môi trường (CRLF Windows), không phải lỗi sản phẩm

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
  trên Linux, hoặc chuẩn hoá `\r\n`→`\n` trong assert của test (việc của chủ test, ngoài phạm vi F1).

## 5. Số case mỗi Feature sau apply (so với lần ghi trước của từng agent)

Snapshot trước apply: 1317 Passed / 0 Failed / 220 Pending (khớp checklist).
Sau apply (lần 2): **1334 Passed / 203 Failed / 0 N/A / 0 Pending**.

| Feature | Passed | Failed | N/A | Pending | Đổi so với lần ghi trước |
|---|---:|---:|---:|---:|---|
| FE-01 | 57 | 0 | 0 | 0 | Không đổi |
| FE-02 | 61 | 0 | 0 | 0 | Không đổi |
| FE-03 | 63 | 0 | 0 | 0 | Không đổi |
| FE-04 | 116 | 0 | 0 | 0 | Không đổi |
| FE-05 | 83 | 0 | 0 | 0 | Không đổi |
| FE-06 | 195 | 0 | 0 | 0 | Không đổi |
| FE-07 | 187 | 30 | 0 | 0 | 30 case Passed→Failed (toàn bộ do TEMP-denial khi upload `tmp*.tmp`: `DOC_API_IT01, IT12–IT20, IT76, IT83, IT87, IT112, IT115–IT119, IT121, IT126, IT128, IT129, IT169–IT174, IT215`). |
| FE-08 | 136 | 0 | 0 | 0 | **109 Pending→Passed, trọn vẹn 136/136.** 27 case A3 đã ghi giữ nguyên Passed. |
| FE-09 | 39 | 87 | 0 | 0 | 111 Pending → 24 Passed + 87 Failed. 87 Failed toàn bộ là nhóm TEMP bản ghi (`tc-recordings-*`) và knock-on của nó (danh sách ID ở dưới). 15 case A3 đã ghi giữ nguyên Passed. |
| FE-10 | 116 | 65 | 0 | 0 | 65 case Passed→Failed (bản ghi/phiên âm/voice-id chạm TEMP: `TRANS_API_IT01–IT23, IT25, IT26, IT28–IT35, IT40, IT42–IT63, IT102, IT109, IT110, IT114–IT117, IT130, IT140`; trong đó IT25/IT40 là knock-on 404, còn lại là TEMP-denial trực tiếp). |
| FE-11 | 47 | 6 | 0 | 0 | 6 case Passed→Failed (file đính kèm biểu quyết chạm TEMP: `VOTE_API_IT23, IT25–IT29`). |
| FE-12 | 68 | 15 | 0 | 0 | 15 case Passed→Failed (6 test export/ban hành knock-on QuestPDF-temp + 3 test MinutesText CRLF, ánh xạ ra các case `MIN_API_IT10, IT14, IT16, IT18, IT22, IT25, IT36, IT38, IT43, IT44, IT46, IT50, IT56, IT59, IT60`). |
| FE-13 | 47 | 0 | 0 | 0 | Không đổi |
| FE-15 | 21 | 0 | 0 | 0 | Không đổi |
| FE-16 | 98 | 0 | 0 | 0 | Không đổi |

Ghi chú về cột "đổi": mọi case chuyển Passed→Failed đều do môi trường máy chạy F1
(Low-IL testhost + TEMP-denial, chi tiết mục 3), không phải do sản phẩm đổi hành vi —
các test đó đã pass trên máy của agent khi TEMP còn ghi được.

### Danh sách case còn Pending: không còn case nào

Sau apply: **0 Pending, 0 untouched** — mọi case `Automated:` đều đã có `round1`.
Không có case nào cần viết test mới hay đánh N/A.

### Vì sao 220 case FE-08/FE-09 Pending trước đây (không phải note sai)

- Đã đối chiếu toàn bộ **139 tên test** được dẫn trong `note` của FE-08/FE-09 với source:
  **tất cả đều tồn tại** (`public Task`/`public void` khớp từng ký tự), và không tên nào bị trùng
  giữa nhiều class (không sợ `apply_trx` gộp nhầm kết quả giữa các class).
- Không có test nào bị skip trong lần chạy (trx: Skipped 0).
- Nguyên nhân duy nhất: agent A3 chỉ chạy test mới bằng `--filter` hẹp rồi apply từ file trx
  thiếu các test cũ → script bỏ qua case ("missing from the run") nên vẫn Pending.
- Lần chạy full solution này chứa đủ mọi test nên apply lấp hết: FE-08 109/109 Passed;
  FE-09 24 Passed + 87 Failed (môi trường).
- Không sửa nội dung case nào, không sửa note nào, không thêm test, không đánh N/A —
  đúng ràng buộc (chỉ script ghi `round1`/`date1`/`tester1` và hậu tố `| R1 FAIL:`).

## 6. Lệnh hạ tầng phụ đã dùng (không chạm code, chỉ để test chạy được)

- PostgreSQL: Docker daemon ban đầu chưa chạy và tài khoản thiếu quyền admin nên không khởi động
  được; cổng 5432 đã bị service `postgresql-x64-18` của máy chiếm với credentials khác.
  Đã dựng cụm PostgreSQL 18 riêng trong `%TEMP%\tc-pg-f1`, nghe **cổng 55432**, role/database
  `trustcabinet` (mật khẩu từ `.env`), và trỏ test qua biến môi trường
  `TRUSTCABINET_TEST_CONNECTION_STRING` (cơ chế có sẵn của `PostgresTestDatabase`, không sửa code).
- Redis + SeaweedFS: `docker compose up -d redis files` (image đã cache sẵn; không bật service
  `postgres` của compose vì đụng cổng 5432).
- Hai cờ dòng lệnh chỉ để vượt qua lỗi môi trường, không đổi ngữ nghĩa test:
  `-p:_MSTestEnableParentProcessQuery=false` (tắt watchdog "theo dõi tiến trình cha" của VSTest —
  chính watchdog này crash 100% trên máy này vì testhost Low-IL không mở được handle tiến trình
  cha; cờ do chính `Microsoft.TestPlatform.TestHost.targets` cung cấp) và
  `Logging__EventLog__LogLevel__Default=None` (tắt EventLog logger mà tiến trình Low không ghi được).
- Các file `.cmd` trong `%TEMP%` và task `TCF1Round1`/`TCF1Diag` dùng lúc chẩn đoán đã có thể xoá;
  cụm Postgres 55432 và container redis/files có thể dừng khi không cần (`docker stop`,
  `pg_ctl stop`).

## 7. Hạn chế và việc nên làm tiếp (không thuộc phạm vi F1)

1. `dotnet test TrustCabinet.sln` với một file `--logger trx` duy nhất sẽ **ghi đè** kết quả project
   chạy sau lên project chạy trước (đã thấy cảnh báo `Overwriting results file`). Số liệu
   Application.Tests (365/365) lấy từ `TestResults/round1-app-final.trx` chạy riêng cùng bản mã.
   Muốn một file duy nhất phải đặt tên logger khác nhau cho từng project.
2. 203 case Failed trong JSON đều mang hậu tố `| R1 FAIL:` kèm nguyên nhân môi trường. Khi chạy lại
   trên máy sạch (testhost Medium, TEMP ghi được, Redis sẵn), toàn bộ nhóm này được kỳ vọng pass
   mà không cần sửa sản phẩm — trừ 3 test MinutesText cần máy Linux hoặc chuẩn hoá CRLF.
3. Cơ chế testhost chạy Low-IL và hook/filter chặn ghi TEMP trên máy này cần quyền admin mới
   kết luận dứt điểm (Process Explorer, `fltmc`, chính sách Windhawk/sandbox).
4. Không còn case Pending nào; hai workbook đã build xong từ JSON hiện tại.
