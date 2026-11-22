# Checklist Round 1 — test đơn vị 5.1 (unit)

Thứ tự làm từ trên xuống. Chạy gần như hoàn toàn bằng máy; người chỉ quyết định ở Bước 0 và duyệt ở Bước 4. Nguồn dữ liệu là `cases/FE-xx/unit/<sheet>.json`; không gõ vào Excel, kết quả ghi vào JSON rồi `build`. Tester Round 1: **Phạm Gia Khánh**. Quy tắc viết UTC: [AGENTS.md](../AGENTS.md) mục 6; quy trình build an toàn: cùng file, "Safe rebuild workflow".

Người làm: **Khánh** = bạn, **Agent** = agent chạy theo prompt, **Claude** = nhờ Claude viết.

## Trạng thái hiện tại (cập nhật 2026-11-21)

- 1984 UTC trên 157 sheet (157 method), **tất cả `Pending`**: JSON không có `round1`, `date1`, `tester1`. Workbook không có kết quả nào để `sync-back`.
- `functions.json` chưa có `directTests` ở method nào. `Application.Tests` có 365 test pass, nhưng viết theo ý tác giả test, không theo từng UTC.
- Mã nguồn UTC mô tả được ghim ở `source.ref` = `7286680a` (nhánh `app`).
- `apply_trx.py` chỉ ghi `api.json` và dựa vào `Automated:`, mà UTC cấm `Automated:`. Cần công cụ riêng (Bước 1).

## Bước 0 — Quyết định

| # | Việc | Người | Xong khi |
|---|---|---|---|
| 0.1 | So mã production giữa `7286680a` và nhánh sẽ chạy: `git diff 7286680a -- backend` | Claude | Biết có lệch không; lệch thì chạy trên `7286680a` hoặc `relink` rồi soát lại UTC |
| 0.2 | Chốt cách nối UTC với test: mỗi UTC một test xUnit có trait `UTC=<Class.Method>/<UTCnnn>` | Khánh | Đồng ý bằng lời |

## Bước 1 — Công cụ ghi kết quả cho 5.1

- [ ] Thêm lệnh vào `scripts/test_reports/unit_cases.py` (ví dụ `apply-trx`): đọc `.trx`, khớp trait UTC, ghi `round1`, `date1`, `tester1` vào `cases/FE-xx/unit/*.json`. Có `--dry-run` và `--features`; giữ CRLF của JSON.
- [ ] Case có test fail do môi trường: không ghi `Failed`, để `Pending` kèm lý do trong báo cáo.

Người làm: Claude. Xong khi: chạy thử trên một sheet, `check` 0 lỗi.

## Bước 2 — Viết test cho UTC

- [ ] Chia agent theo Feature (mỗi agent một nhóm sheet, song song). Mỗi agent chỉ sửa JSON và test của nhóm mình.
- [ ] Test viết **từ Expected của UTC** (`Return` / `Exception` / `Log Message`), đặt trong file mới của `tests/TrustCabinet.Application.Tests/`, không sửa test có sẵn và không sửa `Fakes/` dùng chung (chỉ được thêm helper nhỏ, ghi vào báo cáo).
- [ ] Test chỉ được coi là phủ UTC khi có `Assert` cho cả ba dòng. Không phủ hết thì thu hẹp case hoặc đánh `N/A` kèm lý do; không đánh `Passed` cho case chưa được kiểm hết.
- [ ] Chạy bằng `--artifacts-path` riêng và `--filter`; không chạy cả solution.
- [ ] Hàm không gọi được trực tiếp (chỉ có đường HTTP): ghi vào báo cáo như một khoảng trống, không viết test qua HTTP.

Người làm: Agent. Xong khi: mọi UTC có test hoặc `N/A` kèm lý do; `check` 0 lỗi.

## Bước 3 — Chạy chính thức một lần

- [ ] `dotnet build TrustCabinet.sln -warnaserror`.
- [ ] `dotnet test tests/TrustCabinet.Application.Tests --logger "trx;LogFileName=round1-unit.trx" --results-directory TestResults`. Không cần PostgreSQL.
- [ ] Ghi kết quả bằng lệnh ở Bước 1 (`--dry-run` trước).
- [ ] Phân loại mỗi test fail: môi trường (chạy lại) hay sản phẩm (giữ `Failed` kèm bằng chứng). Không mở Jira.

Người làm: một Agent. Xong khi: không còn `Pending` ngoài ý muốn.

## Bước 4 — Duyệt (phần duy nhất cần người)

- [ ] **Khánh** lấy mẫu mỗi nhóm lớn khoảng 10 UTC: đọc test, so từng dòng Expected với `Assert`.
- [ ] Mỗi `Failed` do lỗi sản phẩm: mở ticket Jira (bạn quyết), ghi mã vào `note`.

Xong khi: tỷ lệ khớp mẫu đạt; không nhóm nào cần soát lại toàn bộ.

## Bước 5 — Chốt Report 5.1

- [ ] `python scripts/test_reports/unit_cases.py check`.
- [ ] `build` (dry-run) rồi đưa diff cho Khánh; **chờ OK** trước khi ghi đè.
- [ ] `sync-back` nếu workbook cũ có kết quả, rồi `build --overwrite` (tự sao lưu `.bak-…`). Đóng Excel trước.
- [ ] Mở `.xlsx`: kiểm đếm từng sheet, font Calibri 11 và style khớp 5.2.2, cover có dòng change log của Round 1.
- [ ] Mỗi case `Failed` có ticket Jira trong `note`.

Xong khi: 5.1 không còn case trống ngoài ý muốn.

## Bước 6 — Đóng vòng

- [ ] Cập nhật [README.md](../README.md) và [checklist.md](checklist.md).
- [ ] Tổng hợp số liệu: Passed / Failed / N/A / trống.
- [ ] Commit và mở PR **khi Khánh cho phép**. Không đưa vào: `.vscode/`, file `.bak`, nhiễu CRLF.
- [ ] Round 2 và 3 dùng lại quy trình với `round2`/`date2`/`tester2` và `round3`/`date3`/`tester3`.

## Chạy song song với việc khác

- Bước 2 chạy song song được với R1 (UI) nếu mỗi agent giữ file riêng. Bước 3 chạy `dotnet test` cho Application.Tests, không dùng DB hay cổng nên không tranh với R1.
- Hai agent không được cùng sửa một file `cases/**`, `.xlsx`, hay `scripts/test_reports/`.
