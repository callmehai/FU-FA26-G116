# Plan sửa UC, BR, message và diagram — BA Final Review 24/10/2026

**Phạm vi:** gom 6 note bàn giao (SCRUM-18/20, SCRUM-97, Voice ID, SCRUM-26, SCRUM-107, chương trình nghị sự) thành việc cụ thể trên
Report 3 và các diagram. **Không gồm** đặc tả chi tiết màn hình (Report 3 mục III và 5.1) — phần đó
ở [06-plan-dac-ta-man-hinh.md](06-plan-dac-ta-man-hinh.md).

**Trạng thái:** bản nháp để Khánh duyệt. Chưa sửa file nào.

## 0. Nguyên tắc làm

- **Report 3 DOCX là nguồn chuẩn** (`docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`).
  Agent viết file patch, Khánh dán vào DOCX → `python scripts/sync_reports.py` → đồng bộ sang
  `docs/01-requirements/05-use-case-specifications.md` và `06-business-rules.md` (chi tiết ở mục 8.2).
- **Thứ tự làm và cách chạy từng lượt ở [mục 8](#8-thứ-tự-và-cách-chạy-agent-đọc-mục-này-trước).**
- Nguồn đúng khi sửa là code + docs API, không phải ticket gốc: `docs/03-api/05-quan-ly-cuoc-hop.md`,
  `09-thanh-phan-tham-du.md`, `09-thanh-phan-tham-du-ban-giao.md`, `00-danh-muc-ma-thong-bao.md`, comment Jira.
- BR mới lấy từ **BR-320** (dải hiện tại kết thúc ở BR-319). Không đánh lại số, BR bỏ thì chuyển
  xuống "BR đã ngừng dùng".
- Mã thông báo **không tạo mới** cho SCRUM-18/20/26/97/107 — BE đã cấp hết. Chỉ Voice ID có thể cần mã
  mới, lấy từ `MSG290` và cập nhật "Mã tiếp theo" cùng commit.
- Sequence: sửa `.txt` → sửa tab trong `TrustCabinet_Sequence_Diagrams.drawio` → xuất PNG bằng
  `python scripts/sync_sequence_drawio_png.py`. Sửa `.drawio` theo `DRAWIO_EDITING_RULES.md`: sửa
  có chủ đích, giữ ID, render lại và xem ảnh.
- Mỗi việc ghi một dòng vào **Change Log** của Report 3 (ngày, M, người sửa, nội dung).

## 1. Bước chuẩn bị (trước khi sửa)

- [ ] (chuyển session Jira) Đọc comment Jira: SCRUM-20, SCRUM-18, SCRUM-97 (2 comment mới nhất), SCRUM-26 (comment 10255),
      mô tả SCRUM-31, SCRUM-35, SCRUM-102, SCRUM-103.
- [ ] (chuyển session Jira) Xem PR #111 (SCRUM-20), PR #123 (SCRUM-97), PR #128 (SCRUM-107, mục *Deviations from the docs*) để đối chiếu hành vi thật.
- [ ] (chuyển session Jira; thuộc lượt 7) Hỏi Hải: PR Voice ID (README + ADR mới) đã merge chưa, số ADR là bao nhiêu — mục 4 dẫn chiếu ADR đó.

## 2. SCRUM-18 / SCRUM-20 — lời mời, tạo/sửa, danh sách

### 2.1 UC Specification (Report 3 mục II)

**Trạng thái lượt 4:** patch viết xong ở [patch-scrum-18-20.md](note-may-nha/patch-scrum-18-20.md); **đã dán vào Report 3 DOCX ngày 24/10/2026** cho 2.1, 2.2 và 2.3 (V.2); `05-use-case-specifications.md` và `06-business-rules.md` chưa có phần lượt 4 vì `sync_reports.py` chỉ ghi `Report-3_...md`. Mặc định đã dùng: UC-30 giữ riêng, bộ lọc Đang diễn ra không thành BR; UC-15 không đổi; thêm BR-320 (thành phần khi tạo) và BR-321 (chưa trả lời = tham dự, từ chối chỉ ẩn khỏi lịch).

| UC | Report 3 | Sửa gì |
|---|---|---|
| UC-14 Create Meeting | II.4.1 | Bước 3: chọn thành phần tham dự ngay trong form. Form là popup, không còn trang riêng. POST-3 ("may subsequently be assigned participants") → thành phần được lưu cùng lúc tạo. Thêm EX cho thành phần không hợp lệ (đối chiếu mã BE dùng khi tạo kèm roster, `MSG277` theo PR #92). |
| UC-15 Update Meeting | II.4.2 | Sửa ở popup (chỉ nếu UC-15 bước 1–2 mô tả trang riêng). Kiểm tra lại có cho sửa thành phần trong popup không. |
| UC-17 View Meeting List | II.4.4 | Bước 1–3: mở sẵn bộ lọc **Đang diễn ra** (`status=InProgress`). AF-2: bộ lọc thời gian chỉ còn Hôm nay / Tuần này / Chưa diễn ra / Đã diễn ra, bỏ khoảng ngày; "Chưa/Đã diễn ra" so với giờ bắt đầu dự kiến (cuộc họp đang diễn ra tính là "Đã diễn ra"). Vẫn hiện cuộc họp mình đã từ chối. |
| UC-28 / UC-28a | II.6.2.1 | Thêm ghi chú: khi tạo cuộc họp, thành phần được chọn trong form UC-14 (UC-28a dùng cho thêm sau). POST-2 "New invitations start with Pending" giữ nguyên. |
| UC-29 Respond to Invitation | II.6.3 | Viết lại: trả lời là xác nhận trước, **một lần**. Primary Actors bỏ **Chair**. Bỏ AF-1 (đổi câu trả lời). Thêm: gửi lại đúng câu đã ghi → MSG154; gửi câu khác → EX MSG285; Chủ tọa trả lời → EX MSG286. Điểm vào: đầu màn chi tiết và thẻ cuộc họp trên lịch, không phải tab Thành phần. POST: từ chối chỉ ẩn cuộc họp khỏi lịch của người đó, không đổi quyền xem/vào phòng. Chưa trả lời coi như tham dự. |
| UC-30 View Response Status | II.6.4 | Bỏ "totals by status" ở Description, POST-1 và bước 3. Phản hồi hiện thành nhãn cạnh tên trong tab Thành phần; Chủ tọa không có nhãn. Cân nhắc gộp UC-30 vào UC-27 — **hỏi PO trước**, mặc định giữ UC-30 và chỉ bỏ bộ đếm. |

### 2.2 Business Rules (Report 3 V.1 + `06-business-rules.md`)

- [x] **BR-30**: thay "overlap khoảng ngày" bằng 4 lựa chọn sẵn; nêu rõ quy tắc Chưa/Đã diễn ra theo giờ bắt đầu dự kiến và Hôm nay/Tuần này theo múi giờ người xem.
- [x] **BR-106**: "may be changed while Published/InProgress" → mỗi lời mời trả lời một lần, không đổi được. Gửi lại đúng câu đã ghi không phải lỗi.
- [x] **BR-105**: thêm "Chair does not respond" hoặc tách BR mới.
- [x] **BR mới (BR-320…)**: Chủ tọa không trả lời lời mời; chưa trả lời coi như tham dự; từ chối chỉ ẩn khỏi lịch của chính người đó, không đổi quyền; danh sách cuộc họp mở sẵn bộ lọc Đang diễn ra (nếu nhóm coi đây là rule chứ không phải UI — mặc định ghi trong UC-17 và đặc tả màn hình, không thành BR).
- [x] **BR-115**, **BR-176**: bỏ phần "totals" nếu có nhắc.
- [x] Rà mục I.3.3 Data Business Rules và mục I.3.2 entity *InvitationResponse*. → Không có chữ "latest/may change"; đã thêm "trả lời một lần, Chủ tọa giữ Pending" vào InvitationResponse/RespondedAt (participant, guest) và một Data Business Rule mới (24/10/2026, [patch-sot-luot-1-4](note-may-nha/patch-sot-luot-1-4.md)).

### 2.3 Message (Report 3 V.2)

- [x] (đã dán DOCX 24/10/2026) Thêm **MSG285**, **MSG286** (nguồn: catalogue dòng 65–66).
- [x] (đã dán DOCX 24/10/2026) **MSG154**: mở rộng tình huống "gửi lại đúng câu trả lời đã ghi" (UC-29), không chỉ UC-28b.
- [x] (đã dán DOCX 24/10/2026) Đối chiếu MSG105–MSG108 với UC-29 mới.
- [x] Bảng tiếng Anh trong catalogue (dòng 341–344): thêm dòng MSG285/MSG286 cho truy vết (thêm cả MSG277 vì UC-14 EX-3 dẫn tới).

### 2.4 Diagram

- [x] Sequence `UC29_RespondToMeetingInvitation` (.txt, tab drawio, PNG): thêm nhánh MSG285, MSG286, MSG154; bỏ nhánh đổi câu trả lời.
- [x] Sequence `UC30_ViewParticipantResponseStatus`: bỏ bước tính totals.
- [x] Sequence `UC17_ViewMeetingList`: tham số `status=InProgress` mặc định, `timing=upcoming|past`, `from/to` cho Hôm nay/Tuần này, `excludeDeclined` (lịch).
- [x] Sequence `UC14_CreateMeeting`: gửi kèm thành phần trong cùng request (đối chiếu API 05).
- [x] **State machine `SM-04_InvitationResponse`**: bỏ 2 chuyển "Invitee changes to…"; ghi chú Chair giữ Pending. Xuất lại PNG nếu Report 3 dùng ảnh. → Đã bỏ 2 chuyển, thêm ghi chú; PNG xuất lại.
- [x] **Use case diagram** (`docs/assets/use-case-diagrams/trustcabinet-use-cases.drawio`, trang Chair → `uc-chair.png`): bỏ liên kết Chair — UC-29. Report 3 I.4.3.4 thay ảnh. → Đã bỏ UC-29 khỏi trang Chair, `uc-chair.png` xuất lại; Khánh chèn vào Report 3.
- [x] Permission Matrix I.4.4: dòng UC-29 bỏ Chair (ô Chair = No, chú thích ¹⁰, mô tả actor Chair, email mời). Ảnh use case Chair và BF-02 đã thay trong DOCX 24/10/2026.
- [x] Class diagram `feature-06-…drawio`: kiểm tra có thuộc tính/phương thức đổi phản hồi hoặc totals không. `feature-04-…drawio`: tạo cuộc họp kèm thành phần, bộ lọc thời gian. → Đã kiểm: feature-06 không có đổi phản hồi/totals; feature-04 dùng `createMeeting(meeting)` và `criteria: Map`, đủ chứa thành phần và bộ lọc, không sửa.
- [x] Business flow `BF-02_Meeting_Preparation_Approval.drawio`: bước "chọn thành phần" nằm trong tạo cuộc họp; bước phản hồi lời mời một lần. → Sửa nhãn, giữ bố cục: bước tạo kèm participants, "Reschedule Meeting (time, location, link)", trả lời "(once)"; PNG xuất lại.

## 3. SCRUM-97 — UC-73 giao Chủ tọa

### 3.1 UC Specification

| UC | Report 3 | Sửa gì |
|---|---|---|
| UC-73 | II.6.6 | Bước 1–2: Chủ tọa bấm **Đổi vai trò** trên dòng người muốn giao → chọn "Chủ tọa" (radio, mỗi vai trò có dòng mô tả quyền) → bấm **Giao Chủ tọa**. Bỏ bước "hệ thống hiện danh sách người nhận". **AF-1 (MSG258)**: không hiện thông báo; nếu không ai đủ điều kiện thì không có lựa chọn "Chủ tọa" — viết lại AF-1 hoặc bỏ, đổi PRE-3 tương ứng. EX-1, EX-2, EX-3 giữ. |
| UC-28b | II.6.2.2 | Description, AF-1 và EX-5: thêm ngoại lệ — từ khi công bố, **Thư ký chỉ thay Chủ tọa khi tài khoản Chủ tọa bị khoá**. Trước công bố đổi tự do. Ghi rõ **không** hoán đổi Chủ tọa ↔ Thư ký. Đổi Thư ký = cho người mới làm Thư ký rồi đổi người cũ về Thành viên. |

### 3.2 Business Rules

- [x] **BR-126**: thêm ngoại lệ "the Secretary may replace the Chair when the Chair's account is locked".
- [x] **BR-295**: đối chiếu — người nhận không phải Thư ký hiện tại (giữ). Thêm câu "Chair and Secretary do not swap roles" vào BR-295 hoặc BR mới.
- [x] **BR-294**: kiểm tra nội dung "before publication" khớp "đổi vai trò tự do".

### 3.3 Message

- [x] **MSG258**: catalogue (dòng 116, 596) và Report 3 V.2 (dòng 239) — ghi "BE vẫn trả 200/MSG258 làm empty state; FE không hiển thị, ẩn lựa chọn Chủ tọa". Không xoá mã.
- [x] **MSG256**: Report 3 V.2 vẫn là câu cũ; câu tiếng Việt trong catalogue đã nói "khi tài khoản hiện tại còn hoạt động" — đồng bộ câu tiếng Anh theo ngoại lệ tài khoản bị khoá.

### 3.4 Diagram

- [x] Sequence `UC73_HandOverChair` (.txt, tab drawio, PNG): luồng từ dòng người nhận; bỏ nhánh MSG258 hiển thị.
- [x] Sequence `UC28b_AssignChangeMeetingRole`: nhánh Thư ký thay Chủ tọa bị khoá (`PUT assignments` kèm `chairReplacement`).
- [x] Class diagram `feature-06`: kiểm tra phương thức `chairReplacement` / chair-handover. Đã kiểm: `handOverChair` có ở controller, service, repository; thay Chủ tọa bị khoá dùng `manageMeetingRoleAndPermissions` sẵn có, không cần sửa class diagram.

## 4. Voice ID và tách người nói — SCRUM-31, 35, 102, 103

> Thay đổi phạm vi: trước đây ghi "không làm voiceprint". Phụ thuộc PR của Hải (README + ADR mới) — làm sau khi PR đó merge.

### 4.1 UC Specification

- [ ] Viết đặc tả đầy đủ **UC-43 → UC-51** (Report 3 II.9–II.10 hiện chỉ có tiêu đề) theo mẫu bảng UC hiện tại, dựa trên mô tả SCRUM-31, SCRUM-35, SCRUM-102, SCRUM-103.
- [ ] Nội dung bắt buộc phải có trong UC (từ note):
  - đăng ký giọng mẫu tự nguyện, **đồng ý riêng** không gộp với đồng ý ghi âm;
  - chỉ lưu vector đã mã hoá; **rút đồng ý là xoá ngay**;
  - hệ thống chỉ **gợi ý** tên người nói, Thư ký xác nhận, không tự gán (UC-46);
  - họp online: mỗi trình duyệt ghi micro của chính người dùng nên biết chắc người nói;
  - model chạy trên server nhóm, giọng nói không gửi ra ngoài.
- [ ] Quyết định với PO: đăng ký/rút giọng mẫu là UC mới (ví dụ UC-78, UC-79) hay nằm trong UC-43–51. Mặc định: **UC mới**, vì actor là mọi người dùng, không phải Thư ký. Cập nhật I.4.2 UC list, Master UC Inventory, Permission Matrix.

### 4.2 Business Rules và message

- [ ] BR mới (BR-32x): đồng ý Voice ID riêng; vector mã hoá; rút đồng ý xoá ngay; chỉ gợi ý; không gửi giọng ra ngoài; thời hạn lưu.
- [ ] Message cho đăng ký/rút đồng ý/lỗi đăng ký: lấy từ MSG290 nếu BE đã cấp; nếu chưa, ghi tạm "TBD" và hỏi BE, không tự cấp mã.

### 4.3 ERD và dữ liệu

- [ ] `docs/02-design/02-erd.md` §7: thêm bảng đồng ý Voice ID và giọng mẫu (tên, khoá, quan hệ User, thời điểm đồng ý/rút, vector mã hoá).
- [ ] `02-erd.md` dòng 655 ("Ranh giới đã chốt" — Voiceprint không có bảng): bỏ hoặc sửa thành phạm vi mới.
- [ ] ERD drawio + PNG: `docs/assets/erd-muc-tieu/trustcabinet-erd-muc-tieu .drawio`, `docs/assets/erd-chen/…`. Report 3 I.3.1 thay ảnh.
- [ ] Report 3 I.3.2: dòng 483 "No voiceprint is stored" của *TranscriptSegment* sửa lại; thêm mô tả 2 entity mới. I.3.3 thêm data rule.
- [ ] Báo Hải: `AGENTS.md` và README còn ghi voiceprint ngoài phạm vi (nếu PR chưa sửa).

### 4.4 Căn cứ pháp lý (Luật 91/2025/QH15 + NĐ 356/2025/NĐ-CP thay NĐ 13/2023)

- [ ] Report 3 IV.2.3 Security (dòng ~3765) và IV.2.8 Compliance (dòng ~3808).
- [ ] `02-erd.md` dòng 655 và mọi chỗ khác: `grep -rn "13/2023" docs/` rồi sửa từng chỗ.
- [ ] Màn tải bản ghi (Report 3 III.9.2, câu đồng ý ghi âm) — thuộc plan màn hình.

### 4.5 Diagram

- [ ] Sequence mới cho UC-43–51 và UC đăng ký giọng mẫu (thư mục mới `docs/06-sequencediagram/recording-transcript/` + README, tab drawio, PNG).
- [ ] Context diagram `docs/11-contextdiagram/trustcabinet-context.drawio` + Report 3 I.1: "AI / Speech Processing Provider" ngoài hệ thống → model tự chạy trên server nhóm. Report 3 I.5.2 dòng 6 và III.17.6 (API-06 "provider not yet decided") sửa tương ứng.
- [ ] State machine `SM-11_Transcript` (gợi ý người nói → Thư ký xác nhận) và có thể SM mới cho đồng ý Voice ID.
- [ ] Use case diagram: thêm UC đăng ký/rút giọng mẫu cho các actor.
- [ ] BF-04 Post-Meeting: bước gợi ý và xác nhận người nói.

## 5. SCRUM-26 — điểm danh

### 5.1 UC Specification

| UC | Report 3 | Sửa gì |
|---|---|---|
| UC-31 | II.6.5 | Bước 1–2: điểm danh là một cột trong bảng thành phần ở màn chi tiết; trong phòng họp dùng panel. Không có bộ đếm có mặt/vắng. EX-4 (409 MSG151): không giữ bản nháp — FE báo lỗi, tải lại, Thư ký chọn lại. Ghi version riêng cho điểm danh theo **ADR-0025** (MSG151 gồm xung đột theo entry, không dùng revision roster). EX-5 MSG150: bỏ "No changes were saved", dùng câu trung tính. Thêm EX lỗi đọc MSG289. |

### 5.2 Business Rules

- [x] **BR-172**: thêm cơ chế phát hiện xung đột theo version điểm danh (ADR-0025); bỏ câu nào hứa giữ bản nháp nếu có.

### 5.3 Message

- [x] **MSG150**: Report 3 V.2 (dòng 4219) và bảng tiếng Anh catalogue (dòng 351) → câu trung tính, khớp câu tiếng Việt "Không xác nhận được kết quả lưu điểm danh…".
- [x] **MSG151**: câu tiếng Anh khớp câu tiếng Việt mới ("Thành phần hoặc dữ liệu điểm danh đã thay đổi…").
- [x] Thêm **MSG289** (lỗi đọc điểm danh) vào Report 3 V.2; kiểm tra MSG278 (query không hợp lệ).

### 5.4 Diagram

- [x] PNG `uc31_recordattendance.drawio.png` đã xuất 24/10/2026 (draw.io `--scale 1.5`, 4972×4184; nhãn MSG150/MSG151 nằm trong khung).
- [x] Sequence `UC31_RecordAttendance` (.txt, tab drawio và PNG xong; thêm khối GET attendance với nhánh 500 MSG289): GET attendance có version, PUT gửi version, 409 MSG151 → tải lại; 503 MSG150; 500 MSG289.
- [x] State machine `SM-06_Attendance`: kiểm tra có trạng thái "nháp" không.
- [x] Dẫn chiếu ADR: `grep -rn "ADR-0024" docs/` — chỗ nào nói điểm danh thì đổi thành ADR-0025.

## 6. SCRUM-107 — tải tài liệu từ máy lên cuộc họp (UC-33a AF-1)

Nguồn: `docs/03-api/11-tai-lieu-cuoc-hop.md`, ADR-0024, mô tả PR #128. Không có mã mới.
Mười điểm lệch trong note 07 chia làm ba loại: sửa đặc tả, chỉ ghi nhận, không phải việc của BA.

### 6.1 UC Specification

| UC | Report 3 | Sửa gì (điểm trong note 07) |
|---|---|---|
| UC-33a | II.7.2.1 | **AF-1 đảo bước 2 và 3** (điểm 1): kiểm quyền, quyền xem cuộc họp và trạng thái trước, rồi mới kiểm số tệp, dung lượng, loại tệp. EX-1, EX-6, EX-7 đổi điểm vào thành "Step 2 of AF-1"; EX-3 và EX-8 thành "Step 3 of AF-1". Thêm câu: Thành viên gửi tệp không hợp lệ nhận MSG162, không phải MSG164. |
| UC-33a | EX-3 | Thêm các trường hợp (điểm 5, 9): request không có tệp, không phải multipart hoặc multipart hỏng → MSG164; **đuôi phải là `.pdf` hoặc `.docx` và khớp nội dung**; PDF chỉ nhận khi `%PDF-` ở đầu tệp. |
| UC-33a | EX-7 | Nêu rõ (điểm 4, 10): cuộc họp bị giấu và cuộc họp không tồn tại nhận cùng một MSG174; người từng là Chủ tọa/Thư ký mất vai trò và không còn xem được cuộc họp nhận MSG174, không phải MSG162. |
| UC-33a | EX-8 | Nêu rõ (điểm 3): MSG288 dùng cho cả "> 10 tệp" và "> 200 MB", kể cả khi kích thước khai báo vượt giới hạn — từ chối trước khi nhận hết tệp. |
**Trạng thái lượt 2:** đã viết patch ([patch-scrum-107.md](note-may-nha/patch-scrum-107.md)) và sửa `05-use-case-specifications.md` cho UC-33a (AF-1, EX-1, EX-3, EX-6, EX-7, EX-8). Đã dán vào Report 3 DOCX và chạy `sync_reports.py --report 3` ngày 24/10/2026 (12 dòng UC/BR/V.2 + MSG288 loại hiển thị); PageCount của StoredFile (I.3.2) và Change Log chưa dán (ngoài phạm vi 4 mục).


### 6.2 Business Rules

- [x] **BR-187** hoặc **BR-215** (điểm 9): thêm "the file extension must be .pdf or .docx and match the detected content". Mặc định sửa BR-215 vì BR đó nói về nhận diện theo nội dung. → Đã sửa BR-215 (mặc định); BR-187 giữ nguyên.
- [x] Cân nhắc BR mới (BR-32x) cho thứ tự kiểm "authority before file validation" (điểm 1) — **hỏi PO**; mặc định chỉ ghi trong UC-33a và sequence, không thành BR. → Dùng mặc định: không tạo BR, chỉ ghi ở UC-33a AF-1 và sequence.
- [x] **BR-319**: không đổi nội dung; đối chiếu câu với EX-8 mới.

### 6.3 Message (Report 3 V.2)

- [x] **MSG288** (dòng 256): đổi cột trạng thái từ "Dành sẵn, chưa triển khai" thành loại hiển thị thật (Form error), vì SCRUM-107 đã triển khai. → Patch: cột loại "Form error". Catalogue đã ghi "Đã triển khai SCRUM-107".
- [x] **MSG164**: thêm tình huống request không có tệp / không phải multipart / đuôi không khớp nội dung. → Patch: sửa cột Where, câu giữ nguyên.
- [x] **MSG174**: thêm tình huống người mất vai trò không còn xem được cuộc họp. → Patch: sửa cột Where, câu giữ nguyên.

### 6.4 Dữ liệu

- [x] Report 3 I.3.2 entity *Document* (dòng ~404, `PageCount`) (điểm 6): "PageCount do trình duyệt gửi, chỉ lưu cho PDF khi là số nguyên dương; không rõ thì để trống, không từ chối tải lên". → Trường nằm ở entity 19 *StoredFile*, không phải Document; patch ghi đúng chỗ.

### 6.5 Diagram

- [x] Sequence `docs/06-sequencediagram/document-management/UC33a_UploadDocumentsToMeeting` (.txt, tab drawio, PNG): đưa khối kiểm quyền/trạng thái lên trước khối kiểm tệp; thêm nhánh MSG164 (multipart hỏng, đuôi không khớp), MSG288 (chặn theo kích thước khai báo), MSG174 (mất vai trò); kho lưu ghi là "Object storage (R2)" — đối chiếu tên participant với ADR-0024.
- [x] Class diagram `feature-07-document-management-class-diagram.drawio`: kiểm tra `pageCount` nullable và lớp lưu trữ đối tượng (R2/S3) có khớp không. → Đã kiểm: `pageCount: int?` và `FileStorage` khớp, không cần sửa.

### 6.6 Chỉ ghi nhận, không sửa đặc tả

- **Điểm 7** — lập nghị sự vẫn "mỗi tài liệu một mục", trong khi Report 3 I.3.2 (dòng ~427) và ADR-0020 cho phép nhiều mục. Đây là lệch của UC-33b (SCRUM-58), chưa làm. **Không sửa spec**; ghi vào Jira SCRUM-58 để khi làm UC-33b thì theo spec.
- **Điểm 2** — kho dev là SeaweedFS: đã sửa trong ADR-0024 và `03-co-so-du-lieu.md`. Report 3 không nhắc MinIO → không phải sửa.
- **Điểm 8** — fixture dev mất object trên kho: vấn đề dữ liệu dev/server test, không phải tài liệu yêu cầu. Báo leader/BE, không đưa vào Report 3.

## 6b. Chương trình nghị sự — SCRUM-98, UC-23, UC-77 (note 08)

> **Kiểm trước khi làm:** ngày 24/10/2026 code trên branch này vẫn kiểm `PlannedDurationMinutes > 0`
> (`backend/TrustCabinet.Domain/Agenda/AgendaRules.cs`), còn `PresenterName` tự do, và chưa có
> MSG271–MSG274. Tìm PR của SCRUM-98 / UC-77; chỉ sửa docs theo hành vi **đã merge vào `main`**.
> Nhiều BR đã được viết theo hướng mới từ review 18/10 (BR-66, BR-68, BR-70, BR-237, BR-238,
> BR-306 → BR-312) — việc chính là dọn chỗ còn sót, không viết lại.

### 6b.1 UC Specification

| UC | Report 3 | Sửa gì |
|---|---|---|
| UC-23a Create Agenda Item | II.5.1.1 | Bỏ Thời lượng khỏi bước nhập. Người trình bày: chọn thành viên nội bộ đang tham gia hoặc khách có lời mời còn hiệu lực, không nhập tên tự do. Thông báo người trình bày khi Published/InProgress (BR-307). |
| UC-23b Update Agenda Item | II.5.1.2 | Sửa được trường lập kế hoạch ở mọi trạng thái của item (BR-68). Tài liệu: sửa một item chỉ đổi liên kết của item đó; một tài liệu gắn được nhiều item. |
| UC-23c Delete | II.5.1.3 | Đối chiếu: chỉ item Pending được xoá (BR-237). |
| UC-23d Reorder | II.5.1.4 | Sắp xếp được mọi item, kể cả Done (BR-238) — **chờ PO trả lời câu hỏi còn ghi trong SCRUM-98**. |
| UC-24 Set Current | II.5.2 | Chủ toạ bắt đầu một item Pending bất kỳ; tối đa một item InProgress (BR-70, BR-71). |
| UC-25 Complete | II.5.3 | Ghi giờ bắt đầu/kết thúc thực tế, không tính thời lượng; bỏ "regardless of planned duration". Chỉ hoàn thành item InProgress — **chờ PO** câu "hoàn thành mục lúc nào cũng được". |
| UC-26 View Agenda | II.5.4 | Bỏ tổng thời lượng so với khung giờ cuộc họp. |
| UC-77 Record Conclusion | II.5.5 | Đối chiếu BR-309 → BR-311 với code khi merge; "không cho sửa sau khi biên bản đã tồn tại" khớp BR-310. |
| UC-28c | II.6.2.3 | POST-4 / BR-308 đã có: gỡ người hoặc thu hồi lời mời thì xoá phân công người trình bày — chỉ đối chiếu. |

### 6b.2 Business Rules

- [ ] Rà mọi BR còn nhắc *planned duration*, *planned time slot*, *PresenterName*: `grep -n -i "duration\|planned time" docs/01-requirements/06-business-rules.md`.
- [ ] Tài liệu nhiều mục: đối chiếu BR-279, BR-280 và ADR-0020; **mâu thuẫn với note 07 điểm 7** (PR #128 ghi lập nghị sự vẫn "mỗi tài liệu một mục"). Kiểm code `MeetingDocumentAgendaItem` sau khi PR SCRUM-98 merge rồi mới chốt câu.
- [ ] BR-306/BR-307: note 08 yêu cầu BA xác nhận người trình bày là quy tắc nghiệp vụ — BR đã có, chỉ ghi xác nhận vào Jira.

### 6b.3 Message

- [ ] MSG271 → MSG274 (Report 3 V.2 dòng 252–255, catalogue dòng 609–612): đổi "Dành sẵn, chưa triển khai" khi UC-77 đã merge.
- [ ] Lỗi validate thời lượng (nếu có mã/câu riêng trong V.2 hoặc catalogue) → đánh dấu ngừng dùng, không xoá mã.

### 6b.4 Dữ liệu và diagram

- [ ] Report 3 I.3.2 entity *AgendaItem* (dòng ~252–254): bỏ `PlannedDurationMinutes`, bỏ `PresenterName` (hoặc ghi là chỉ giữ lịch sử, theo migration thật); thêm khách làm người trình bày nếu có cột riêng.
- [ ] `docs/02-design/02-erd.md` (dòng ~200–202) và ERD drawio + PNG (`docs/assets/erd-muc-tieu/`, `docs/assets/erd-chen/`).
- [ ] Sequence `docs/06-sequencediagram/agenda-management/`: UC23a, UC23b, UC23d, UC24, UC25, UC26, UC77 — bỏ thời lượng, người trình bày chọn từ danh sách, liên kết tài liệu theo item.
- [ ] State machine `SM-02_Agenda`: chuyển Pending → InProgress từ item bất kỳ, Done vẫn sắp xếp được.
- [ ] Class diagram `feature-05-agenda-management-class-diagram.drawio`: bỏ thuộc tính thời lượng, `PresenterName`.

## 7. Việc chung khi xong

- [x] Rà Report 3 V.2 còn thiếu các mã đã có trong catalogue → thêm 31 dòng (#261–#291), đánh dấu MSG152/MSG153 ngừng dùng, sửa dòng phiên hết hạn từ MSG140 sang MSG01. MSG271–274 thuộc lượt 5, MSG141/MSG144 giữ "Retired" theo đặc tả, xem [patch-sot-luot-1-4](note-may-nha/patch-sot-luot-1-4.md).
- [x] `docs/01-requirements/00-master-use-case-inventory.md`: actor UC-29 bỏ Chair (UC mới Voice ID thuộc lượt 7, chưa làm).
- [x] Cập nhật số BR trong đầu `06-business-rules.md` (267 → 269 sau BR-320, BR-321).
- [x] Change Log Report 3: thêm 5 dòng 24/10/2026 (SCRUM-107, 97, 18/20, màn hình v8, rà soát tổng); dòng SCRUM-26 đã có từ lượt 1.
- [ ] Comment Jira từng ticket: đã cập nhật docs gì; chuyển ticket khỏi BA Final Review. → chuyển session Jira, không làm ở đây.
- [ ] Note này: đánh dấu xong từng mục; README thư mục cập nhật trạng thái.

## 8. Thứ tự và cách chạy (agent đọc mục này trước)

### 8.1 Thứ tự — làm lần lượt, không song song

| Lượt | Việc | Plan | Model gợi ý | Điều kiện bắt đầu |
|---|---|---|---|---|
| 1 | SCRUM-26 | 05 mục 5 | Sonnet | Ngay |
| 2 | SCRUM-107 | 05 mục 6 | Sonnet | Lượt 1 đã commit, Khánh đã dán patch vào DOCX |
| 3 | SCRUM-97 | 05 mục 3 | Sonnet | Lượt 2 xong như trên |
| 4 | SCRUM-18/20 | 05 mục 2 | Sonnet | Lượt 3 xong như trên |
| 5 | Chương trình nghị sự (SCRUM-98, UC-23, UC-77) | 05 mục 6b | Sonnet | Lượt 4 xong **và** PR SCRUM-98 / UC-77 đã merge vào `main` (24/10/2026 code còn `PlannedDurationMinutes`, `PresenterName`, chưa có MSG271–274) |
| 6 | Chụp và đặc tả màn hình | [06](06-plan-dac-ta-man-hinh.md) | Sonnet + skill `verify-ui-in-browser` | Lượt 1–5 xong (đặc tả màn dẫn số UC/MSG đã chốt) |
| 7 | Voice ID | 05 mục 4 | Opus | PR README + ADR của Hải đã merge, PO chốt UC đăng ký giọng mẫu |
| — | Rà soát (grep `13/2023`, `ADR-0024`, đối chiếu MSG) | 05 mục 4.4, 5.4, 7 | Haiku | Trong lượt tương ứng |

**Vì sao không song song:** mọi lượt cùng sửa Report 3 DOCX (nhị phân, không merge được),
`05-use-case-specifications.md`, `06-business-rules.md`, `00-danh-muc-ma-thong-bao.md` và
`TrustCabinet_Sequence_Diagrams.drawio` (mọi sequence chung một file). Chỉ các diagram ở file riêng
(SM-04, use case diagram, context diagram, ERD) mới tách được cho subagent trong cùng lượt.

### 8.2 Một lượt gồm những gì

1. Đọc **chỉ**: note của ticket (`01`…`07`), mục plan tương ứng ở bảng 8.1, mục 0 của plan này,
   và các file nguồn mà mục đó ghi. Không làm việc của ticket khác.
2. **File patch cho Report 3** — `docs/notes/ban-giao-ba-final-review/note-may-nha/patch-<ticket>.md`
   (như lượt 1; việc phải chạy ở máy nhà như xuất PNG ghi thêm vào `note-may-nha/README.md`), mỗi dòng:
   mục Report 3 → ô/bước → câu cũ → câu mới. **Agent không sửa DOCX**; Khánh dán vào Word rồi chạy
   `python scripts/sync_reports.py`.
3. Sửa bản Markdown: `docs/01-requirements/05-use-case-specifications.md`,
   `06-business-rules.md`, `docs/03-api/00-danh-muc-ma-thong-bao.md` (bảng tiếng Anh) cho khớp patch.
4. Diagram: sequence `.txt` → tab trong drawio → `python scripts/sync_sequence_drawio_png.py`;
   rồi các diagram riêng của ticket.
5. Đánh dấu `[x]` các mục đã xong trong plan; một commit cho lượt đó (commit message tiếng Việt).
   **Cấm** dòng `Co-Authored-By: Claude …` hay bất kỳ dòng attribution nào trong commit.
6. Dừng lại, báo Khánh. Không tự sang lượt sau.

### 8.3 Giữ token thấp

- Mỗi lượt là **một session mới**, không nối vào session dài. Lời dặn chỉ cần: "làm lượt N theo
  `05-plan-sua-uc-br-msg-diagram.md` mục 8".
- **Không đọc cả Report 3** (hơn 4000 dòng): `grep -n` tìm mục rồi đọc đúng khoảng dòng.
- Drawio: sửa đúng ô cần sửa, giữ ID, không vẽ lại cả file; xuất PNG một lần ở cuối lượt.
- Câu hỏi ở mục 9 nên được trả lời trước khi bắt đầu lượt liên quan; gặp câu chưa có trả lời thì
  dùng mặc định ghi trong plan và nêu ra khi báo cáo, không dừng giữa chừng.

## 9. Câu hỏi cần trả lời trước

1. UC-30: giữ UC riêng (chỉ bỏ bộ đếm) hay gộp vào UC-27?
2. "Mở sẵn bộ lọc Đang diễn ra" có thành BR không, hay chỉ nằm ở UC-17 + đặc tả màn hình?
3. Voice ID: đăng ký/rút giọng mẫu là UC mới? Số UC bao nhiêu?
4. Voice ID: BE đã cấp mã thông báo chưa?
5. PR README/ADR Voice ID của Hải đã merge chưa?
6. SCRUM-107: thứ tự "kiểm quyền trước khi kiểm tệp" có thành BR không, hay chỉ ghi ở UC-33a?
7. SCRUM-107: quy tắc đuôi tệp khớp nội dung sửa vào BR-215 hay BR-187?
8. Nghị sự: PO đã chốt cho sắp xếp item Done chưa (SCRUM-98 còn ghi câu hỏi)?
9. Nghị sự: "hoàn thành mục lúc nào cũng được" có cho hoàn thành item Pending chưa bắt đầu không? Code hiện chỉ cho item InProgress.
10. Nghị sự: một tài liệu gắn nhiều item (note 08) hay một item (note 07 điểm 7) — code sau merge là cái nào?
