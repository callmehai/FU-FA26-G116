# Bug và nghi bug từ Round 1 UI (5.2)

Một chỗ để theo dõi mọi lỗi sản phẩm tìm thấy khi chạy UI Round 1. Nguồn: `cases/FE-xx/ui.json` (note của case) và `reports/R1.md`. Case cũ lệch sản phẩm **không** nằm ở đây — chúng đã được sửa theo sản phẩm (2026-11-22). Cập nhật file này mỗi khi có lần chạy lại hoặc khi có ticket Jira.

Trạng thái: `Mới` · `Đã báo dev` · `Đã sửa, chờ chạy lại` · `Đã đóng` · `Không phải bug`.

Tổng quan (2026-11-23): không còn bug hay nghi bug. Tester HuyNT xác nhận ngày 2026-11-23 các mục BUG-01, BUG-02, SUS-01, SUS-02, SUS-03 (và SUS-04 tìm thấy cùng ngày) không phải bug; đã xoá khỏi file này, các case liên quan (MEET_FT01, REC_FT27, MIN_FT09) ghi Passed. Các lệch app/case đã chốt giữ app (§3).

## 1. Bug xác nhận

Không có.

## 2. Nghi bug (chưa kết luận)

Không có.

## 3. Quyết định BA/PO

PO chốt 2026-11-22: **giữ app, sửa case/UC theo app**. Không còn mục nào chờ quyết, các việc dưới là việc đã làm hoặc việc BA cần làm.

| Mã | Mục | Quyết định | Việc đã/cần làm |
|---|---|---|---|
| QD-01 | CAL_FT40 (FE-16): "Xin thay đổi" chưa đổi gì, nút "Gửi yêu cầu" khoá, không hiện câu hướng dẫn | Giữ app | Đã sửa case theo app, Passed (`ShareDialogs.tsx:473` giữ nguyên) |
| QD-02 | AC10 (FE-10): bỏ câu thoại ngay, không hỏi lý do (`features/speech/SpeechTimeline.tsx:517`), lệch UC-46c bước 2–3 | Giữ app | BA thu hẹp UC-46c ở bước BA Final Review (không có case để sửa) |
| QD-04 | FT76/77 (FE-08): nút "Mời" của người chờ ẩn hẳn khi đã có người giữ lượt (`SpeakingPanel.tsx:289`) | Giữ app | Đã sửa câu chữ expected của hai case thành "ẩn" |
| QD-03 | Form tạo họp không hiện cùng lúc lỗi ngày và giờ | Bỏ mục | Không có case nào đòi; mục viết từ nhận xét cũ chưa kiểm |

## 4. Case còn Failed vì thiếu AC

Không còn. CAL_FT11 (FE-16) đã có AC34 trong `tests/qa/plans/fe-16.mjs` (từ chối trong pop-over, họp biến mất ngay, tải lại và chế độ Danh sách không hiện lại, họp còn lại và họp không liên quan giữ nguyên, API `excludeDeclined=true` loại họp đã từ chối). Đạt ngày 2026-11-22, bằng chứng `tests/qa/out/SCRUM-203/20261122-075728-eb5a3255/`.

Đã chạy lại và đạt cùng ngày: DOC_FT07 (FE-07 AC2, `20261122-074401-eb5a3255`) và DOC_FT70 (FE-07 AC17 thêm bước cuộn tới "Trang 3 / 3", `20261122-075848-eb5a3255`).

## 5. Ghi chú môi trường khi chạy lại (2026-11-22)

- DB dev dùng lại có `role_capabilities` lệch seed (Thành viên có `MeetingCreator`, Quản trị có thêm 3 quyền) làm FE-16 AC24 lỗi giả. Đã xoá 4 dòng thừa; chạy FE-03 (AC sửa ma trận quyền) thì kiểm lại ma trận trước khi chạy FE-16.
- Backend cần `TMP`/`TEMP` trỏ tới thư mục ghi được (`%USERPROFILE%\AppData\LocalLow\TrustCabinetQA`), nếu không upload tài liệu và PDF lỗi 500 `UnauthorizedAccessException`.
- Cổng 55432 bị Windows giữ (excluded port range): Postgres chạy ở 55620 qua `POSTGRES_PORT` và `ConnectionStrings__TrustCabinet`.
- Lần chạy FE-09, FE-11, FE-13, FE-15 (Nguyễn Trường Huy, 2026-11-22): backend chạy với `Email__Smtp__Enabled=false` (máy có SMTP Gmail thật, không để test gửi thư thật). Speech worker và ffmpeg không có, nên case cần bản gỡ băng, câu nói thật, mã OTP Khách qua email hoặc tải bản ghi định dạng khác mp3 để trống. Âm thanh trong plan FE-09 là tệp âm tổng hợp.
- Sau ADR-0075 DB chỉ còn ba vai trò hệ thống (Phê duyệt viên, Quản trị viên, Thành viên). Plan nào tự tạo tài khoản với vai trò "Thư ký" sẽ nhận 400 MSG09. Đã đổi `fe-15.mjs` sang Thành viên.

## 6. Lần chạy tay 2026-11-23 (HuyNT)

Theo note bàn giao `docs/notes/NOTE-ban-giao-manual-ui-round1.md`. Kết quả đã ghi vào `round1` / `date1` / `tester1` của từng case trong `cases/FE-xx/ui.json` (51 case, ghi chú bắt đầu bằng "Round1 chạy tay 23/11").

- **Môi trường:** app = origin/app `e7dc0e7a` (nhánh này không đổi `backend/`, `frontend/`), backend tự phục vụ bản build production ở `:5226` (không qua Vite), database riêng `trustcabinet_r1` (seed Development), SMTP vào mailpit (`localhost:1025`, xem thư ở `localhost:8025`), worker nhận dạng giọng nói chạy bằng `docker compose --profile speech`, LiveKit local. Case chạy bằng `tests/qa/plans/manual-r1.mjs` (mỗi tiêu chí một case, id = mã case) và các AC có sẵn của `fe-04`, `fe-09`, `fe-11`, `fe-12`. Bằng chứng ở `tests/qa/out/MANUAL-R1/20261123-*`, `FE-04/…`, `SCRUM-196/…`, `SCRUM-198/…`, `SCRUM-199/…`.
- **Kết quả:** 51 Passed. MEET_FT01 và REC_FT27 ghi Passed theo xác nhận của tester là không phải bug. Nhiều case Passed có ghi "Lệch case" khi app làm khác chữ của case (giữ app theo §3): RBAC_FT01 (không có nhóm/quyền "Biên bản"), MIN_FT03/MIN_FT11/MIN_FT18 (biên bản chỉ tạo được sau khi kết thúc, tab khoá trước đó), MEET_FT37 (nút trong hộp "Chưa thể bắt đầu" khoá, API 409 MSG112), AGENDA_FT03 (tài liệu mở trong hộp xem, không đổi URL), ROOM_FT02 (camera tắt sẵn), ROOM_FT54 (phải bấm "Tải lại" mới hiện "Chưa lưu được"), MEET_FT14 (phụ đề theo quyền thật của chutoa@).
- **Âm thanh:** máy không có giọng đọc tiếng Việt; REC_FT35/36 dùng giọng máy tiếng Anh (Windows TTS) chỉ để worker cắt ra câu. Các case cần nội dung tiếng Việt đúng (nhóm A còn lại) vẫn chưa chạy.
- **FE-11 AC17/AC18 (nghi upload tài liệu trả 500):** chạy lại 2026-11-23 đều đạt (`tests/qa/out/SCRUM-198/20261123-073902-03594e25`); upload tài liệu vào mục nghị sự và vào biểu quyết đều 200. Không tái hiện 500; khả năng cao là lỗi môi trường đã ghi ở §5 (`TMP`/`TEMP` không ghi được → `UnauthorizedAccessException`).
- **Còn chưa chạy:** nhóm A trừ REC_FT35/36 (cần tiếng Việt thật, và TRANS_FT29–41 cần máy AI tạo dự thảo — máy này không có), nhóm B (Voice ID, micro thật), nhóm C (khoá API thử), Google (AUTH_FT08–11), AUTH_FT14, AUTH_FT23, VOTE_FT15, ROOM_FT14/15/25, DOC_FT27, DOC_FT51 (cần PDF nhiều trang), ROOM_FT24, ROOM_FT45, AGENDA_FT04, MIN_FT02 (cần dự thảo AI), RPT_FT11, và các case cần máy thật (REC_FT20, REC_FT24).
