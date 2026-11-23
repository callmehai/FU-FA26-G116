# Bàn giao: case UI Round 1 chưa chạy tự động + bug cần xác nhận thủ công

Ngày 23/11/2026. Nguồn: `docs/08-reports/test-reports/cases/FE-09|11|13|15/ui.json`, lý do từng case ở
`round1/ui-map/FE-xx.md`, bug ở `round1/bugs.md`. Đã chạy lại bằng runner `tests/qa/` trên máy local
(backend :5225, Docker đủ postgres/redis/SeaweedFS/mailpit/LiveKit/speech). Chưa ghi gì vào `ui.json` hay `bugs.md`:
người làm manual điền kết quả sau khi tự chạy.

## 1. Case để trống, chia theo lý do

### A. Cần tiếng nói tiếng Việt thật (không tự động được trên máy này)

Plan tạo tiếng bíp khi máy không có `say`/ffmpeg; worker nhận dạng trả 0 đoạn. Worker và backend vẫn chạy đúng
(đã kiểm log). Cách làm manual: vào phòng hoặc tải lên một file ghi âm người nói tiếng Việt, đợi bản ghi lời.

FE-09: REC_FT13, FT24, FT45–FT48, FT51–FT60, FT62–FT68 (và AC21–AC23 của plan).

### B. Gửi email hoặc mã OTP: tự động được bằng mailpit, chưa viết

Hộp thư test: http://localhost:8025 (API `/api/v1/messages`). Plan hiện chưa đọc mailpit. Làm manual: thực hiện
thao tác rồi mở mailpit xem thư.

- FE-13: NOTI_FT29 (mời khi công bố), FT30 (người tổ chức cũng nhận), FT31 (dời lịch), FT32 (huỷ, không có đường
  dẫn), FT33 (nhắc 15 phút trước), FT35 (nháp/chưa công bố thì không gửi).
- FE-11: VOTE_FT16 (khách đã xác thực bỏ phiếu kín).
- FE-09: REC_FT50 (khách phải tích thông báo ghi âm), FT69 (khách chỉ thấy và xoá đoạn của mình).
- FE-15: RPT_FT12 (Báo cáo mở không cần capability; khách không dùng được).

### C. Cần hai người trong phòng LiveKit cùng lúc

Runner chưa mô phỏng hai người trong phòng: FE-11 AC22. Làm manual bằng hai trình duyệt.

### D. Phụ thuộc môi trường, cần máy thật

REC_FT01, FT08, FT17, FT30, FT31, FT32: xem `round1/ui-map/FE-09.md` (nghe phát lại, mạng chậm, họp Hybrid đã kết thúc,
tải file ghi âm sau khi xác nhận đồng ý).

## 2. Lỗi (Failed) và nghi vấn: kết luận sau khi tái hiện

| Mã | Kết luận | Cách tự tái hiện |
|---|---|---|
| BUG-02 / REC_FT36 (file quá giới hạn) | **Bug thật.** Sửa lại nguyên nhân: backend trả 413/MSG293 sớm rồi đóng kết nối, trình duyệt nhận lỗi mạng chứ không thấy mã 413 | Secretary, tab Bản ghi, tải lên file ghi âm lớn hơn giới hạn (đã thử 301 MB), xem toast |
| SUS-02 (tooltip trên điện thoại) | **Bug thật.** `TooltipLayer.tsx` bỏ qua `pointerover` kiểu touch, chỉ `InfoTip` có đường chạm. Dòng `.recording-meta` ("Lưu bởi / Mục đích / Tự xoá") không hiện khi chạm | 390px, tab Bản ghi, chạm vào dòng meta |
| SUS-03 (gợi ý còn hiện) | **Bug nhẹ về câu chữ.** Gợi ý vẫn hiện dù dock đã mất khi LiveKit chạy tốt | Vào phòng online lúc LiveKit chạy tốt, xem gợi ý |
| SUS-01 (con trỏ nhảy) | **Nhiều khả năng là bug.** Ở 390px sau Enter con trỏ nhảy về ô bảng đầu dù sau một lần chạm thật con trỏ vẫn đúng chỗ | Cần máy thật hoặc trình giả lập thật: biên bản, cuối đoạn "Cuộc họp kết thúc vào", chạm rồi Enter, gõ chữ, xem chữ rơi đâu |

## 3. Chưa kết luận: cần người xác nhận lỗi môi trường hay lỗi code

- **FE-11 AC17, AC18:** bước tải tài liệu cuộc họp lên (`POST /api/meetings/{id}/documents/upload`) trả HTTP 500.
  SeaweedFS đang chạy; chưa rõ do cấu hình `FileStorage:*` hay do code. Việc cần làm: lấy log backend lúc gọi
  (cuộc họp phải đang `InProgress`).
- **Lần chạy lại toàn bộ có lỗi:** FE-09 AC13, AC21, AC22; FE-11 AC13, AC17, AC18, AC31; FE-13 AC26. Phần lớn giống
  lỗi môi trường (không có giọng nói, kho tệp); phân loại trước khi ghi `Failed`. FE-09 AC9 chạy đơn lẻ thì đạt,
  chỉ chập chờn khi chạy nguyên bộ (ECONNRESET).

## 4. Chạy lại bằng runner

```
cd tests/qa
node run.mjs plans/fe-11.mjs --only AC17 --device desktop --base http://localhost:5225
```

Kết quả ở `tests/qa/out/<SCRUM-n>/<giờ>-<commit>/` (`summary.md`, `report.html`, ảnh). Mã SCRUM: FE-09 = 196,
FE-11 = 198, FE-13 = 200, FE-15 = 202. Tài khoản seed dùng mật khẩu `TrustCabinet@2026`.

Lưu ý khi dựng môi trường trên Windows: tắt EventLog logger (`Logging__EventLog__LogLevel__Default=None`) vì lỗi
"Access is denied" làm backend reset kết nối; Vite nghe IPv6 nên dùng `localhost`, không dùng `127.0.0.1`; đĩa đầy làm
file test sinh ra bị cắt về 0 byte.
