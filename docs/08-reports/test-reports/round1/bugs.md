# Bug và nghi bug từ Round 1 UI (5.2)

Một chỗ để theo dõi mọi lỗi sản phẩm tìm thấy khi chạy UI Round 1. Nguồn: `cases/FE-xx/ui.json` (note của case) và `reports/R1.md`. Case cũ lệch sản phẩm **không** nằm ở đây — chúng đã được sửa theo sản phẩm (2026-11-22). Cập nhật file này mỗi khi có lần chạy lại hoặc khi có ticket Jira.

Trạng thái: `Mới` · `Đã báo dev` · `Đã sửa, chờ chạy lại` · `Đã đóng` · `Không phải bug`.

Tổng quan (2026-11-22): 2 bug xác nhận, 3 nghi bug. Các lệch app/case đã chốt giữ app (§3). BUG-02, SUS-02 và SUS-03 tìm thấy khi chạy FE-09, FE-11, FE-13, FE-15 (Nguyễn Trường Huy, 2026-11-22).

## 1. Bug xác nhận

### BUG-01 — Back của trình duyệt mở lại dialog "Tạo cuộc họp" trống (MEET_FT01, FE-04)

- Trạng thái: Mới · Jira: chưa tạo.
- Hiện tượng: Thư ký tạo cuộc họp xong, trang chuyển sang chi tiết. Bấm Back của trình duyệt thì quay về danh sách và dialog "Tạo cuộc họp" **trống** mở lại, vì URL `?create=…` còn trong lịch sử. Bấm "Tạo cuộc họp" lần nữa sẽ tạo họp trùng. Vi phạm ngoại lệ E2 của UC.
- Tái hiện bằng tay (desktop hoặc phone đều ra):
  1. Đăng nhập Thư ký, vào `/mgmt/meetings`.
  2. Bấm "Tạo cuộc họp", nhập tiêu đề và thời gian hợp lệ, bấm tạo. Trang chuyển sang `/mgmt/meetings/{id}`.
  3. Bấm nút Back của trình duyệt.
  4. Kết quả sai: danh sách hiện lại và dialog "Tạo cuộc họp" mở sẵn, các ô trống. Kết quả đúng: danh sách hiện, dialog không mở.
- Tái hiện bằng runner: `cd tests/qa; node run.mjs plans/fe-04.mjs --only AC1 --base http://localhost:5173`. Lần 2026-11-21 và 2026-11-22, desktop và phone.
- Bằng chứng: `tests/qa/out/FE-04/20261122-074043-eb5a3255/04-secretary-AC1-desktop-failure.png`, `08-secretary-AC1-phone-failure.png`.
- Hướng sửa gợi ý: điều hướng sau khi tạo dùng `replace` (thay mục lịch sử) thay vì `push`, hoặc xoá tham số `create` trước khi chuyển trang.

### BUG-02 — Tải lên bản ghi quá 300 MB báo "Không kết nối được máy chủ" thay vì MSG293 (REC_FT36, FE-09)

- Trạng thái: Mới · Jira: chưa tạo.
- Hiện tượng: Thư ký tải lên tệp ghi âm 301 MB. Khay tải lên báo "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại." thay vì "Bản ghi vượt quá 300 MB." (MSG293). Người dùng tưởng mất mạng và bấm thử lại, lần nào cũng gửi lại 301 MB rồi hỏng y như cũ. Vi phạm UC-44c (EX quá dung lượng) và BR-340–BR-357 về giới hạn 300 MB.
- Nguyên nhân (đã đối chiếu code): `RecordingEndpoints.UploadAsync` (`backend/TrustCabinet.Web/Endpoints/RecordingEndpoints.cs:170–186`) đọc tới 300 MB rồi trả 413/MSG293 khi trình duyệt vẫn đang gửi phần còn lại, nên kết nối bị cắt và trình duyệt chỉ thấy lỗi mạng. Gọi API trực tiếp vẫn nhận đúng 413/MSG293 (FE-09 AC9 đạt). Frontend (`frontend/src/features/recordings/`) không kiểm dung lượng tệp trước khi gửi.
- Tái hiện bằng tay (desktop):
  1. Đăng nhập Thư ký (thuky@), mở một cuộc họp Trực tiếp đang diễn ra hoặc đã kết thúc, tab "Bản ghi".
  2. Bấm "Tải tệp lên", chọn một tệp âm thanh 301 MB, tick xác nhận đồng ý, bấm "Tải lên".
  3. Kết quả sai: khay báo "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.". Kết quả đúng: khay báo "Bản ghi vượt quá 300 MB." và giữ tệp để người dùng chọn tệp khác.
- Tái hiện bằng runner: `cd tests/qa; node run.mjs plans/fe-09.mjs --only AC32 --base http://localhost:5173`.
- Bằng chứng: `tests/qa/out/SCRUM-196/20261122-135539-39c00e3b/07-secretary-AC32-desktop-failure.png`.
- Chưa kiểm: trên server test qua Caddy. Máy local đi qua proxy của Vite.
- Hướng sửa gợi ý: frontend chặn tệp lớn hơn giới hạn trước khi gửi (`file.size`) và hiện MSG293. Nếu vẫn muốn backend trả lời trước khi nhận hết thân yêu cầu thì đọc `Content-Length` để từ chối sớm.

## 2. Nghi bug (chưa kết luận)

### SUS-01 — Trình soạn biên bản trên điện thoại chèn đoạn sai chỗ (MIN_FT13, FE-12)

- Trạng thái: Mới · Jira: chưa tạo.
- Hiện tượng: ở 390px (phone), thêm đoạn mới sau dòng "Cuộc họp kết thúc vào …" thì đoạn lại nằm ở **ô đầu bảng tiêu đề** (vị trí khoảng 215 trong nội dung) thay vì sau dòng kết thúc (vị trí lớn hơn 1750). Desktop đúng. Kiểm bằng API `GET` nội dung biên bản.
- Mức chắc chắn: 3 lần ra cùng kết quả (2 lần 2026-11-21, 1 lần 2026-11-22), chỉ phone. Chưa rõ lỗi ở trình soạn trên cảm ứng hay ở helper đặt con trỏ của plan.
- Cách tái hiện bằng tay (làm trên điện thoại thật, hoặc Chrome DevTools bật giả lập thiết bị 390px có cảm ứng):
  1. Dùng một cuộc họp đã kết thúc có biên bản nháp (từ mẫu cố định, có bảng tiêu đề ở đầu và dòng "Cuộc họp kết thúc vào …" ở cuối). Đăng nhập Thư ký, mở tab Biên bản, bấm Sửa.
  2. Cuộn xuống cuối, chạm vào cuối dòng "Cuộc họp kết thúc vào …" để đặt con trỏ ở đó.
  3. Bấm Enter (bàn phím ảo), gõ vài chữ, ví dụ "Đoạn thử".
  4. Lưu biên bản, đọc lại nội dung (mở bản xem hoặc `GET` nội dung biên bản).
  5. Kết quả đúng: "Đoạn thử" nằm ngay sau dòng kết thúc. Kết quả nghi bug: "Đoạn thử" nằm trong ô đầu bảng tiêu đề.
- Cách phân biệt hai khả năng: nếu chạm tay thật vẫn sai thì là bug trình soạn trên cảm ứng (mở Jira cho FE). Nếu chạm tay thật đúng thì lỗi nằm ở helper `typeNewParagraph` trong `tests/qa/plans/fe-12.mjs` (AC3, khoảng dòng 487–540: đặt con trỏ bằng JS Range rồi Enter và gõ; trên phone Range không đồng bộ với vùng chọn của trình soạn), sửa helper và đóng nghi bug.
- Lệnh chạy lại: `cd tests/qa; node run.mjs plans/fe-12.mjs --only AC3 --base http://localhost:5173`.

### SUS-02 — Trên điện thoại không xem được người lưu, mục đích và ngày tự xoá của bản ghi (REC_FT02, REC_FT34, FE-09)

- Trạng thái: Mới · Jira: chưa tạo.
- Hiện tượng: trên desktop, rê chuột vào dòng thông tin của bản ghi ("Tải lên · dung lượng · ngày") thì hiện tooltip "Lưu bởi … · Mục đích … · Tự xoá ngày …". Ở 390px có cảm ứng, chạm vào dòng đó không hiện gì, nên người dùng điện thoại không biết mục đích lưu và ngày tự xoá (BR-353, giữ 180 ngày). Trái quy tắc của skill check-responsive: không thông tin nào chỉ hiện khi rê chuột.
- Tái hiện bằng tay (điện thoại thật, hoặc Chrome DevTools giả lập 390px có cảm ứng):
  1. Đăng nhập Thư ký hoặc Thành viên, mở tab "Bản ghi" của một cuộc họp đã có bản ghi.
  2. Chạm vào dòng thông tin dưới tên bản ghi.
  3. Kết quả nghi bug: không hiện tooltip, không có cách nào khác xem "Lưu bởi / Mục đích / Tự xoá ngày". Kết quả đúng: chạm thì hiện, hoặc thông tin hiện sẵn trên điện thoại.
- Bằng chứng: `tests/qa/out/SCRUM-196/20261122-133302-39c00e3b/12-secretary-AC19-phone-failure.png`.
- Mức chắc chắn: runner chạm bằng mô phỏng cảm ứng của Playwright. Cần thử trên máy thật để chắc. FT02 và FT34 vẫn ghi Passed theo bằng chứng desktop, có ghi chú hạn chế này.

### SUS-03 — Cuộc họp Kết hợp vẫn nhắc "Bật mic phòng ở ô góc màn hình." dù trang không còn ô mic (REC_FT37, FE-09)

- Trạng thái: Mới · Jira: chưa tạo. Mức: nhỏ (câu chữ).
- Hiện tượng: Thư ký mở tab "Bản ghi" của cuộc họp Kết hợp đang diễn ra, chưa có bản ghi. Trang hiện gợi ý "Bật mic phòng ở ô góc màn hình." nhưng không có ô mic nào ở góc, vì ADR-0072 đã chuyển công tắc mic phòng của họp Kết hợp vào phòng họp trực tuyến.
- Tái hiện bằng tay: đăng nhập Thư ký, mở một cuộc họp Kết hợp đang diễn ra chưa có bản ghi, tab "Bản ghi", đọc dòng gợi ý dưới "Chưa có bản ghi.".
- Bằng chứng: `tests/qa/out/SCRUM-196/20261122-133302-39c00e3b/01-secretary-AC10-desktop-ket-hop-chi-mic-phong.png`.
- Cần FE xác nhận gợi ý đúng cho họp Kết hợp (ví dụ chỉ vào phòng họp trực tuyến) rồi mới coi là bug.

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
