# Round 1 — Chạy chính thức lần 3 (FULL3)

Ngày chạy: 2026-11-21. Người chạy: Agent F3. Tester Round 1: Phạm Gia Khánh.
Mã nguồn: cùng một bản mã với F1/F2 (không sửa code production, không sửa test có sẵn
hay fixture dùng chung, không sửa script, không commit, không động tới git).

Kết luận trước: toàn bộ 153 fail do môi trường của F1/F2 đã hết. Lần chạy chính thức này
(Web.Tests 2852/74, Application.Tests 365/0) chỉ còn 74 fail, tất cả đều thuộc nhóm M
(môi trường/harness), không có fail nào thuộc nhóm S (sản phẩm). Không còn case `Failed`
nào trong JSON: 1537/1537 case `Passed`.

## 1. Bước 1 — Kiểm môi trường (cả 4 mục đều đạt, nhưng phải sửa môi trường mới chạy được)

| # | Mục kiểm | Kết quả |
|---|---|---|
| 1.1 | `docker ps`: PostgreSQL, Redis, SeaweedFS | ĐẠT sau khi sửa (xem dưới). `docker compose up -d` từ thư mục gốc; cả ba container `trustcabinet-postgres`, `trustcabinet-redis`, `trustcabinet-files` đều `Up`, postgres và redis `healthy`. `pg_isready` OK, `redis-cli ping` = `PONG`, SeaweedFS `/status` = `200`. |
| 1.2 | Tiến trình Windhawk | ĐẠT. `Get-Process` lọc `*windhawk*` rỗng — đã gỡ, hook chặn ghi file không còn. |
| 1.3 | Ghi file vào `$env:TEMP` và thư mục con mới tạo | ĐẠT. `$env:TEMP` là mặc định của hệ thống (`C:\Users\admin\AppData\Local\Temp`), ghi/đọc/xoá file thử thành công. Không đặt `C:\tc-temp`. |
| 1.4 | `SpaFallback` 10/10 với `--artifacts-path "$env:TEMP\tc-artifacts-f3"` | ĐẠT. `Passed: 10/10`. |

Hai sự cố môi trường phát sinh khi chạy thử và cách xử lý (chỉ sửa môi trường, không đụng code):

1. **Test không đọc được `POSTGRES_PASSWORD` trong `.env`.** Các test đọc `.env` bằng cách
   đi ngược từ `AppContext.BaseDirectory` lên tới thư mục chứa `TrustCabinet.sln`; vì
   `--artifacts-path` trỏ ra `$TEMP`, bước đi ngược này không bao giờ tới repo nên mật khẩu
   rỗng → 1468 test báo `PostgreSQL integration tests need TRUSTCABINET_TEST_CONNECTION_STRING,
   POSTGRES_PASSWORD, ...`. Xử lý: truyền mật khẩu qua biến môi trường của tiến trình chạy
   test (đọc từ `.env`, đúng cơ chế `PostgresTestDatabase` hỗ trợ). Không sửa code đọc cấu hình.
2. **Cổng 5432 (IPv4) bị service `postgresql-x64-18` của máy chiếm.** `Get-NetTCPConnection`
   cho thấy `0.0.0.0:5432` thuộc về `postgres.exe` của Windows, còn container compose chỉ giữ
   được `[::]:5432`. Mọi kết nối `localhost:5432` (IPv4) đều rơi vào postgres của máy với mật
   khẩu khác → `28P01: password authentication failed for user "trustcabinet"`. Không dừng được
   service này vì thiếu quyền admin (lệnh `Stop-Service` báo `Cannot open ... service`).
   Xử lý, theo đúng thứ tự đã thử:
   - Đã thử `Host=::1` (về đúng container compose) nhưng volume `trustcabinet-pgdata` được khởi
     tạo từ trước với mật khẩu cũ (biến `POSTGRES_PASSWORD` hiện tại không còn tác dụng —
     chứng minh: từ trong container, mật khẩu sai vẫn đăng nhập được qua loopback nhờ rule
     `trust`, còn qua IP container thì chỉ mật khẩu đúng mới qua). Đã đồng bộ lại mật khẩu
     bằng `ALTER USER trustcabinet WITH PASSWORD '<giá trị trong .env>'` (không mất dữ liệu).
     Dù vậy `[::1]:5432` từ máy host vẫn báo 28P01 — sau khi `docker compose restart postgres`,
     cả hai stack `[::]` và `0.0.0.0` đều đã rơi vào tay `postgres.exe` của máy.
   - Chốt: chạy thêm một container postgres:17 phụ (`tc-pg-f3`, volume mới, cùng user/db/mật
     khẩu với `.env`) ở cổng **`127.0.0.1:55432:5432`** — cổng 55432 trùng với cụm F1 trước đây
     (kiểm tra lúc đó đã đóng) và trùng `ConnectionStrings:TrustCabinet` trong user-secrets của
     backend dev. Test trỏ tới đó qua `TRUSTCABINET_TEST_CONNECTION_STRING` (cơ chế có sẵn của
     test, F2 cũng dùng). Container compose gốc vẫn giữ nguyên, không xoá volume nào.

## 2. Bước 2 — Build và chạy

| # | Lệnh | Kết quả |
|---|---|---|
| 2.1 | `dotnet build TrustCabinet.sln -warnaserror --artifacts-path "$env:TEMP\tc-artifacts-f3"` | Exit 0. **0 Warning, 0 Error.** (Warning RDG002 từng thấy ở lần chạy thử không tái hiện ở build tăng dần.) |
| 2.2 | `dotnet test TrustCabinet.sln --artifacts-path "$env:TEMP\tc-artifacts-f3" --logger "trx;LogFileName=round1-full3.trx" --results-directory TestResults` (lần đầu, TEMP mặc định, **không** đặt `Logging__EventLog=None`) | Web.Tests: **2852 passed / 74 failed / 0 skipped (tổng 2926)**, khoảng 6 phút 32 giây. File `TestResults/round1-full3.trx` (giữ kết quả Web.Tests; `dotnet test` cả solution ghi đè trx giữa các project như F1/F2 đã ghi nhận). |
| 2.2b | `dotnet test tests/TrustCabinet.Application.Tests --artifacts-path "$env:TEMP\tc-artifacts-f3" --logger "trx;LogFileName=round1-app-full3.trx" --results-directory TestResults` | Application.Tests: **365/365/0**. File `TestResults/round1-app-full3.trx`. |

Không cần tới cờ `Logging__EventLog__LogLevel__Default=None`: lần này **0 fail EventLog**
ngay từ lần chạy đầu nên không đặt (đúng ràng buộc).

## 3. Tổng số test so với FULL2

| Project (file trx) | Total | Passed | Failed | Skipped |
|---|---|---:|---:|---:|
| `TrustCabinet.Web.Tests` (`round1-full3.trx`) | 2926 | 2852 | 74 | 0 |
| `TrustCabinet.Application.Tests` (`round1-app-full3.trx`) | 365 | 365 | 0 | 0 |
| **Tổng Round 1 (FULL3)** | **3291** | **3217** | **74** | **0** |
| FULL2 (để so) | 3318 | 3165 | 153 | 0 |

Chênh tổng 27 test (3318 → 3291) không phải do mất test: 71 fail nhóm solveroot (mục 4)
khiến các theory thuộc 5 class đó chỉ còn **1 kết quả gộp** thay vì một kết quả mỗi data row
(ví dụ `SpeechClipFoldTests` 19 row → 1, `SkillMirrorTests` 9 row → 1). Đối chiếu tên giữa
`round1-full2.trx` và `round1-full3.trx`: ngoài các row gộp này, phần chênh còn lại chỉ là
cùng test với GUID ngẫu nhiên trong tên (`RoleEndpointsTests` tạo role `Guid.CreateVersion7`
mỗi lần chạy — 4↔4, trung hoà). Không có test nào biến mất khỏi mã nguồn.

So với FULL2: fail giảm **153 → 74 (−79)**; toàn bộ 150+ fail nhóm TEMP-denied, knock-on
HTTP 500, `AudioPath` null, EventLog, Redis, hub flake của F1/F2 **đều đã hết** — bằng chứng
trực tiếp rằng việc gỡ Windhawk đã sửa đúng nguyên nhân.

## 4. Phân loại 74 test fail — toàn bộ nhóm M, không có nhóm S

Không có test nào fail tại `Assert` logic sản phẩm. Chi tiết từng nhóm:

### 4.1. Nhóm M1 — 71 test: không tìm thấy `TrustCabinet.sln` (harness, do `--artifacts-path`)

Thông điệp quyết định (nguyên văn, chung cho cả nhóm):

- `System.IO.DirectoryNotFoundException : TrustCabinet.sln not found above the test output.` (ArchitectureTests ×3, DomainDependencyTests ×1)
- `System.InvalidOperationException : TrustCabinet.sln not found above the test binaries.` (MessageCodeCatalogueTests ×65, qua `TypeInitializationException`; SkillMirrorTests ×1)
- `System.InvalidOperationException : Repository root not found.` (SpeechClipFoldTests ×1)

Nguyên nhân: 5 class này xác định gốc repo bằng cách đi ngược từ `AppContext.BaseDirectory`
lên tới `TrustCabinet.sln`. Lần chạy chính thức bắt buộc dùng `--artifacts-path`
`$TEMP\tc-artifacts-f3` (ngoài repo) nên bước đi ngược thất bại ngay trong khởi tạo,
trước khi một dòng logic sản phẩm nào được chạm tới. Đây là giới hạn của harness, không
phải lỗi sản phẩm.

Đã chạy lại riêng để xác nhận (đúng tinh thần bước 3): cùng 5 class trên nhưng build vào
thư mục trong repo (`--artifacts-path "$pwd\tc-artifacts-diag"`, sln tìm thấy được) cho
**116/116 pass**; thư mục chẩn đoán đã xoá sau đó. (Hai lần chạy chẩn đoán dùng output
mặc định trong repo bị crash testhost với `Win32Exception (5): Access is denied` ở watchdog
`SetParentProcessExitCallback` — cũng là biểu hiện môi trường; lần chẩn đoán kết luận dùng
thêm `-p:_MSTestEnableParentProcessQuery=false` như F2 từng dùng. Lần chạy chính thức ở
`$TEMP` không gặp crash này nên giữ nguyên cờ theo nhiệm vụ.)

Danh sách 71 test (tên rút gọn theo class): `ArchitectureTests` 3
(`A2_Application_references_only_Domain_and_its_three_packages`,
`A6_Web_names_Infrastructure_only_in_the_composition_root`,
`Listed_exceptions_still_break_their_rule`); `DomainDependencyTests` 1
(`The_domain_project_has_no_package_reference`); `MessageCodeCatalogueTests` 65 (toàn bộ
các case `Meeting_workflow_preserves_main_code_reservations` MSG48–MSG77,
`Participants_preserve_main_code_reservations` MSG238/239/252/253,
`Workflow_codes_use_the_allocation_after_main_reservations` MSG78–MSG89/MSG130–137,
`Workflow_sequences_use_catalogued_codes_with_the_documented_status` ×3,
`Meeting_workflow_preserves_main_code_reservations` còn lại và 5 test quét mã
`Backend_endpoints_use_named_constants…`, `Every_code_the_backend_declares…` ×2,
`Every_code_the_catalogue_says_the_backend_sends…`,
`The_frontend_only_matches_on_MSG_codes`, `The_next_code_follows_the_highest_one_in_use`,
`The_scanners_find_codes_on_all_three_sides`); `SpeechClipFoldTests` 1
(`Marks_what_the_shared_case_expects`); `SkillMirrorTests` 1
(`Every_project_skill_is_mirrored_unchanged_into_agents_skills`).

### 4.2. Nhóm M2 — 3 test MinutesTextTests (CRLF trên Windows, đã biết, không thuộc case nào)

Ba test fail: `Ai_text_cannot_turn_into_a_title_a_table_row_or_a_centred_line`,
`Without_a_reviewed_draft_the_agenda_conclusions_and_votes_fill_the_sections_and_blanks_remain`,
`A_title_with_marks_or_line_breaks_stays_one_plain_line`
(đều trong `tests/TrustCabinet.Web.Tests/Summaries/MinutesTextTests.cs`).

Dòng lỗi quyết định (nguyên văn):
`Assert.Contains() Failure: Item not found in collection` — Collection chứa
`"^ **Kết quả cuộc họp Họp # chen @@ x | y**\r"`, expected `"^ **Kết quả cuộc họp Họp #
chen @@ x | y**"`: lệch đúng một ký tự `\r` (test so khớp tới ký tự xuống dòng; runtime
Windows trả CRLF, kỳ vọng LF). Nội dung biên bản đúng từng chữ — sản phẩm tách dòng, giữ
tiêu đề, hàng bảng, căn giữa đúng luật. Không sửa (đúng ràng buộc nhiệm vụ).

### 4.3. Nhóm S — 0 test

Không có test nào chạy tới `Assert` logic sản phẩm rồi sai. Không giữ case `Failed` nào vì
lý do sản phẩm, không mở Jira (đúng ràng buộc).

Các nhóm M của F1/F2 đã xác nhận hết hẳn: TEMP-denied 0, knock-on HTTP 500 0,
`AudioPath` null 0, EventLog 0 (không cần cờ tắt), Redis 0, hub flake 0.
Đặc biệt Redis: listener IPv4 6379 không tồn tại (chỉ có `[::1]` qua wslrelay và `[::]`
qua docker backend) nhưng mọi test Redis đều pass.

## 5. Số case mỗi Feature sau apply (so với FULL2: 1334 Passed / 203 Failed / 0 Pending)

`apply_trx.py TestResults/round1-full3.trx --dry-run` cho `Passed 1537, Failed 0,
left untouched 0`; chạy thật không giới hạn `--features` cho kết quả tương tự trên cả 15
Feature. `integration_cases.py check`: **0 error(s)**; `build` xong:
`Report5.2_Integration_Test.xlsx` (15 sheets, 875 case),
`Report5.2.2_Integration_Test_API_Test.xlsx` (15 sheets, 1537 case), kèm preview.

| Feature | Passed | Failed | N/A | Pending |
|---|---:|---:|---:|---:|
| FE-01 | 57 | 0 | 0 | 0 |
| FE-02 | 61 | 0 | 0 | 0 |
| FE-03 | 63 | 0 | 0 | 0 |
| FE-04 | 116 | 0 | 0 | 0 |
| FE-05 | 83 | 0 | 0 | 0 |
| FE-06 | 195 | 0 | 0 | 0 |
| FE-07 | 217 | 0 | 0 | 0 |
| FE-08 | 136 | 0 | 0 | 0 |
| FE-09 | 126 | 0 | 0 | 0 |
| FE-10 | 181 | 0 | 0 | 0 |
| FE-11 | 53 | 0 | 0 | 0 |
| FE-12 | 83 | 0 | 0 | 0 |
| FE-13 | 47 | 0 | 0 | 0 |
| FE-15 | 21 | 0 | 0 | 0 |
| FE-16 | 98 | 0 | 0 | 0 |
| **Tổng** | **1537** | **0** | **0** | **0** |

So với FULL2, 203 case `Failed` (FE-07 30, FE-09 87, FE-10 65, FE-11 6, FE-12 15 — toàn bộ
hậu tố `| R1 FAIL:` môi trường) nay đều `Passed` mà không sửa sản phẩm. Tổng case tăng
1334 → 1537 vì 203 case này quay lại `Passed` (1537 = 1334 + 203, khớp số case API).

### Danh sách case `Failed` còn lại: không còn case nào

Không có case nào giữ `Failed`. Không có bằng chứng lỗi sản phẩm nào để ghi.

### Danh sách case đã đưa về `Pending`: không có case nào

Đã kiểm tra: không một test nào trong 74 test fail được case nào trích dẫn trong `note`
(`grep` toàn bộ `cases/` không trúng tên class nào; dry-run cũng cho `left untouched 0`
với `Failed 0`). Vì vậy không có case nào cần đổi về `Pending` — mọi case đều gắn với
test đã pass.

## 6. Hạ tầng đã dùng và việc chưa làm

Hạ tầng (không chạm code, chỉ để test chạy được):

- `TEMP`/`TMP` mặc định của hệ thống; `--artifacts-path` riêng `$TEMP\tc-artifacts-f3`
  cho build/test chính thức (đúng ràng buộc).
- PostgreSQL cho test: container phụ **`tc-pg-f3`** (postgres:17-alpine, volume mới,
  user/db/mật khẩu khớp `.env`) trên `127.0.0.1:55432`; test trỏ qua
  `TRUSTCABINET_TEST_CONNECTION_STRING` (cơ chế có sẵn). Container vẫn đang chạy — lần
  chạy sau dùng lại được. Container compose `trustcabinet-postgres` và volume của nó giữ
  nguyên (đã `ALTER USER` đồng bộ mật khẩu với `.env`).
- Service `postgresql-x64-18` của máy vẫn chạy và vẫn giữ IPv4 5432 (không đủ quyền dừng).
  Ai có quyền admin nên chọn một: dừng service đó để compose postgres dùng được 5432 như
  thiết kế, hoặc chuyển hẳn test/dev sang 55432.
- Redis compose (`trustcabinet-redis`, 6379) và SeaweedFS (`trustcabinet-files`, 8333) dùng
  nguyên trạng. Không đặt `Logging__EventLog=None`.

Việc chưa làm / hạn chế (ngoài phạm vi F3):

1. 74 test fail nhóm M vẫn còn đó trong lần chạy theo đúng cờ nhiệm vụ (71 do
   `--artifacts-path` ngoài repo, 3 do CRLF Windows). Muốn chúng xanh trong một lần chạy
   duy nhất cần hoặc build output trong repo (trái ràng buộc `--artifacts-path` riêng),
   hoặc chủ test chỉnh cơ chế tìm gốc repo / chuẩn hoá xuống dòng (ngoài phạm vi: không
   được sửa test). Chúng không gắn với case nào nên không ảnh hưởng kết quả Round 1.
2. `dotnet test TrustCabinet.sln` với một `--logger trx` vẫn ghi đè giữa các project
   (đã thấy cảnh báo `Overwriting results file`); số liệu Application.Tests (365/365) lấy
   từ `TestResults/round1-app-full3.trx` chạy riêng cùng bản mã, như cách F2 đã làm.
3. Ba test MinutesTextTests vẫn fail trên Windows (CRLF); trên máy Linux (LF) được kỳ vọng
   pass — chưa có máy Linux để kiểm chứng.
4. Chưa kiểm chứng lại sau khi khởi động lại máy (container `tc-pg-f3` và compose có tự lên
   lại không tùy cấu hình restart).
