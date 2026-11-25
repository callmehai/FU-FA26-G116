# Checklist Round 1 — test tích hợp 5.2 (UI) và 5.2.2 (API)

Thứ tự làm từ trên xuống. Mỗi bước có người làm và điều kiện xong. Chi tiết nằm ở hai tài liệu giao việc: [plan-api.md](plan-api.md) (API) và [plan-ui.md](plan-ui.md) (UI). Tester Round 1: **Phạm Gia Khánh**. Nguồn dữ liệu là `cases/FE-xx/{api,ui}.json`; không gõ vào Excel, kết quả ghi vào JSON rồi `build`.

Người làm: **Khánh** = bạn, **Agent** = agent chạy theo plan, **Claude** = nhờ Claude viết.

## Trạng thái hiện tại (cập nhật 2026-11-21)

Số liệu đọc từ `cases/*/api.json`; `integration_cases.py check` 0 lỗi. Nhánh `docs/fix-integration-cases-app` đã đẩy lên `origin` (1 commit WIP, chưa mở PR). Prompt giao agent ở [prompts.md](prompts.md), báo cáo agent ở `reports/`.

**API (5.2.2): 1537/1537 case đã có `Automated:` (không còn `Manual only`); 1317 Passed, 220 Pending.**

| Feature | Case | `round1` | Ghi chú |
|---|---:|---|---|
| FE-01, 02, 03, 05 | 264 | 264 Passed | A4 xong |
| FE-04 | 116 | 116 Passed | B1 xong; cần lấy mẫu đối chiếu Expected |
| FE-06 | 195 | 195 Passed | A1 xong |
| FE-07 | 217 | 217 Passed | B2 xong; 1 case sửa nội dung (IT142) |
| **FE-08** | 136 | **27 Passed, 109 Pending** | A3 chỉ ghi kết quả cho 27 case mới viết; 109 case có test sẵn chưa điền `round1` |
| **FE-09** | 126 | **15 Passed, 111 Pending** | như FE-08 |
| FE-10 | 181 | 181 Passed | xong; cần lấy mẫu |
| FE-11, 13, 15 | 121 | 121 Passed | A2 xong |
| FE-12 | 83 | 83 Passed | xong; 3 test `MinutesTextTests` fail sẵn (`` trên Windows, không thuộc case nào) |
| FE-16 | 98 | 98 Passed | B3 xong: 220/220 test; IT71 đổi `week=not-a-date` (mẫu cũ `12-11-2026` bị hiểu là 11/12) |

**UI (5.2): R1 đã chạy 11/15 Feature (cập nhật 2026-11-22):** FE-01 17/0/17, FE-02 32/0/13, FE-03 41/0/4, FE-04 31/1/42, FE-05 38/0/9, FE-06 79/0/11, FE-07 72/0/29, FE-08 60/0/38, FE-10 33/0/52, FE-12 20/1/22 (+1 N/A), FE-16 63/0/5 (Passed/Failed/trống). FE-09, FE-11, FE-13, FE-15 chưa chạy. Còn 2 Failed: FE-04 MEET_FT01 (Back mở lại dialog tạo), FE-12 MIN_FT13 (trình soạn trên điện thoại), (CAL_FT40 đã sửa case theo app, Passed). 19 case cũ lệch sản phẩm đã sửa theo sản phẩm 2026-11-22 (19 Passed sau khi chạy lại DOC_FT07/FT70; CAL_FT11 Passed nhờ AC34 mới). Chi tiết `reports/R1.md`. U1 và U2 đã xong: plan Playwright đã sửa (có sửa `tests/qa/plans/fe-15.mjs`) và bảng ánh xạ AC nằm ở `ui-map/` cho 12 Feature (FE-04, 10, 12 nằm trong plan-ui.md).

### Việc còn lại, theo thứ tự

1. [ ] **F1** (một agent, sau khi mọi agent API báo xong, hiện đã đủ điều kiện): chạy `dotnet build TrustCabinet.sln -warnaserror`, rồi `dotnet test` cả solution, `apply_trx.py` không giới hạn Feature, `check`, `build`, dọn container Postgres phụ (`fe*-pg`). F1 cũng lấp 220 case Pending của FE-08/FE-09.
2. [ ] **Lấy mẫu đối chiếu Expected** 5–10 case ở FE-04, FE-10, FE-12 (đọc test được trích dẫn, so từng dòng Expected với `Assert`).
3. [ ] **R1**: chạy UI thật bằng Playwright, theo `ui-map/`. Xong 11/15 Feature; còn FE-09 (69), FE-11 (23), FE-13 (35), FE-15 (17) giao anh em.
4. [ ] **Nhóm H** (51 case UI): Khánh làm tay.
5. [ ] Build và chốt hai workbook (Bước 4 và Bước 8), rồi mở PR.

## Bước 0 — Chuẩn bị chung

| # | Việc | Người | Xong khi |
|---|---|---|---|
| 0.1 | ✅ Chốt các sửa JSON ngày 19–20/11 đang uncommitted (FE-03, 04, 07, 10, 11, 12, 16) | Khánh | Đã xem lại diff, chạy `python scripts/test_reports/integration_cases.py check` được 0 lỗi |
| 0.2 | Bật PostgreSQL (`docker compose up -d`), đặt user-secrets, chạy được `dotnet test` | Khánh | `dotnet test TrustCabinet.sln` chạy xong, không lỗi hạ tầng |
| 0.3 | Soát drift các vùng chưa soát: phòng mở tab mới, doc viewer overlay (FE-07, FE-08 UI), cập nhật realtime (FE-08, bình luận FE-07) | Agent | Case lệch đã sửa trong JSON, ghi lại trong báo cáo của agent |
| 0.4 | ~~Viết script đọc `.trx`~~ **Đã có:** `scripts/test_reports/apply_trx.py <file.trx> [--features FE-04 ...] [--dry-run]`; ghi `round1`/`date1`/`tester1` vào `api.json`, giữ định dạng và CRLF | Claude | Đã thử trên bản sao FE-01; chạy `--dry-run` trước khi ghi thật |

Bước 0.4 chặn Bước 2. Làm sớm.

## Bước 1 — Soát case API (Phase 0 của api-plan)

- [ ] Từng Feature: đối chiếu *Expected* với `Assert` của test được trích dẫn; đối chiếu case `Manual only` với endpoint và `docs/03-api/`.
- [ ] Soát hành vi 19–20/11: giới hạn tải tệp 10/100 MB, dự thảo và biên bản chỉ khi họp đã kết thúc, Thư ký được bắt đầu họp, tìm không dấu, lịch 7 ngày, nhật ký.
- [ ] Soát ranh giới 403 và 404, loại case trùng.
- [ ] Mỗi Feature xong thì chạy `check`; có sửa thì chạy `build`.

Xong khi: cả 15 Feature API đã soát, `check` 0 lỗi.

## Bước 2 — Chạy test API có sẵn (Phase 1)

- [ ] `dotnet test TrustCabinet.sln --logger "trx;LogFileName=round1.trx"`.
- [ ] Chạy script 0.4 để điền `round1` cho **1236** case có `Automated:`.
- [ ] Test pass nhưng không phủ hết Expected thì để trống hoặc sửa case, không đánh `Passed`.
- [ ] Test fail vì code sai thì case `Failed`, mở ticket Jira, ghi mã ticket vào `note`.

Xong khi: mọi case `Automated:` có `round1` là `Passed`, `Failed` hoặc để trống kèm lý do.

## Bước 3 — Viết test cho case API chưa có test (Phase 2)

- [ ] **B1 (259 case):** agent `integration-test-writer` viết xUnit, chạy `dotnet test`, đổi `note` từ `Manual only` sang `Automated:`, rồi điền `round1`.
- [ ] **B2 (42 case):** chèn lỗi, dịch vụ giả, đồng hồ giả. Làm được thì viết test; không thì `N/A` kèm lý do.
- [ ] `check` 0 lỗi sau mỗi Feature.

Thứ tự Feature: FE-04, FE-12, FE-10, FE-07, FE-16, rồi các Feature còn lại.

Xong khi: không còn case `Manual only` nào chưa có kết quả hoặc lý do `N/A`.

## Bước 4 — Build và chốt Report 5.2.2

- [ ] `python scripts/test_reports/integration_cases.py build`.
- [ ] Mở `Report5.2.2_Integration_Test_API_Test.xlsx`: kiểm đếm Passed / Failed / N/A / trống từng sheet.
- [ ] Mỗi case `Failed` có ticket Jira trong `note`.

Xong khi: 5.2.2 không còn case trống ngoài ý muốn.

## Bước 5 — Cập nhật plan Playwright (UI, bước 1 của plan-ui)

- [ ] Sửa `tests/qa/plans/fe-xx.mjs` theo hành vi mới. FE-04 đã biết: AC17, AC18 (Thư ký được bắt đầu họp) và AC23 (danh sách không mở sẵn "Họp hôm nay").
- [ ] Soát tiếp FE-07 (10/100 MB), FE-10 và FE-12 (khoá tab Biên bản), FE-16 (lịch 7 ngày, chọn thứ Bảy/Chủ nhật), các Feature còn lại.
- [ ] Làm bảng ánh xạ D / P / N cho 12 Feature chưa có (đã có FE-04, FE-10, FE-12).

Xong khi: không plan nào còn kiểm hành vi cũ.

## Bước 6 — Chạy UI (bước 2 và 3 của plan-ui)

- [ ] Bật backend :5225, frontend :5173, PostgreSQL.
- [ ] Mỗi Feature: `cd tests/qa; node run.mjs plans/fe-xx.mjs` trên desktop và điện thoại. Agent xem từng ảnh rồi mới kết luận.
- [ ] Ánh xạ vào `ui.json`: case **D** điền `round1`, `date1`, `tester1` và đường dẫn ảnh trong `note`; case **P** mở rộng AC rồi chạy lại; case **N** viết AC mới.
- [ ] **Nhóm AX (152 case):** hai phiên, chèn lỗi, chờ thời gian. Viết AC riêng, không được thì làm tay.
- [ ] Thứ tự Feature: FE-04, FE-12, FE-10, FE-07, FE-16, rồi các Feature còn lại.

Xong khi: mọi case nhóm A và AX có `round1`.

## Bước 7 — Case người làm (nhóm H, 51 case)

- [ ] **Khánh** làm tay: Google thật, email thật, micro, giọng nói, kiểm hình ảnh. Danh sách trong [plan-ui.md](plan-ui.md) mục "Case của người (nhóm H)".
- [ ] Điền `round1`, `date1`, `tester1` và bằng chứng trong `note`.

Xong khi: 51 case H có kết quả.

## Bước 8 — Build và chốt Report 5.2

- [ ] `python scripts/test_reports/integration_cases.py check`, rồi `build`.
- [ ] Mở `Report5.2_Integration_Test.xlsx`: kiểm đếm từng sheet, logo và định dạng còn nguyên.
- [ ] Mỗi case `Failed` có ticket Jira trong `note`.

Xong khi: 5.2 không còn case trống ngoài ý muốn.

## Bước 9 — Đóng vòng

- [ ] Cập nhật [README.md](../README.md) và [AGENTS.md](../AGENTS.md) nếu quy trình đổi.
- [ ] Tổng hợp số liệu Round 1: Passed / Failed / N/A / trống, cho cả hai report.
- [ ] Commit và mở PR **khi Khánh cho phép**. Không đưa vào: `.vscode/`, các file `.bak`, nhiễu CRLF ở file preview.
- [ ] Round 2 và 3 dùng lại quy trình này với `round2`/`date2`/`tester2` và `round3`/`date3`/`tester3`.

## Ngoài phạm vi lần này

Report 5.0, 5.3 và 5.4 chưa làm. Report 5.1 (unit) có pipeline riêng, xem README.
