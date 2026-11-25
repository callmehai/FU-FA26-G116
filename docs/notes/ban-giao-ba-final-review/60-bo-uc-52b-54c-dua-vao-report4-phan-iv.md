# Note gửi BA — bỏ UC-52b, UC-54c, rà UC Specification và đưa phần hệ thống tự xử lý sang Report 4 phần IV (24/11/2026)

Mọi số UC, BR trong note này là **số mới** sau PR #519 (bảng ánh xạ UC đợt 1) và PR #522 (đổi mã BR trong code, docs, test). Số dòng là dòng trong bản trích (theo commit `d56b7960`, đã lệch sau PR #537: **tìm theo chữ, số dòng chỉ là gợi ý**) `docs/01-requirements/05-use-case-specifications.md` trên `app` hiện tại (chỉ đọc; sửa ở DOCX Report 3 rồi `sync_reports.py --report 3`).

## 1. Đã xong, không còn việc

- Tách BR-125 / BR-154 và chuyển vế “bản ghi gộp không tính vào giờ” sang BR-151: đã làm ở PR #520.
- UC-06b bước 3 ghi đủ Full Name, Email, System Role: đã làm trên `app`.
- Đánh số lại UC kèm bảng ánh xạ: đã làm ở PR #519 (`docs/01-requirements/10-bang-anh-xa-uc-dot-1.md`).
- Sequence diagram đổi sang mã UC/BR mới: đã làm ở PR #525 (không đổi lần nữa — đổi lại sẽ làm hỏng mã đã đúng).

## 2. Rà UC so với code trên `app` (còn mở)

Report 3 có 154 tiêu đề UC (81 UC cha và 73 UC con); 21 UC cha chỉ là nhóm, không có spec riêng, nên **133 UC có spec**.

- Có code, thiếu UC: cấu hình AI (`GET/PUT /api/admin/ai`, `POST /api/admin/ai/test`, màn `/mgmt/admin/ai`, `MSG408–413`): đề nghị UC mới ở FE-03, số trống tiếp theo là **UC-82**.
- Lịch sử trạng thái (`GET /api/meetings/{id}/history`): thêm AF vào **UC-15** Xem chi tiết cuộc họp, trích `BR-022`, `BR-023`.
- Thư ký chạy gỡ băng (`POST /api/recordings/{id}/transcribe`): thành AF của UC-54a, xem mục 3.2.
- Lệch cần sửa DOCX: V.2 bổ sung `MSG457–462` của **UC-50** Đăng ký phát biểu; 5.3 bổ sung `JOB-03 → JOB-08` cho các worker còn lại; API-06 viết lại từ “planned” sang máy local + API do Admin chọn.
- Hạn mức kho tệp là **250 GB** (PR #529, `FileStorageOptions.MaxTotalGigabytes`): UC-36a, UC-36b, UC-39 đang ghi 10 GB thì sửa theo.

## 3. Quy tắc viết UC và hai UC bị bỏ

### 3.1 Quy tắc chốt (thêm vào `docs/01-requirements/README.md` §2)

1. **Actor phải là người tương tác với hệ thống và đạt một mục tiêu.** Việc hệ thống tự xử lý (hàng đợi, tự lưu, tự xoá, thăm dò, thử lại, ngưỡng khớp giọng) và chi tiết kỹ thuật (tên dịch vụ, bảng, dòng dữ liệu, mã HTTP, tên trường API, địa chỉ IP, token, cờ cấu hình) **không phải UC**. Nghiên cứu từng chỗ, xoá khỏi UC Specification, chuyển sang **Report 4** (thiết kế do nhóm chọn): phần IV "Other Design Specifications", hoặc các mục I.1, I.2, I.3.
2. Trong UC chỉ giữ hành động của actor (nhập, chọn, xác nhận), phản hồi nhìn thấy được và điều kiện nghiệp vụ. Hành vi giao diện nhỏ (nút nào, tooltip, tab, phím tắt, trạng thái mờ, cuộn) thuộc BR hoặc mô tả màn hình, không ghi trong luồng.
3. Mỗi bước một ý, ngắn. Không giải thích cách hệ thống làm.
4. Ngoại lệ đã chốt: **UC-28 giữ nguyên** (tự lưu ghi chú khi ngừng gõ, giữ trên máy khi mất mạng, gắn ghi chú vào mục biên bản) — đây là cam kết người dùng nhìn thấy, không rút.

### 3.2 Hai UC cần bỏ

Sau khi đánh số lại, "UC-45c" bây giờ là **Manage Screen Sharing**, **không được xoá**.

| Cũ | Mới | Tên | Quyết định |
|---|---|---|---|
| UC-44b | **UC-52b** | Stop and Save Room Recording | Bỏ. Dừng ghi là hệ quả của việc tắt mic (UC-45a, UC-52a). Thiết kế "lưu lượt ghi" sang Report 4 phần IV. |
| hàng đợi hệ thống (Report 4 mục 10.2.4) | không có UC riêng | Process Transcription Queue (System) | Chỉ có ở Report 4: xoá mục 10.2.4 và sơ đồ, nội dung vào phần IV. |
| UC-45c cũ | **UC-54c** | Transcribe Meeting Recording | **Bỏ UC riêng.** Phần tự chạy (gỡ băng tự xếp hàng sau khi lưu lượt ghi, hàng đợi, ngưỡng) sang Report 4 phần IV. Phần Thư ký bấm chạy **bắt buộc** chuyển thành AF của **UC-54a**: `POST /api/recordings/{id}/transcribe`, chạy khi chưa có bản gỡ băng, hoặc chạy lại khi bản gỡ băng Failed/Completed; mã `MSG300`, `MSG301`, `MSG479` gắn vào AF đó. Không làm thì code có hành động của người dùng mà không UC nào mô tả. |
| UC-44d | **UC-52d** | Delete Recording Take | Giữ nguyên, một hành động của Thư ký. |

Tham chiếu chéo cần sửa sau khi bỏ:

- Tới UC-52b: mô tả UC-52a (dòng 1354), UC-52a AF-1 và AF-3, UC-54a.
- Tới UC-54c: UC-52c POST-1 (1382), UC-52d AF-1 (1397).
- MSG362, 363, 365, 295 đang nằm ở UC-52b: gắn lại vào UC-52a hoặc UC khác còn bắn mã đó; chạy `MessageCodeCatalogueTests`.

Thuật ngữ "lượt ghi": lượt còn **tự chia mỗi 30 phút**, cắt ở chỗ ngừng nói (`TAKE_SPLIT_SECONDS = 30 * 60` trong `frontend/src/features/recordings/takeRoller.ts`, ADR-0078), không chỉ là bật mic rồi tắt mic. UC-52a bước 5 đang ghi sai "5 phút" và ghi việc này trong luồng chính: đưa về thiết kế (phần IV), UC chỉ nói "bản ghi gồm các lượt". Giới hạn 300 MB nằm ở UC-52a AF-3 (1358): cũng chuyển sang thiết kế.

### 3.3 Những gì vẽ ra từ UC bị bỏ, xoá theo

- Report 4: 9.2.5 (UC-52b), 10.2.3 (UC-54c) và 10.2.4 (hàng đợi): tiêu đề, hình, chú thích; đánh lại số hình và mục. Nếu vẽ AF chạy gỡ băng của UC-54a thì thêm vào sơ đồ UC-54a.
- Report 3: danh sách UC, mô tả UC theo actor ở 4.3, spec, bảng quyền.
- Repo: `docs/06-sequencediagram/TrustCabinet_Sequence_Diagrams.drawio` (xem 3.5), `drawio-png/`, README của `meeting-recording/` và `transcription-voice-id/`, `00-master-use-case-inventory.md`, `docs/uc-map.html` (sinh lại cùng Report 3).
- Test: **bỏ** `docs/08-reports/test-reports/cases/preview/FE-09/unit-RoomTake.Complete.md` và `FE-10/unit-Transcription.ProcessNext.md` (chốt 25/11), gỡ mọi chỗ trỏ tới hai file.

### 3.4 Chỗ ghi thiết kế: Report 4 phần IV "Other Design Specifications"

Phần này cần có, không bỏ. Hiện chỉ có chữ mẫu. Viết mới từng thuật toán (mục đích, đầu vào, các bước, lỗi, BR liên quan):

- hàng đợi gỡ băng (lấy bản chờ cũ nhất và đánh dấu đang xử lý trong một bước; bản bị xoá hoặc có lượt mới thì bỏ kết quả; xong thì màn Bản ghi tải lại), ITN, độ tin cậy từng từ, lọc tiếng đệm, lọc câu ngắn;
- mang phần sửa tay sang lần gỡ băng lại: bốn ngưỡng 60% thời lượng nói, 2 giây, 500 ms, 80% (`VoiceShare = 0.6`, `MinVoiceOverlapMs = 2000`, `LineToleranceMs = 500`, `LineOverlap = 0.8` trong `TranscriptCarry.cs`, khớp theo thời gian chứ không theo giọng, BR-125, BR-154, ADR-0077);
- khớp giọng Voice ID và gán nhãn `SPEAKER_NN`: ngưỡng theo ADR-0074; bản ghi mic phòng dài theo ADR-0086 (nhóm dưới 2% thời lượng lời gộp về người gần nhất, chuyển chữ bằng Gipformer fp32), ADR-0086 **đang chờ PO duyệt**: viết vào phần IV, ghi rõ "đề xuất";
- lưu lượt ghi: cắt chẵn mili giây, tự chia 30 phút ở chỗ ngừng nói, server tự lưu lượt không nhận thêm tiếng trong 12 giờ (`RecordingOptions.IdleTakeHours`, `RoomTakeService.CompleteIdleAsync`), đầy 300 MB thì lượt mới thành bản ghi phòng mới (`RoomTakeService.cs:218`), giữ trên máy và gửi lại trong 7 ngày;
- bản ghi gộp: dựng sau khi họp xong, rút khoảng lặng trên 2 giây, dài tối đa 12 giờ (`CombinedRecording.MaxLength`, BR-151);
- tự xoá bản ghi sau 180 ngày; tự huỷ cuộc họp quá giờ (BR-029, ADR-0062); nhắc giờ họp 15 phút;
- đồng bộ danh sách thời gian thực; tự thử lại gửi thông báo; hỏi lại định kỳ (khách 10 giây, UC-43; UC-59);
- giãn cách lịch và giá trị mặc định khi tạo nhiều cuộc họp (UC-81a, BR-144…147);
- dây chuyền khi vô hiệu hoá tài khoản (BR-117, BR-142, BR-143).

Chỗ khác chỉ thêm một hai câu trỏ về phần IV:

- I.1 Software Architecture: "Background Workers", "Speech worker (in-house)" (BR-121).
- I.2.2, I.2.3: dòng Workers, Recordings, Transcription.
- I.3: 3.24 Recording (các lượt ghi), 3.25 Transcript (vòng đời Pending, Processing, Completed, Failed); kiểm tra trường đã có trong bảng chưa.
- III.1.3 Data Protection, III.2 Performance: hai phần này còn chữ mẫu của dự án khác ("TalentHub v2", "Spring Security", "shopeasy"), viết lại thật. Đưa "Known risk" của UC-35b (930) vào III.1.3.
- Report 4 không có Markdown snapshot: sửa thẳng DOCX `Report-4_Software Design Specification.docx`, chạy `check_docx.py`.

### 3.5 Sequence diagram (`TrustCabinet_Sequence_Diagrams.drawio`, 139 trang)

Đã đọc chữ của cả 139 trang. Sequence diagram là thiết kế (controller, service, repository), thuộc Report 4, nên **không** phải rút lifeline hay bước hệ thống theo quy tắc 3.1. Việc cần làm:

1. **Đã xong ở PR #525: số UC và mã BR đều là mã mới, không đổi lần nữa.** Đổi lại theo bảng ánh xạ sẽ làm hỏng mã đã đúng (số cũ và số mới chồng lên nhau: trang "UC-45a - View Recording Transcript" cũ là UC-54a mới, còn UC-45a mới là Manage Microphone).
2. **Đã xong ở PR #525: mã BR đã là mã mới (kể cả 4 trang mã cũ 3 chữ số không quá 154: UC-04, UC-21, UC-28c, UC-29).** Không đổi lần nữa theo bảng ánh xạ.
3. **Trang "UC-52b - Stop and Save Room Recording"** (tên cũ "UC-44b - …"): bỏ trang, nhưng code vẫn có lệnh lưu lượt khi tắt mic. Gộp luồng lưu (MSG362, MSG363, MSG365, MSG295; lượt rỗng; lượt đầu hoặc bản ghi đầy) vào cuối trang "UC-52a - Start Room Recording" (tên cũ "UC-44a - …"), sau vòng lặp "While the room microphone is on". Bỏ `ref` tới trang "UC-54c - Transcribe Meeting Recording".
4. **Trang "UC-54c - Transcribe Meeting Recording"** (tên cũ "UC-45c - …"): chuyển thành khối `alt`/`opt` AF trong trang "UC-54a - View Recording Transcript" (tên cũ "UC-45a - …"), giữ MSG295, 300, 301, 479. Các nhãn trong trang đó đang trỏ sang trang này thì chuyển sang trỏ AF mới.
5. **Trang hệ thống** (actor System) chuyển sang Report 4 phần IV làm hình cho thuật toán, không để trong bộ sequence diagram theo UC:
   - "UC-54c - Transcribe Meeting Recording: Process Transcription Queue (System)" (tên cũ "UC-45c - …"): hàng đợi gỡ băng;
   - "SYS - Build Combined Recording …" (quét mỗi 30 giây, giới hạn 12 giờ);
   - "SYS - Auto-End Forgotten Meeting" (quét mỗi 15 phút, quá 24 giờ). Đây là tự **kết thúc** cuộc họp đang chạy, khác tự **huỷ** cuộc họp chưa bắt đầu (BR-029); phần IV ghi cả hai. Hai trang SYS đang ghi "DRAFT awaiting approval": chốt luôn khi chuyển.
6. Trang "UC-45a - Record Speech While Microphone On (AF-2)" (tên cũ "UC-39a - …"): phần nhận dạng, lọc câu chỉ có tiếng ồn, giữ 180 ngày khớp mục 4.1 (UC-45a AF-2): giữ trang như hình thiết kế của AF, bỏ chú thích lưu trữ nếu UC đã rút.

Sau khi sửa: xuất lại `drawio-png/`, render kiểm tra theo `DRAWIO_EDITING_RULES.md`, cập nhật README của `meeting-recording/` và `transcription-voice-id/`.

## 4. Kết quả quét toàn bộ UC Specification

Trung vị một UC khoảng 2.650 ký tự. Mọi UC đều có actor là người, không có UC actor "System" riêng; vi phạm nằm **bên trong** UC.

### 4.1 Hệ thống tự làm, không ai bấm (bỏ khỏi UC, vào Report 4; giữ một câu hệ quả nếu người dùng nhìn thấy)

| UC | Chỗ | Việc |
|---|---|---|
| UC-54c | bước 4–5, AF-1 | Hàng đợi, ngưỡng khớp, tự gỡ băng sau khi lưu: phần IV (mục 3.2) |
| UC-52a | bước 5, AF-3 (1358) | Tự chia 30 phút, "Take N dividers", 300 MB: thiết kế |
| UC-52e | mô tả, AF-1 (1410) | "or the system when the 180-day period ends" và cả AF tự xoá sau 180 ngày: actor là hệ thống. Bỏ AF-1; UC chỉ còn Thư ký xoá |
| UC-51a | AF-3 (1315) | Dựng bản ghi gộp sau khi họp xong, rút khoảng lặng trên 2 giây, giới hạn 12 giờ |
| UC-45a | AF-2 (1190) | Giữ trên máy và gửi lại trong 7 ngày, tự xoá sau 180 ngày |
| UC-22 | mô tả, AF-2 (628) | Tự huỷ cuộc họp quá giờ (BR-029), "records System as the actor". Bỏ AF-2, ghi ở Report 4 (tác vụ chạy nền), giữ luật ở BR-029 |
| UC-14 | AF-1 bước 5 (509), AF-3, EX-3 (509–510) | Xếp thông báo, gạch giờ cũ trong thư; "cancelled by the system" |
| UC-19 | bước 6 | Nhắc trước giờ họp 15 phút; tự thử lại gửi thư; nhật ký gửi từng thư. UC chỉ giữ "người tham dự được mời và nhận thông báo" |
| UC-20, UC-22, UC-17, UC-19, UC-14 | bước thông báo | Gửi thông báo, thư, `.ics` (`METHOD:CANCEL`, `huy-hop.ics`): chỉ giữ "người liên quan được thông báo"; "thông báo lỗi thì không hoàn tác" thuộc BR |
| UC-41d | mô tả | "a later direct Meeting upload recreates it": thư mục Cuộc họp tự tạo lại là hành vi của UC-36a AF-1 bước 4 (946), UC-36b và UC-68a, không thuộc xoá thư mục |
| UC-06c | bước 7–10 | Dây chuyền của vô hiệu hoá: giữ một câu "Việc vô hiệu hoá chấm dứt Voice ID và các chia sẻ lịch của tài khoản (BR-117, BR-142, BR-143)" |
| UC-08 (377–379), UC-10 (409–410) | dây chuyền dừng chia sẻ lịch | Lặp lại lần thứ 4, 5 của cùng dây chuyền: thay bằng một câu trỏ BR-142, BR-143 |
| UC-26, 23a–d, 24, 25, 27 (750–751), 35a bước 3 và AF-3 (917), 80f AF-4 (2127), 81c AF-2 (2170) | tự làm mới | Đồng bộ thời gian thực là thiết kế; UC chỉ nói người xem thấy thay đổi |
| UC-43, UC-59 bước 5 (1691) | hỏi lại định kỳ | "asks the server again every 10 seconds", "asks again every few seconds": sang Report 4 |
| UC-30c | POST-2 (833) | "removed from the room within 30 seconds": thiết kế |
| UC-21 | tự xoá phiếu nháp | Luật nhìn thấy được: giữ một câu, chi tiết sang BR |
| UC-06c, 55b, 56a/b/c/f | "names filled in automatically" | Luật nhìn thấy được (BR-120, BR-123): giữ một câu, bỏ cơ chế |
| UC-45a | "Sentences with only silence … not kept" | Luật lọc: BR-113, bỏ khỏi luồng |

Không đụng: **UC-28** (763–765) giữ nguyên, xem 3.1 mục 4.

### 4.2 Cách làm bên trong chiếm gần hết luồng (UC của người, nhưng phần lớn là thiết kế)

| UC | Số bước hệ thống | Phần cần chuyển |
|---|---|---|
| UC-58 | bước 5–7 | Quy trình AI: cắt bản gỡ băng theo giờ từng mục, đổi số đọc thành chữ số, viết từng phần rồi ghép, bỏ phần thừa 300 ký tự, chép kết quả biểu quyết không qua mô hình. UC giữ: Chair/Secretary yêu cầu → nhận thông báo → đọc bản nháp |
| UC-70 | bước 2–4 | Dựng bản nháp từ mẫu cố định: đọc lại như UC-58 |
| UC-07 | 15/19 bước | **Không tách**, giữ một UC (đã chốt). Rút cơ chế liên kết (tạo yêu cầu, dùng một lần, liên kết cũ hết hiệu lực, kiểm tra hạn/đã dùng/bị thay) sang Report 4 phần bảo mật |
| UC-17 | bước 3, 6–10 | Tự tìm người đủ điều kiện, kiểm tra lại, gán, đổi trạng thái, ghi lịch sử "cùng lúc": giữ lựa chọn của Member, phần còn lại một câu |
| UC-54a | bước 4 (1443) | ITN, độ tin cậy từng từ, lọc tiếng đệm: phần IV |
| UC-55b | 1499–1501 | "names either a user or a Guest, never both", "SPEAKER_NN with the smallest unused number": phần IV |
| UC-81a | 2143 | Thuật toán giãn cách, làm tròn và mọi giá trị mặc định của form: BR-144…147 và phần IV |
| UC-64, UC-66 | bước 4–6 | Chốt danh sách bỏ phiếu, kiểm đếm, bảo mật phiếu kín: UC giữ "Chair mở/đóng phiếu, xem kết quả", cách kiểm đếm sang BR và Report 4 |

### 4.3 Chi tiết kỹ thuật (xoá hoặc viết bằng ngôn ngữ nghiệp vụ)

- UC-01 (235): "before calling the API; the backend still answers …".
- UC-05 (299): "over the full GET /api/users response; the server does not page yet".
- UC-16 (538–539): "kept in the URL (sortSet)".
- Mã HTTP và tên API, giữ MSG: UC-24 (710: 403, 409), UC-25 (725: 404), UC-47 (1256: "403", "endpoint", "guest token"), UC-34 (897), UC-53 (1424: "answered 404"), UC-06a/06b EX-1 ("after a 400/409 answer"), UC-29 EX-1, UC-32 EX-1.
- Tên trường API trong luồng: UC-42 (1144–1145) `lastPageReached`, `alreadyConfirmed`; UC-63 AF-3 (1764) `openImmediately`; UC-57b bước 3 (1637) `personAgreed`; UC-69 (1862) `ApprovedBy/ApprovedAt/IssuedAt`.
- UC-78 (2007–2008): "The client sends a mark-as-read request…".
- "Lưu cùng nhau / atomic", "competing requests": UC-18 (568–569), UC-19 (583), UC-24 (708), UC-33 (883–884), UC-72 POST-5, UC-73, UC-74. Giữ ý nghiệp vụ ("không lưu nửa chừng"), bỏ cơ chế.
- UC-43 (1157–1161): PRE-4 "enforced by the LiveKit room service, not the backend"; POST-4 "connection row", "audit row written before the room token is issued", "IP address"; "asks the room service for a connection", "within 20 seconds", "another tab of the same browser".
- UC-12 (471, 474): "IP address": nếu người dùng nhìn thấy thì ghi "địa chỉ truy cập", không nói nơi lưu.
- UC-04 "job title" nói "chức danh"; UC-58, 59 "the model" nói "mô hình AI" hoặc bỏ.
- UC-79a/79b: "query parameters", "parameter is not valid": nói theo điều người dùng chọn. Tên bảng báo cáo là từ vựng người dùng, giữ.
- UC-71 (1885): "63 columns", "three levels deep" của bảng in: sang BR hoặc thiết kế xuất file.
- UC-35b (930) "Known risk": sang Report 4 III.1.3.

### 4.4 Chi tiết giao diện trong luồng (bỏ; nếu cần giữ thì thành BR hoặc mô tả màn hình)

- UC-37a bước 1 (1010: tên nút, mờ nút, tooltip) và bước 4 (liệt kê "no upload, rename, move, delete, search"): một câu "Quản trị xem kho tài liệu cá nhân của tài khoản đã vô hiệu hoá ở chế độ chỉ đọc".
- UC-43 (1159–1160): "new browser tab", tab bị chặn, "Join room button".
- UC-42 (1144): nút "Xác nhận đã đọc", cuộn tới trang cuối. UC-35b (930): nút ✕, Esc, Back, Ctrl-click.
- UC-44 (1172–1174): nút quay lại, nội dung hộp thoại Stay/Leave, đóng tab; AF-2 (1173) cửa sổ nổi.
- UC-49 (1277–1282): cả UC là cửa sổ nổi của trình duyệt ("browser supports a floating document window"); **giữ là UC** (chốt 25/11), chỉ gọt chi tiết giao diện trong luồng.
- Phím tắt và chi tiết điều khiển: UC-45a bước 1 (1189) Ctrl+D, UC-45b bước 1 (1202) Ctrl+E, nút micro, camera; UC-48 (1267–1268) Enter/Shift+Enter, bộ đếm ký tự, huy hiệu chưa đọc; UC-45c: trình duyệt hỏi chọn màn hình; UC-45b MSG334, UC-45d: "lock icon beside the address bar".
- UC-50 bước 1 (1293): "Thêm (⋯) → Phát biểu" trên điện thoại.
- UC-71 (1888–1889): danh sách thanh công cụ, "64 levels".
- UC-11c, 13, 81b, 36b, 68a: "confirmation dialog", "meeting dialog", "document dialog".
- Tên **tab** trong luồng ("Recordings tab", "Biên bản tab") ở UC-15 AF-1, UC-51b PRE-1, UC-26, 32, 35a, 51a/c, 52c/d/e, 54a/b, 59, 69–75, 81a, 81c: nói "mở bản ghi/biên bản của cuộc họp", tên tab để ở mô tả màn hình.
- AF chỉ có giao diện: UC-15 AF-1 (524), UC-16 AF-5 (539), UC-46 (1241), UC-52a bước 1 (1357), UC-52c bước 6 (1383).
- UC-80f, 81c: chip, toast, thanh 00:00–24:00 "scrolled at 08:00", màu từng người; UC-80c: nút "Chia sẻ đã kết thúc (N)".
- UC-05 (299): "tooltip only when cut". UC-56a/d/e: "either tab", "from the sidebar".
- UC-55c bước 2 "tự chọn Lọt tiếng": hành vi giao diện, bỏ.

### 4.5 Lỗi trong spec (sửa luôn khi sửa DOCX)

- UC-39: POST-2 nói một tệp lỗi không chặn tệp khác, EX-1 lại nói "None of the submitted documents are added". Đã đối chiếu code (`frontend/src/features/documents/useFileUpload.ts`, 25/11): mỗi tệp gửi một yêu cầu riêng. **POST-2 đúng, sửa EX-1**:
  - tệp sai loại, rỗng hoặc quá 10 MB bị loại ngay lúc chọn và được nêu tên, các tệp hợp lệ vẫn tải: EX-1 viết "Các tệp không hợp lệ không được thêm và được nêu tên; các tệp hợp lệ vẫn được tải";
  - chọn quá 10 tệp, quá 100 MB, hoặc vượt chỗ còn lại của kho: cả lượt bị chặn, không tệp nào gửi. Câu "None… added" chỉ giữ cho ca này (EX-3 và một EX giới hạn lượt chọn);
  - kiểm lại phía server (`DocumentFileRules`, endpoint tải lên) trước khi viết; server khác thì theo server và ghi vào PR.
- Phần mở đầu FE-07 (904) ghi 200 MB và BR-315…319; các luồng ghi 100 MB. Phần mở đầu FE-11 (1722) còn trích BR-159, 160, 165, 252, 266, 267, 270 — các mã không còn sau khi gộp BR. Đổi theo số mới.
- UC-18 bước 7 dẫn tới EX-3, nhưng EX-3 là "lý do quá dài": sửa tham chiếu.
- UC-63 bước 2 (1763) lặp "(“Không ý kiến” in the app)" ba lần. UC-31 AF-1 (850) dính dòng ("submitted again1. At Step 4"). Câu MSG432 ở UC-75 bước 4 (1940) bị rối.
- UC-13 AF-1 (494) chép gần nguyên UC-81b bước 1–3: chỉ cần trỏ sang UC-81b.

### 4.6 Dài dòng

Rút gọn trước: UC-43 (7.372 ký tự, hơn 2,5 lần trung vị: tách vào phòng của thành viên và của khách), UC-07, UC-36b, UC-63, UC-36a, UC-04, UC-58, UC-68a, UC-16, UC-35b, UC-69. Bước dài hơn hai câu thì tách hoặc bỏ phần giải thích.

### 4.7 UC tốt, chỉ cần cắt cho gọn (kiểm tra khi sửa)

UC-13, UC-14, UC-16, UC-18, UC-21, UC-30a–c, UC-31, UC-33, UC-34, UC-36a–d, UC-38, UC-55c, UC-63, UC-65, UC-67, UC-69, UC-71–75, UC-80a–f, UC-81a–c: actor là người, mục tiêu rõ. Lỗi chủ yếu là các bước "The system verifies…" lặp lại điều kiện trước, mã HTTP, tên tab và nút (xem 4.3, 4.4).

### 4.8 Không có UC cho thứ này (đúng là không cần)

Tạo thông báo, gửi thư mời, nhắc giờ họp, tự huỷ cuộc họp, tự xoá bản ghi sau 180 ngày, gỡ băng chạy nền: hệ thống tự chạy, **không cần UC**, chỉ mô tả ở Report 4 (I.1 Background Workers và phần IV). Người dùng thấy kết quả qua UC-76, UC-78 (thông báo), UC-51a (bản ghi), UC-54a (chữ gỡ băng, kèm AF Thư ký chạy gỡ băng).

### 4.9 Chốt thêm cho nhóm ghi âm, gỡ băng (đối chiếu code 24/11/2026)

**UC-52a — đổi thành "Turn Room Microphone On/Off"** (bật là bắt đầu ghi, tắt là lưu lượt; phần tắt của UC-52b gộp vào đây):

- Luồng chính cho **họp trực tiếp**: Thư ký bật/tắt mic phòng trên thẻ ghi âm ở trang cuộc họp (`MeetingDetailPage.tsx`, `recordsRoom`).
- AF **họp kết hợp**: mic của cuộc gọi của Thư ký chính là mic phòng, **một trạng thái** (ADR-0072, ADR-0085, PR #534; `sendsRoomMic` trong `roomSupport.ts`); trỏ UC-45a. Phòng trực tuyến không có thẻ mic nổi và trang cuộc họp ở tab khác không còn thẻ "Đang ghi ở tab khác". **Chủ toạ tắt mic Thư ký là dừng ghi cả phòng** (ADR-0085, Hải chốt 25/11): ghi thành hệ quả nhìn thấy được trong AF này. Phòng trực tuyến không dùng được thì thẻ ghi âm trên trang lại là công tắc, như họp trực tiếp.
- Mic phòng đang bật thì thẻ mic theo mọi trang (PR #528, ADR-0083, `RoomMicProvider.tsx`), **trừ phòng họp trực tuyến** (ADR-0085): UC-52a ghi "thẻ mic hiện trên các trang khác khi mic đang bật", không ghi theo trang cuộc họp.
- Người khác trong họp kết hợp/trực tuyến ghi theo mic riêng: UC-45a AF-2, không thuộc UC-52a.
- AF "lượt bỏ dở ở máy khác": nút "Dừng ghi ở máy kia" trên thẻ ghi âm (`RecorderCard.tsx`, tooltip "Lưu phần máy kia đã ghi để chuyển chữ, rồi bật mic ở máy này", PR #527) — Thư ký dừng lượt đang mở dở từ máy khác để chuyển chữ, rồi bật mic ở máy này.
- Gắn lại MSG362, 363, 365 (lưu lượt, lượt rỗng) vào bước tắt mic của UC này.

**UC-52d — Delete Recording Take: giữ** (nút "Xoá lượt này" trên từng khối "Lượt N" ở bản gỡ băng, `TranscriptView.tsx:442`; `DELETE /api/recordings/{id}/takes/{n}`). Lượt = một lần bật rồi tắt mic phòng (hoặc đoạn máy tự cắt 30 phút); mọi lượt nối vào bản ghi phòng, đầy 300 MB thì mở bản ghi phòng mới. Gọt:

- AF-1 (bản gỡ băng đang chờ/chạy: cắt câu cũ, dời lượt sau, xếp hàng lại): sang Report 4 phần IV; UC giữ một câu "chữ đã sửa ở các lượt khác được giữ".
- AF-2 (giọng chỉ có trong lượt bị xoá mất tên): rút thành một câu ở POST, bỏ AF.
- "(UC-54c)" trỏ sang AF chạy gỡ băng của UC-54a; bỏ "On the Recordings tab".

**UC-54a — AF Thư ký chạy gỡ băng** có đủ ba trường hợp, đều có nút trên FE (`TranscriptView.tsx`): "Chuyển thành chữ" (chưa có bản gỡ băng), "Chạy lại" (Failed), "Chuyển thành chữ lại" có hộp xác nhận (Completed, giữ chữ đã sửa và tên đã gán ở chỗ còn khớp). Giữ cả ba từ UC-54c AF-2, AF-3.

**UC-52e — Erase Meeting Recording: giữ**, actor chỉ Thư ký (nút "Xoá bản ghi", `RecordingsPanel.tsx:484`; `DELETE /api/recordings/{id}`). Backend: `canErase = secretary && Status == Available`, không phân loại, nên xoá được **bản ghi phòng, tệp tải lên và bản gộp**; xoá bản ghi phòng thì bản gộp bị xoá theo (ADR-0067); xoá riêng bản gộp thì không dựng lại. Gọt:

- Bỏ vế "or the system when the 180-day period ends" ở mô tả, PRE-1, và bỏ AF-1 (AF thành `N/A`); cách tính 180 ngày sang Report 4 phần IV, luật giữ ở BR-110.
- Bước 1 sửa "on a room recording" thành "trên một bản ghi (bản ghi phòng, tệp tải lên hoặc bản gộp)"; thêm POST "xoá bản ghi phòng thì bản gộp của cuộc họp cũng bị xoá".
- Bước 4 bỏ "marks the recording as Erased with the date"; POST-2 bỏ "or that it expired"; POST-4 (không dựng lại bản gộp) giữ như hệ quả, bỏ cơ chế.
- Giữ POST-3 (lượt sau hỏi lại xác nhận thông báo), EX-1, EX-2; bỏ tên tab.

## 5. Cách thành viên làm (mỗi bước một commit)

1. Thêm quy tắc 3.1 vào `docs/01-requirements/README.md` §2.
2. Report 3 (DOCX, rồi `sync_reports.py --report 3`): bỏ UC-52b, UC-54c; đổi tên UC-52a thành "Turn Room Microphone On/Off" (cập nhật `10-bang-anh-xa-uc-dot-1.md`, `uc-map.html` và tên trang `.drawio`); thêm AF chạy gỡ băng vào UC-54a; sửa tham chiếu chéo (3.2); bỏ chi tiết tự chia lượt 30 phút khỏi UC-52a bước 5 (sang phần IV, UC chỉ nói "bản ghi gồm các lượt"); xử lý 4.1 đến 4.5 và 4.9 (đổi tên UC-52a, gọt UC-52d và UC-52e, AF ba trường hợp ở UC-54a) theo thứ tự UC; thêm AF lịch sử trạng thái vào UC-15; cập nhật bảng ánh xạ, BR, MSG.
3. Report 4 (DOCX): xoá 9.2.5, 10.2.3, 10.2.4; viết phần IV (3.4); thêm câu trỏ ở I.1, I.2, I.3; viết lại III.1, III.2.
4. Repo: sơ đồ `.drawio` theo 3.5 (số UC/BR đã xong ở PR #525, chỉ còn gộp và bỏ trang), README, `00-master-use-case-inventory.md`, `uc-map.html`, hai test case FE-09, FE-10.
5. Ghi chú vào ticket Jira của UC-52b, UC-54c, UC-52a (đổi tên) và UC-39 (skill `jira-deviation-note`; agent được ghi).

## 6. Giao cho agent (chốt 25/11/2026)

Mục này cho agent làm hết việc của note 60 và [note 61](61-dong-bo-style-uc-spec-report3.md) mà không phải hỏi lại. Gặp chỗ chưa chốt thì dừng, ghi vào PR, không tự đoán.

### 6.1 Đã chốt

1. Tên AF/EX **viết như câu** ("Invalid agenda item information"), theo note 61 §1.
2. **UC-49 giữ là UC**, chỉ gọt chi tiết giao diện trong luồng (§4.4).
3. **Bỏ** hai test case FE-09 `unit-RoomTake.Complete.md`, FE-10 `unit-Transcription.ProcessNext.md` (§3.3).
4. **UC-39**: POST-2 đúng, sửa EX-1 theo §4.5. Hạn mức kho 250 GB (§2).
5. **UC-52a**: họp kết hợp theo ADR-0085 (§4.9).
6. ADR-0086 (tách người nói mic phòng dài) viết vào phần IV, ghi "đề xuất, chờ PO duyệt" (§3.4).
7. **Sơ đồ UC tổng thể** (note 61 §4 comment 7): thêm một trang vào `docs/14-uc-diagram/trustcabinet-use-cases.drawio`; actor nối tới các gói chức năng FE-01…FE-13, không vẽ từng UC; actor ngoài như sơ đồ ngữ cảnh (OAuth Provider, AI drafting service, Resend Email). Ảnh đặt đầu Report 3 §4.3, caption "Figure n: System Overview Use Case Diagram".
8. Agent **được ghi comment Jira** (§5 bước 5).

### 6.2 Thứ tự (hai note cùng sửa một DOCX, làm tuần tự, mỗi bước một commit)

1. §5 bước 1: quy tắc 3.1 vào `docs/01-requirements/README.md` §2.
2. Note 61 §3 bước 1–2: UC-23a về mẫu, rồi `python scripts/uc_spec_style_report3.py --apply <out.docx>`; commit riêng, không đổi chữ.
3. §5 bước 2: chữ Report 3 theo thứ tự UC (§3.2, §4.1–4.5, §4.9, UC-15, UC-39).
4. Note 61 §3 bước 3: câu kết EX, "Main Flow" → "Normal Flow", trích MSG.
5. Note 61 §4: góp ý của thầy (caption mọi hình, link ảnh gốc hình dày, viết lại đoạn §3.1 ERD, bỏ MinutesSignature, quét "in the code" / "as built" / "as of <ngày>", sơ đồ tổng thể, hai chỗ Google AI Provider).
6. §5 bước 3: Report 4.
7. §5 bước 4: `.drawio` theo §3.5, PNG, README, inventory, bỏ hai test case.
8. Change Log: một dòng mỗi report, khoảng 200 ký tự.
9. §5 bước 5: Jira.

### 6.3 Quy tắc

- Không sửa tay snapshot (`docs/08-reports/Report-*.md`, `05-use-case-specifications.md`, `06-business-rules.md`): sửa DOCX ở `docs/08-reports/source/`, rồi `sync_reports.py --report 3`.
- Report 3 viết ngôn ngữ nghiệp vụ: không tên công nghệ/nhà cung cấp, endpoint, mã HTTP, tên trường API.
- Ảnh màn hình chỉ từ wireframe (`screens/*.png`). `.drawio` theo `DRAWIO_EDITING_RULES.md`, render rồi xem lại.
- Số dòng trong note là gợi ý; tìm theo chữ.
- Không push Drive.

### 6.4 Điều kiện xong

- `check_docx.py` OK cho Report 3 và Report 4.
- Snapshot Report 3 sau sync không còn "UC-52b", "UC-54c", "Google AI Provider", "drawn from", "MinutesSignature", "10 GB" (trừ Change Log).
- Mọi hình Report 3 có caption `Figure n:`; có sơ đồ UC tổng thể đầu §4.3.
- `uc_spec_style_report3.py` không tham số: không còn lệch.
- `MessageCodeCatalogueTests`, `SkillMirrorTests` qua; CI của PR xanh.
- Mỗi ticket ở §5 bước 5 có một comment.
