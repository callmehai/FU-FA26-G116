# PR #265 (SCRUM-35, 36, 32) và PR #267 (SCRUM-132) — việc BA mới sau note 34

**Người ghi:** KhánhPG/BA, 06/11/2026. **Nguồn:** kênh Discord `note-gửi-ba` (note của BE đính kèm), comment Jira
SCRUM-35 #10454/#10458, SCRUM-36 #10455/#10459, SCRUM-32 #10456, SCRUM-132 #10451, diff PR #267. Nối tiếp
[note 34](34-pr263-scrum-32-bien-ban-lay-du-thao-hien-co.md). Note này gom việc BA, không thay Jira hay đặc tả.

**Cập nhật 07/11/2026 (Lộc, BE):** xung đột `MSG406` ở §4 đã chốt theo review của leader trên #267. #267 rebase lên `main` và
đổi sang **`MSG414`**, mã kế tiếp lúc rebase ("Mã tiếp theo" giờ là `MSG415`). `MSG406` giữ nghĩa của #265 (`format` export
sai). Đã sửa §0, §4, §5, T35-8 cho khớp; comment Jira SCRUM-132 #10451 cũng đã sửa.

## 0. Tóm tắt

| Ticket | Việc BA | Trạng thái code |
|---|---|---|
| SCRUM-35 | UC Spec + sequence luồng **đổi người nói của một câu** | #265 đã vào `main` (`911d25bb`) |
| SCRUM-36 | UC-47–UC-50: mốc làm dự thảo báo cũ; đang viết lại thì không sửa/duyệt | #265 đã vào `main` |
| SCRUM-32 | UC-58–UC-60: trình soạn thảo, giới hạn HTML, PDF/Word, MSG399/402/404 | #263 + #265 đã vào `main` |
| SCRUM-132 | UC-01, UC-02, UC-07, BR, sequence: nhánh 429 (`MSG414`) | #267 **đang mở**, chưa vào `main`; mã đã chốt 07/11/2026 |

PR docs của BA nên gom SCRUM-35/36/32 một lượt (cùng đã ở `main`). SCRUM-132 đợi #267 merge.

## 1. SCRUM-35 — đổi người nói của một câu (UC Spec + sequence)

- Câu chuyển sang **giọng của người được chọn**. Người chưa có giọng thì hệ thống tạo giọng mới `SPEAKER_NN` (số nhỏ
  nhất chưa dùng), có thẻ, màu, thời lượng riêng; câu sau của cùng người vào lại giọng đó.
- `PUT /api/transcript-segments/{id}/speaker` (`MSG407`, chỉ Thư ký). Body đúng **một** trong `userId` / `guestId` /
  `label`; sai thì `400 MSG317`; nhãn không thuộc bản ghi này thì `404`.
- Chuyển câu và gán tên giọng cùng khoá bản ghi → hai thao tác đồng thời không làm sai thời lượng.
- Thời lượng đi theo câu. Giọng hết câu thì ẩn thẻ và không tính là "chưa gán tên".
- Bỏ cột `transcript_segments.speaker_user_id` (chưa từng được ghi) — sơ đồ lớp FE-10 và ERD mục tiêu BE đã sửa.
- Chưa rõ UC nào chứa luồng này: soát UC-43–UC-46 (gỡ băng/gán người nói) rồi chọn UC gần nhất để thêm luồng
  thay thế, không mở số UC mới nếu không cần.

## 2. SCRUM-36 — UC-47–UC-50

- Thẻ Dự thảo báo đỏ kèm *Tạo lại* khi lời thoại đổi sau lúc tạo (`readiness.transcriptChangedAt`). "Đổi" gồm: câu
  mới/sửa/bỏ/xoá; giọng được gán tên, hoặc mất tên do máy gán khi rút đồng ý Voice ID; câu chuyển giọng; bản ghi bị xoá.
- `generatedAt` = lúc AI **đọc** lời thoại, không phải lúc viết xong → câu sửa giữa chừng vẫn làm dự thảo báo cũ.
- **Đang có lượt AI viết lại thì không sửa hay duyệt dự thảo** (`409 MSG387`, cùng mã với khi bấm *Tạo lại*).
  Cần một BR mới (số kế tiếp sau BR cuối trong `06-business-rules.md`) hoặc gộp vào BR chống ghi đè dự thảo.
- Nút *Gán tên người nói* mở ngăn gán tên ngay trong tab Biên bản; dự thảo không còn dòng trống.
- Voice ID: dòng "mất tên do rút đồng ý" là hệ quả của [ADR-0027](../../05-decisions/0027-voice-id-co-dong-y.md) —
  UC rút đồng ý nên dẫn chiếu.

## 3. SCRUM-32 — UC-58–UC-60 sau #265

Ngoài thẻ báo + Tải PDF ở note 34:

- Trình soạn thảo kiểu Word (TipTap), đổi qua lại với văn bản thuần; HTML được server lọc (ADR-0055 trên `main`).
- Giới hạn: HTML lồng quá **64 tầng** → `400 MSG402`; bảng tối đa **63 cột**; nội dung biên bản tối đa
  **300.000 ký tự** (danh mục MSG402 và `17-du-thao-bien-ban.md` trên `main` đã đúng 300.000; BR-394 trong Report 3 còn 100.000 — cần sửa).
- PDF/Word **không tách khối ký tên qua hai trang**; bảng lồng quá 3 tầng in chữ nối tiếp.
- `MSG404` (503) là lỗi DB khi tạo/lưu biên bản; `MSG399` chỉ khi **đã có bản nháp**.
- Xuất: `GET /api/minutes/{id}/export?format=docx|pdf`; PDF dựng ở server (QuestPDF), `format` sai → `400 MSG406`; họp Mật `403 MSG405`. Note 34 §1 từng ghi "Tải PDF không có endpoint" — đã đính chính ở note 34 §5.

## 4. SCRUM-132 — giới hạn số lần thử (PR #267, đang mở)

- `POST /login`, `/google`, `/password-reset`: vượt giới hạn theo IP (chung ba endpoint) hoặc theo email (`/login`) →
  `429` + `MSG414`, kèm `Retry-After`. FE chỉ hiện thông báo.
- Việc BA khi #267 vào `main`: UC-01 (đăng nhập), UC-02 (Google), UC-07 (đặt lại mật khẩu) thêm luồng ngoại lệ; BR
  về khoá/giới hạn; sequence tương ứng thêm nhánh 429.
- ~~**Xung đột mã MSG406 — báo BE/PO trước khi viết.**~~ **Đã chốt 07/11/2026:** trên `main` `MSG406` = `400` `format`
  export sai (#265) và giữ nguyên. Leader chặn #267 ở review vì #267 cũng lấy `MSG406` = `429`, nên #267 đổi sang
  **`MSG414`**, mã kế tiếp lúc rebase (`MSG408`–`MSG413` đã được cấp cho màn Cấu hình AI). UC ghi `MSG414`.
- Ngưỡng và các điểm khác ticket (60 lần/phút/IP chung ba endpoint, 5 lần/phút/email cho `/login`, không có endpoint
  "quên mật khẩu" ẩn danh, `/google` chỉ giới hạn theo IP): xem comment Jira SCRUM-132 #10451 và
  `docs/03-api/01-xac-thuc.md` mục "Giới hạn số lần thử" của #267.
- SCRUM-131 (DOCX CSS lọt ra trang) cùng PR là lỗi FE, không có việc BA.

## 5. Việc BA còn lại

- [x] UC Spec + sequence "đổi người nói một câu" (SCRUM-35) → tab draw.io + PNG theo `DRAWIO_EDITING_RULES.md`.
- [x] UC-47–UC-50: mốc dự thảo cũ, BR "đang viết lại thì không sửa/duyệt" (MSG387) — kèm nhánh MSG387 trong
  sequence UC-49, UC-50.
- [x] UC-58–UC-60: các mục §3 và note 34 §2.
- [x] Sửa DOCX Report 3 → `sync_reports.py --report 3`; không sửa tay snapshot.
- [ ] Comment Jira SCRUM-35, 36, 32 khi PR docs ở `main` (§2.2 của DEVELOPMENT_WORKFLOW).
- [ ] SCRUM-132: đợi #267 vào `main` (xung đột mã đã chốt: `MSG414`).

## 6. Việc chi tiết để giao agent (06/11/2026)

Đối chiếu với `origin/main` 06/11/2026. **Quy tắc chung và bảng chia agent (A-DOCX, A-SEQ-*, A-DRAWIO) nằm ở
[note 34 §5](34-pr263-scrum-32-bien-ban-lay-du-thao-hien-co.md#5-việc-chi-tiết-để-giao-agent-06112026) — đọc trước.**
Mọi việc DOCX của note này giao cho **cùng A-DOCX** của note 34 (một DOCX, một agent). Sequence UC46b, UC47–50, UC60,
UC01, UC02, UC07 nằm ở file riêng nên giao song song được; tab draw.io làm sau.

**Hiện trạng `main` (đã kiểm):** UC-46b chưa nhắc MSG407 và chưa có luồng "đổi một câu"; UC-47–50 chưa có mốc dự thảo cũ
(MSG387 mới có ở UC-47 EX); BR-385 vẫn ghi "plain text", BR-391 "không có PDF", BR-394 "100,000 ký tự"; UC-60 còn ghi
"plain text … preview … Word file"; chưa có BR/UC cho 429.

**T35-1 — UC-46b: đổi người nói của một câu (A-DOCX, §II).** Thêm **AF-2 – Change the speaker of one sentence**
(Thư ký, tab Bản ghi): (1) chọn một câu, mở menu người của câu; (2) chọn người trên roster, Khách có lời mời còn hiệu lực,
hoặc một giọng chưa đặt tên của bản ghi để trả câu về; (3) hệ thống chuyển **riêng câu đó** sang giọng của người ấy — chưa có
giọng thì tạo giọng mới `SPEAKER_NN` nhỏ nhất chưa dùng (có thẻ, màu, thời lượng riêng); thời lượng đi theo câu; giọng hết câu
thì ẩn và không tính "chưa gán tên"; (4) audit ghi lại. Body gồm đúng **một** trong `userId`, `guestId`, `label` (BR-368).
EX: gửi sai (cả hai, hoặc người không hợp lệ) → MSG317 (đã có); `label` không thuộc bản ghi này → 404 không mã (giống UC-80 EX-1).
Thành công trả **MSG407**. Chỉ Thư ký hiện tại (EX actor sai: dùng mã sẵn của UC-46b). Postcondition: chuyển câu và gán tên
giọng khoá cùng bản ghi nên không làm sai thời lượng.
*Xong khi:* `grep MSG407` trong `05-use-case-specifications.md` có trong UC-46b; BR-368 vẫn đúng chữ.

**T35-2 — BR cho T35-1 (A-DOCX, §V).** Thêm BR mới (số kế tiếp sau BR cuối trong `06-business-rules.md`, hiện BR-394 — kiểm
lại trước khi đánh số, vì PR khác có thể đã lấy): *Changing the speaker of one sentence moves that sentence to the chosen
person's voice, creating a new voice for a person who has none; the other sentences keep their voices.* UC: UC-46b; nguồn:
SCRUM-35 / PR #265.

**T35-3 — Sequence UC46b (A-SEQ-46b).** File `docs/06-sequencediagram/transcription-voice-id/UC46b_AssignSpeaker.txt`:
thêm khối `alt` "Change one sentence - AF-2": `TranscriptController` gọi `TranscriptionService: assignSegmentSpeaker(segmentId, userId | guestId | label, actorId)`;
đọc câu + bản ghi, kiểm roster/Khách như khối hiện có, tìm hoặc tạo giọng (`SPEAKER_NN`), chuyển câu, tính lại thời lượng,
`AuditService`, ẩn giọng hết câu; nhánh lỗi MSG317 và 404. Giữ phong cách `activate/deactivate` của file. Xuất lại PNG.

**T35-4 — UC-47–UC-50: dự thảo cũ + khoá khi AI đang viết (A-DOCX, §II).**
- UC-47: Description/Postcondition ghi `generatedAt` = lúc AI **đọc** lời thoại (không phải lúc viết xong).
- UC-48 (xem): thẻ Dự thảo báo **đỏ kèm nút Tạo lại** khi lời thoại đổi sau `generatedAt`. "Đổi" gồm: câu mới, câu sửa, bỏ ra, xoá;
  giọng được gán tên, hoặc mất tên do máy gán khi rút đồng ý Voice ID; câu chuyển giọng; bản ghi bị xoá. Nút *Gán tên người nói*
  mở ngăn gán tên ngay trong tab Biên bản.
- UC-49 (sửa) và UC-50 (duyệt): thêm EX – **AI đang viết lại** → không sửa/duyệt được, hiện **MSG387** (dùng đúng câu thông báo
  trong danh mục; đối chiếu UC-47 EX hiện có).
- Dự thảo không còn dòng trống (AI viết và server lưu đều bỏ) — ghi vào UC-49 nếu đặc tả có nói về định dạng.
- BR mới (số xem T35-2): *While the AI is writing a draft again, the draft cannot be edited or approved; the actor waits for it to
  finish.* UC-49, UC-50; nguồn SCRUM-36 / PR #265. Dẫn chiếu BR-365 và BR-372 ở chỗ nói "mất tên do rút đồng ý".

**T35-5 — UC-58/60 + BR trình soạn Word (A-DOCX, §II + §V).**
- **BR-385** viết lại: *Minutes content is HTML that the server filters before saving; the page, the Word file and the PDF file read
  the same content, and nothing in it can run in a browser.* (bỏ "plain text with line marks").
- **BR-394**: *Minutes content may not be empty (after filtering), may not exceed 300,000 characters, and its tags may not nest
  deeper than 64 levels; a table has at most 63 columns.*
- UC-60 Description/Normal Flow: "plain text" → trình soạn kiểu Word; bước sửa có thanh công cụ: kiểu đoạn, cỡ chữ, màu, căn lề,
  danh sách, bảng, ngắt trang, tìm và thay thế; đổi qua lại với văn bản thuần; bước lưu: server lọc HTML rồi lưu và trả lại bản đã lọc.
- UC-60 EX-2: "empty or longer than 100,000" → "empty, longer than 300,000 characters, or nested deeper than 64 levels" (**MSG402**, câu thông
  báo lấy ở danh mục).
- UC-59/UC-60: PDF/Word không tách khối ký tên qua hai trang; bảng lồng quá 3 tầng trong PDF in chữ nối tiếp, không kẻ lưới (ghi ở
  Description hoặc ghi chú UC-60).
- UC-59: lỗi DB khi tạo biên bản → **503 MSG404**; **MSG399** chỉ khi đã có bản nháp mà không gửi `rebuild` (kiểm UC-59 AF-2/EX đã
  dùng đúng mã).
- UC-58 (xem): hiển thị HTML đã lọc; kiểm câu chữ còn nói "plain text" không.
*Xong khi:* không còn `plain text` và `100,000` trong UC-58/59/60 và BR-385/394 của `06-business-rules.md`.

**T35-6 — Sequence UC60 (A-SEQ-60, cùng file với T34-4).** `UC60_EditMeetingMinutes.txt`: ở bước lưu thêm
`MinutesService: Filter the HTML (nesting, columns, allowed tags)`, nhánh `alt` nội dung không hợp lệ → MSG402; trả lại nội dung đã
lọc cho trang; nhánh tải: `format = docx | pdf` (T34-4). Xuất lại PNG. Nếu `UC58_ViewMeetingMinutes.txt` nói "plain text", sửa tương tự.

**T35-7 — Sơ đồ lớp (không bắt buộc).** BE đã sửa FE-10 và ERD mục tiêu. `docs/07-classdiagram/README.md` ghi FE-12 chưa có sơ đồ
lớp; muốn có thì cần ticket "Class FE-12" (chưa tồn tại) — **hỏi người dùng trước**, đừng tự vẽ hay tự tạo ticket.

**T35-8 — SCRUM-132 (CHỜ, chưa làm).** Chỉ làm khi #267 đã ở `main` (mã đã chốt: `MSG414`, §4). Khi đó:
(a) UC-01, UC-02, UC-07 (A-DOCX): thêm EX "Too many attempts" — vượt giới hạn theo IP (chung ba endpoint) hoặc theo email (chỉ đăng
nhập email) → `429` + `MSG414` + `Retry-After`; hệ thống không thử xác thực; luồng quay về bước nhập. Chữ thông báo lấy từ danh mục.
(b) BR mới về giới hạn: ngưỡng và cửa sổ thời gian lấy từ `appsettings.json` và `docs/03-api/01-xac-thuc.md` mục "Giới hạn số lần
thử" của #267, **không đoán số**.
(c) Sequence `auth/UC01_SignIn.txt`, `auth/UC02_SignInWithGoogle.txt`, `user-management/UC07_ResetUserPassword.txt`: thêm `alt`
"Rate limit exceeded" ở đầu luồng (A-SEQ-auth, song song được).
(d) ~~**Việc của người, không phải agent:** nhắn BE/PO về trùng MSG406 trước khi merge #267.~~ Đã xong 07/11/2026: leader
chặn ở review #267, BE đổi sang `MSG414`.

**T35-9 — Đóng việc.** Sau A-DOCX, SEQ và A-DRAWIO: `sync_reports.py --report 3`, `sync_requirement_extracts.py --check`; PNG trong
`docs/06-sequencediagram/drawio-png/` qua `scripts/sync_sequence_drawio_png.py` nếu repo yêu cầu. Comment Jira, mỗi ticket một
comment (UC/BR/MSG đổi, PR + commit, còn lại gì): **SCRUM-35** (T35-1..3), **SCRUM-36** (T35-4), **SCRUM-32** (T34 + T35-5/6),
**SCRUM-132** (sau T35-8); đổi trạng thái theo §2.2 của DEVELOPMENT_WORKFLOW. SCRUM-35/36/32 hiện In Review.

### Thứ tự gợi ý

1. Song song: A-DOCX (T34-1, 2, 4, 5, 6 + T35-1, 2, 4, 5), A-SEQ-59 (T34-3), A-SEQ-60 (T34-4 + T35-6), A-SEQ-46b (T35-3), A-SEQ-47..50
   (nếu muốn thêm nhánh MSG387 vào bốn sequence UC47–50 trong `transcription-voice-id/`).
2. Sau SEQ: A-DRAWIO cập nhật tab UC46b, UC47–50, UC59, UC60 (theo `DRAWIO_EDITING_RULES.md`; lưu ý lỗi tab lệch trên Windows ở note 32 §4).
3. Sau A-DOCX: T34-7 và T35-9 (sync + comment Jira).
4. SCRUM-132 (T35-8) để sau khi #267 merge.

## 7. Trạng thái thực hiện — nhánh `docs/ba-final-review-note34-35` (06/11/2026)

Đã commit `479a8400` và mở PR [#275](https://github.com/callmehai/FU-FA26-G116/pull/275) (base `main`).
Nội dung trong commit đó:

- **T35-1** UC-46b thêm **AF-2 – Change the speaker of one sentence** (MSG407) và **EX-4** (giọng không thuộc bản ghi → 404, không mã); EX-1
  sửa thành "không nêu đúng một trong user / Guest / giọng".
- **T35-2** thêm **BR-395**.
- **T35-3** `UC46b_AssignSpeaker.txt` thêm khối `opt … AF-2` (tìm/tạo giọng `SPEAKER_NN`, chuyển câu, tính lại thời lượng, audit, ẩn giọng
  hết câu, nhánh MSG317/404/MSG304/MSG407); xuất lại `UC46b_AssignSpeaker.png`, `drawio-png/uc46b_assignspeaker.drawio.png` và tab
  UC-46b trong file gộp.
- **T35-4** UC-47 `generatedAt` = lúc AI đọc lời thoại; UC-48 thẻ đỏ + *Tạo lại* (AF-4), nút *Gán tên người nói* (bước 6); UC-49/UC-50 thêm
  EX "AI đang viết lại" → MSG387; UC-49 bỏ dòng trống; thêm **BR-396**.
- **T35-5** UC-58 đọc HTML đã lọc; UC-60 trình soạn kiểu Word, 300.000 ký tự / 64 tầng / 63 cột, PDF + Word, không tách khối ký tên,
  EX-8 `format` sai → MSG406; **BR-385**, **BR-390**, **BR-391**, **BR-394** viết lại. Đã thêm dòng MSG406/407 vào bảng V.2 của Report 3
  (vì UC dẫn mã; danh mục MSG vẫn là nguồn chuẩn).
- **T35-6** `UC60_EditMeetingMinutes.txt` thêm bước lọc HTML + trả nội dung đã lọc, nhánh `format = docx | pdf`; xuất lại PNG + tab UC-60.
- **T34-1..T34-4, T34-6** xong ở [note 34 §6](34-pr263-scrum-32-bien-ban-lay-du-thao-hien-co.md#6-trạng-thái-thực-hiện--nhánh-docsba-final-review-note34-35-06112026);
  **T34-7** đã sync, comment Jira để sau khi PR docs vào `main`. `sync_reports.py --report 3` và `sync_requirement_extracts.py --check` đều sạch.

**Bổ sung sau khi rà lại note (cùng PR #275):**

- Sequence `UC49_EditMeetingSummary.txt` và `UC50_ApproveMeetingSummary.txt` thêm nhánh *The AI is writing the draft again*
  (EX-6 của UC-49, EX-5 của UC-50) → `MSG387`, đúng như đặc tả đã sửa ở T35-4; xuất lại PNG và hai tab UC-49/UC-50 trong file
  draw.io gộp + `drawio-png/` (chỉ hai tab đó đổi, không thêm/bớt tab).
- Report 3 §II: dòng tóm tắt UC-60 trong bảng danh sách Use Case còn ghi *"Correct the plain text … download it as a Word file"* —
  đã sửa thành trình soạn kiểu Word + Word/PDF trong DOCX rồi sync lại; `sync_requirement_extracts.py --check` vẫn `UP TO DATE`.

**Tạm hoãn / còn lại:**

- **Đặc tả màn hình — tạm không làm (theo yêu cầu 06/11/2026):** không sửa §III 12.1 Minutes Editor (vùng thông báo trên thẻ, nút *Tải PDF*,
  bảng field) và không thay ảnh wireframe. Việc này gộp vào đợt đặc tả màn hình sau; T34-5 vì thế vẫn mở.
- **T35-7** sơ đồ lớp FE-12: chưa có ticket, phải hỏi người dùng trước.
- **T35-8** SCRUM-132: #267 **chưa** vào `main`, xung đột MSG406 chưa chốt — giữ nguyên, chưa sửa UC-01/02/07.
- **T35-9** comment Jira + đổi trạng thái: chỉ làm khi PR docs đã ở `main`, theo §2.2 của DEVELOPMENT_WORKFLOW.
