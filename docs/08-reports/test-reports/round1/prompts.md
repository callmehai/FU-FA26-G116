# Prompt giao agent — Round 1 (chạy song song)

Dán từng prompt vào một agent. Các prompt cùng nhóm "Chạy song song ngay" không đụng nhau: mỗi agent giữ **một bộ Feature riêng** (file `cases/FE-xx/` và file test riêng). Thứ tự và trạng thái xem [checklist.md](checklist.md).

## Quy tắc chung (mọi prompt đều dẫn tới đây)

Mỗi agent đọc mục này trước.

1. **Đọc trước:** `docs/08-reports/test-reports/AGENTS.md`, `plan-api.md` (mục 2 đến 5) và `checklist.md`.
2. **Chỉ động vào Feature của mình.** Chỉ sửa `cases/<FE của mình>/api.json` (và `ui.json` nếu prompt nói). Không sửa `cases/` của Feature khác, không chạy `integration_cases.py build`, không sửa Excel. Không sửa `round1-*.md` ngoài file được giao.
3. **Test mới đặt trong file mới**, tên `<Chủ đề>Round1Tests.cs` hoặc tương tự, trong thư mục Feature ở `tests/TrustCabinet.Web.Tests/`. **Không sửa test có sẵn** và không sửa fixture dùng chung (`TrustCabinetApiFactory.cs`, `*Fixture.cs`) trừ khi chỉ *thêm* một helper nhỏ và ghi lại trong báo cáo.
4. **Build riêng để không giẫm lên agent khác.** Luôn chạy dotnet với thư mục artifacts riêng:
   `dotnet test tests/TrustCabinet.Web.Tests --artifacts-path "$TEMP/tc-artifacts-<FE>" --filter "<lọc test của bạn>" --logger "trx;LogFileName=round1-<FE>.trx" --results-directory TestResults`
   Không chạy `dotnet test TrustCabinet.sln` toàn bộ. Test dùng PostgreSQL thật (cần `docker compose up -d` và `POSTGRES_PASSWORD` trong `.env` hoặc biến môi trường).
5. **Lỗi biên dịch do file của agent khác** (file test không phải bạn viết): đừng sửa. Chờ 3 phút, thử lại, tối đa 5 lần; vẫn lỗi thì ghi vào báo cáo và làm tiếp phần không cần biên dịch.
6. **Không sửa code production để test xanh.** Test fail vì code sai: giữ test, đánh case `Failed`, ghi vào báo cáo kèm bằng chứng (không tự mở Jira). Test fail vì case sai: sửa case.
7. **Không đánh `Passed` khi `Expected` còn dòng không có `Assert` tương ứng.** Thu hẹp Expected cho khớp thực tế hoặc tách thành hai case; ghi ID vào báo cáo.
8. **Ghi kết quả bằng script**, không gõ tay: `python scripts/test_reports/apply_trx.py TestResults/round1-<FE>.trx --features <FE của bạn>` (thử `--dry-run` trước). Sau đó `python scripts/test_reports/integration_cases.py check` phải 0 lỗi.
9. **Không commit, không push, không động tới git.** Giữ nguyên xuống dòng CRLF của JSON (script đã lo; nếu sửa tay thì dùng cách giữ CRLF).
10. **Báo cáo cuối** (ghi vào file `docs/08-reports/test-reports/round1/reports/<FE hoặc tên nhóm>.md`, tạo thư mục kèm `README.md` ngắn nếu chưa có), gồm:
    - số case: Passed / Failed / N/A / để trống, mỗi Feature;
    - **bảng case đã đổi nội dung** (ID, trước, sau, lý do) và số case chỉ đổi `note`;
    - case Passed mà Expected có dòng chưa được Assert phủ (nếu có);
    - test dùng chung cho nhiều case;
    - file test đã thêm, file có sẵn đã đụng (nếu có);
    - bug nghi là thật kèm bằng chứng;
    - việc chưa làm và lý do.

---

## Chạy song song ngay (5 agent API)

### A1 — FE-06 Participants (195 case, 50 chưa có test)

> Đọc mục "Quy tắc chung" trong `docs/08-reports/test-reports/round1/prompts.md` và làm theo. Gói của bạn: **FE-06**. (0) Soát `cases/FE-06/api.json` theo `plan-api.md` mục 2 (hành vi 19–20/11, 403 so với 404, case trùng). (1) Viết test xUnit cho 50 case `Manual only` (danh sách ở mục 7 của `plan-api.md`, phần FE-06) trong file mới `tests/TrustCabinet.Web.Tests/Participants/ParticipantRound1Tests.cs` (đặt thêm file nếu cần), dùng `integration-test-writer` nếu có. Đổi `note` của case thành `Automated: <file> (<TênTest>)`. (2) Chạy toàn bộ test của Feature (`--filter` theo namespace hoặc tên lớp Participants/Attendance), dùng `apply_trx.py --features FE-06` để điền `round1`. Báo cáo theo mục 10 vào `reports/FE-06.md`.

### A2 — FE-13 Notifications, FE-15 Reporting, FE-11 Voting (121 case, 39 chưa có test)

> Đọc mục "Quy tắc chung" trong `docs/08-reports/test-reports/round1/prompts.md` và làm theo. Gói của bạn: **FE-13 (21 chưa có test), FE-15 (3), FE-11 (15)**. Với từng Feature: soát case (mục 2 của `plan-api.md`; FE-11 có VOTE_API_IT27 liên quan giới hạn 10 MB / 100 MB, MSG288), viết test cho case `Manual only` trong file mới (`Notifications/NotificationRound1Tests.cs`, `Reports/ReportRound1Tests.cs`, `Votes/VoteRound1Tests.cs`), đổi `note` sang `Automated:`, chạy bằng `--filter`, ghi kết quả bằng `apply_trx.py --features FE-13 FE-15 FE-11`. Case webhook email của FE-13 (NOTI_API_IT39, IT45) dùng bản giả nếu hạ tầng có sẵn, không thì `N/A` kèm lý do. Báo cáo vào `reports/FE-13-15-11.md`.

### A3 — FE-08 Online Room, FE-09 Recording (262 case, 42 chưa có test)

> Đọc mục "Quy tắc chung" trong `docs/08-reports/test-reports/round1/prompts.md` và làm theo. Gói của bạn: **FE-08 (27 chưa có test), FE-09 (15)**. Hai Feature này chạm LiveKit và kho tệp: dùng bản giả (fake) đã có trong test; nếu không có hạ tầng giả thì đánh `N/A` kèm lý do, không bỏ trống. Soát case (mục 2), viết test cho case `Manual only` trong file mới (`Calls/RoomRound1Tests.cs`, `Recordings/RecordingRound1Tests.cs`), đổi `note`, chạy bằng `--filter`, ghi kết quả bằng `apply_trx.py --features FE-08 FE-09`. Báo cáo vào `reports/FE-08-09.md`.

### A4 — FE-01, FE-02, FE-03, FE-05 (264 case, 21 chưa có test)

> Đọc mục "Quy tắc chung" trong `docs/08-reports/test-reports/round1/prompts.md` và làm theo. Gói của bạn: **FE-01 (10 chưa có test), FE-02 (5), FE-03 (0, đã có test hết), FE-05 (6)**. FE-03 chưa có `round1`: soát case rồi chạy test có sẵn và ghi kết quả. Viết test cho case `Manual only` còn lại trong file mới (`Authentication/AuthRound1Tests.cs`, `Users/UserRound1Tests.cs`, `Agenda/AgendaRound1Tests.cs`), đổi `note`, chạy bằng `--filter`, ghi kết quả bằng `apply_trx.py --features FE-01 FE-02 FE-03 FE-05`. FE-01 và FE-02 có case "chờ thời gian": dùng đồng hồ giả của fixture. Báo cáo vào `reports/FE-01-02-03-05.md`.

### A5 — FE-07 Documents và FE-16 Calendar: ghi kết quả (agent làm hai Feature này)

> Đọc mục "Quy tắc chung" trong `docs/08-reports/test-reports/round1/prompts.md`. Bạn (hoặc agent trước) đã viết test cho FE-07 (`DocumentRound1GapTests.cs`) và FE-16 (`CalendarShareRound1Tests.cs`, `MeetingSlotRound1Tests.cs`), nhưng `cases/FE-07/api.json` và `cases/FE-16/api.json` vẫn là 183/217 và 61/98 `Automated:`, `round1` đều `Pending`. Việc cần làm: (1) với từng case `Manual only` đã có test mới, đổi `note` sang `Automated:`; (2) chạy test của hai Feature bằng `--filter` với `--artifacts-path` riêng và `.trx`; (3) `apply_trx.py --features FE-07 FE-16`; (4) `check` 0 lỗi. Nếu còn case chưa có test, viết nốt hoặc `N/A` kèm lý do. Báo cáo vào `reports/FE-07-16.md` theo mục 10, kèm câu trả lời cho: có case nào đổi nội dung Expected không, và có test nào dùng chung cho nhiều case không.

---

## Chạy song song ngay (2 agent UI, chỉ đọc và viết văn bản, không cần app chạy)

Nhóm này không chạy ứng dụng nên không tranh DB hay cổng. Mỗi agent ghi vào thư mục riêng `docs/08-reports/test-reports/round1/ui-map/` (tạo cùng `README.md` ngắn: "Bảng ánh xạ AC sang case UI, bản nháp, mỗi Feature một file").

### U1 — Cập nhật plan Playwright và bảng ánh xạ: FE-01, 02, 03, 05, 06, 07

> Đọc `docs/08-reports/test-reports/round1/plan-ui.md` (mục "Cách phân test UI" và bảng mẫu FE-04), `tests/qa/README.md` và `docs/08-reports/test-reports/AGENTS.md`. Gói của bạn: **FE-01, FE-02, FE-03, FE-05, FE-06, FE-07**. Với từng Feature: (1) đọc `tests/qa/plans/fe-NN.mjs` và `cases/FE-NN/ui.json`; (2) sửa plan nếu nó còn kiểm hành vi cũ (hành vi 19–20/11 ở `plan-ui.md`; FE-07 có giới hạn tải tệp 10 MB / 100 MB, MSG164, MSG288). Không chạy plan, chỉ sửa mã; sửa nhỏ, đúng AC; (3) làm bảng ánh xạ từng case UI sang AC với mức D (AC phủ đủ), P (phủ một phần), N (chưa có AC), dựa vào **thân hàm `run()` của AC**, không chỉ tiêu đề, và ghi file `ui-map/FE-NN.md`. Không sửa `ui.json`, không chạy `build`, không commit. Báo cáo: số D/P/N mỗi Feature, AC đã sửa và lý do, AC nghi sai.

### U2 — Giống U1, cho FE-08, 09, 11, 13, 15, 16

> Làm đúng như prompt U1 nhưng cho **FE-08, FE-09, FE-11, FE-13, FE-15, FE-16**. Lưu ý FE-16: lịch chia sẻ nay hiện 7 ngày thứ Hai đến Chủ nhật, 00:00–24:00; ô tìm giờ chọn được cả Bảy và Chủ nhật. FE-08, FE-09 có nhiều case cần hai phiên hoặc micro thật (nhóm AX và H trong `plan-ui.md`): đánh dấu rõ.

---

## Chạy sau (không chạy cùng lúc với nhóm trên)

### R1 — Chạy UI thật bằng Playwright (một agent, tuần tự)

Chạy sau khi U1 và U2 xong và API đã chạy xong. Chỉ **một** agent, vì dùng chung backend :5225, frontend :5173 và một DB.

> Đọc `plan-ui.md`, `ui-map/*.md`, `tests/qa/README.md` và `.claude/skills/verify-ui-in-browser`. Bật PostgreSQL, backend (`dotnet run --project backend/TrustCabinet.Web --artifacts-path "$TEMP/tc-artifacts-run"`) và frontend (`cd frontend && npm run dev`). Theo thứ tự FE-04, FE-12, FE-10, FE-07, FE-16, rồi các Feature còn lại: chạy `cd tests/qa; node run.mjs plans/fe-NN.mjs`, **xem từng ảnh** rồi kết luận. Với case mức D: điền `round1`, `date1` (ngày hôm nay), `tester1` (`Phạm Gia Khánh`) và đường dẫn ảnh trong `note`. Case P: mở rộng AC rồi chạy lại. Case N: viết AC mới. Case nhóm H (51 case, danh sách trong `plan-ui.md`): để trống, không tự đánh. Không commit. Báo cáo theo mục 10 của "Quy tắc chung" (phần UI: số Passed, Failed, N/A, trống mỗi Feature; AC đã sửa; bug nghi là thật).

### Ghi chú về chạy song song

- 5 agent API và 2 agent UI chạy cùng lúc là được: khác Feature, khác file, khác thư mục build (`--artifacts-path`). Test API tạo cơ sở dữ liệu riêng cho mỗi lần chạy trên cùng một PostgreSQL.
- Không chạy R1 cùng lúc với nhóm API, vì R1 dùng chung backend và Postgres với `dotnet run`.
- Mỗi agent ghi báo cáo vào thư mục `reports/`. Cần thêm `README.md` ngắn cho thư mục này.

---

## Đợt 2 (sau khi nhận báo cáo FE-04, FE-07, FE-10, FE-12, FE-16)

Đã có `scripts/test_reports/apply_trx.py`; mọi agent dùng script này, không tự viết script map khác.

### B1 — FE-04: trả lời nốt các câu hỏi còn thiếu

> Bạn đã trả lời mục 9 (kết quả chạy) nhưng thiếu 1–8 và 10. Trả lời nốt, kèm ID cụ thể: (1) thống kê `api.json` FE-04: bao nhiêu case chỉ đổi `note`, bao nhiêu sửa Expected/Procedure/Pre-conditions, xoá/gộp, thêm mới; (2) từng case đã sửa nội dung: trước, sau, lý do; (3) case `Passed` mà Expected còn dòng test không `Assert`; (4) test chỉ kiểm mã HTTP mà case yêu cầu thêm `MSGnn`, nội dung hoặc DB; (5) test dùng chung cho nhiều case, và mỗi case có phần Expected riêng được kiểm không; (6) có sửa code production hoặc test có sẵn không (`MeetingEndpointsTests.cs` đang bị sửa) — sửa gì, vì sao; test nào fail ban đầu; (7) bug nghi là thật, nhất là chỗ bạn đổi case cho khớp code (Thư ký được bắt đầu họp, chỉ Chủ trì kết thúc; 403 so với 404); (8) hành vi 19–20/11 (tìm không dấu, Thư ký bắt đầu họp, danh sách không lọc) đã có test phủ ở ID nào; (10) việc chưa làm. Ghi vào `reports/FE-04.md`. Dùng `apply_trx.py` thay `map_trx.py` nếu còn phải ghi lại kết quả. Cuối cùng cho biết `map_trx.py` nằm ở đâu và container `fe04-pg` còn chạy không. Không commit.

### B2 — FE-07: ghi kết quả vào JSON

> `scripts/test_reports/apply_trx.py` **đã có** (đọc docstring đầu file). Việc cần làm: (1) đổi `note` của 34 case `Manual only` của FE-07 đã có test mới sang `Automated: <file> (<TênTest>)`; (2) `python scripts/test_reports/apply_trx.py TestResults/round1-fe07.trx --features FE-07 --dry-run`, rồi chạy thật; (3) kiểm kết quả: 217 case phải có `Automated:` hoặc `N/A` kèm lý do, và `round1` khác `Pending`; (4) `python scripts/test_reports/integration_cases.py check` 0 lỗi. Nếu `.trx` cũ hơn test mới nhất thì chạy lại `dotnet test` với `--artifacts-path` riêng. Báo cáo vào `reports/FE-07.md` theo mục 10, gồm danh sách case đã đổi nội dung Expected/Procedure và case `Passed` mà Expected còn dòng chưa có `Assert`. Không sửa script, không viết script map khác, không commit.

### B3 — FE-16: chạy lại và ghi kết quả

> Lần trước testhost không khởi động vì lỗi biên dịch từ file của agent khác (FE-10). Lỗi đó nay đã được sửa. Làm lại: (1) build bằng `--artifacts-path` riêng; nếu vẫn lỗi biên dịch ở file không phải của bạn, chờ 3 phút rồi thử lại (tối đa 5 lần), vẫn lỗi thì báo kèm dòng lỗi; (2) `dotnet test tests/TrustCabinet.Web.Tests --filter "FullyQualifiedName~CalendarSharing" --logger "trx;LogFileName=round1-fe16.trx" --results-directory TestResults`; (3) đổi 37 `note` `Manual only` sang `Automated:` cho case đã có test pass; (4) `apply_trx.py ... --features FE-16` (thử `--dry-run`); (5) `check` 0 lỗi. Báo cáo vào `reports/FE-16.md` theo mục 10. Lưu ý hành vi 20/11: lịch 7 ngày 00:00–24:00, ô tìm giờ chấp nhận ngày trong tuần 1–7.

### F1 — Chạy chính thức một lần và dọn (chạy sau khi A1–A5, B1–B3 báo xong)

Chỉ một agent. Mục đích: kết quả Round 1 đến từ **một lần chạy** trên cùng một bản mã, không trộn nhiều `.trx` của nhiều thời điểm.

> Đọc `prompts.md` (Quy tắc chung) và `checklist.md`. (1) Dừng các container PostgreSQL phụ do agent tự tạo (tên dạng `fe*-pg`; chỉ những container đó, kiểm bằng `docker ps` trước), giữ PostgreSQL của `docker compose`. (2) `dotnet build TrustCabinet.sln -warnaserror`; nếu lỗi thì báo và dừng. (3) `dotnet test TrustCabinet.sln --logger "trx;LogFileName=round1-full.trx" --results-directory TestResults`. (4) `python scripts/test_reports/apply_trx.py TestResults/round1-full.trx --dry-run`, đọc kết quả, rồi chạy thật (không giới hạn `--features`). (5) `python scripts/test_reports/integration_cases.py check` phải 0 lỗi; sau đó `python scripts/test_reports/integration_cases.py build`. (6) Báo cáo `reports/FULL.md`: tổng test (pass/fail/skip), từng test fail kèm nguyên nhân (đặc biệt `MinutesTextTests`, 3 test fail sẵn do `` khi chạy trên Windows: phân biệt lỗi môi trường với lỗi sản phẩm), số case mỗi Feature theo `Passed` / `Failed` / `N/A` / `Pending`, case nào đổi kết quả so với lần ghi trước của từng agent. Không sửa code, không sửa case ngoài việc script ghi kết quả, không commit.
