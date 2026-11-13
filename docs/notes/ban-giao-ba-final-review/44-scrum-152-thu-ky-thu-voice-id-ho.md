# SCRUM-152 — Thư ký thu giọng mẫu Voice ID hộ bằng micro phòng

**Người ghi:** KhánhPG/BA, 13/11/2026. **Nguồn:** ba note của TrustCabinet Bot gửi BA (12/11/2026 23:02, 23:16, 23:43);
[ADR-0060](../../05-decisions/0060-thu-ky-thu-voice-id-ho.md); PR [#327](https://github.com/callmehai/FU-FA26-G116/pull/327)
(merge `964e25a5`) và [#329](https://github.com/callmehai/FU-FA26-G116/pull/329) (merge `cdc021df`, một lần đồng ý);
contract [13-nhan-dien-giong-noi.md § Thư ký thu giọng mẫu hộ](../../03-api/13-nhan-dien-giong-noi.md#thư-ký-thu-giọng-mẫu-hộ--adr-0060);
comment Jira SCRUM-152.

> **Note 23:43 thay note 23:02.** PO chốt lại: **một lần đồng ý Voice ID** cho cả mẫu tự thu và mẫu Thư ký thu hộ.
> **Không** làm UC "cho phép / rút đồng ý thu hộ" riêng. `MSG471`, `MSG472` ngừng dùng. Đọc §1 theo bản đã chốt, bỏ qua
> phần "đồng ý thu hộ riêng" của note đầu.

## 1. Quy tắc đã chốt (theo code trên `main`)

| # | Quy tắc | Nguồn |
|---|---|---|
| R1 | Mỗi người có **hai bộ mẫu**: tự thu và Thư ký thu hộ, **mỗi bộ tối đa 5**. Cả hai nằm dưới **một** đồng ý Voice ID | ADR-0060 đ.1 |
| R2 | Thông báo Voice ID lên **1.3**, nói rõ Thư ký cuộc họp mình dự có thể thu bằng micro phòng. Ai đồng ý bản cũ thấy "Đồng ý điều khoản mới", bấm một lần, mẫu cũ giữ nguyên. Chưa đồng ý hoặc đồng ý bản < 1.3 thì Thư ký không thu được (`MSG475`) | ADR-0060 đ.2 |
| R3 | Thư ký phải có quyền **"Thu giọng mẫu hộ"** (`VoiceCollector`, thiếu thì `403`). Vai trò Thư ký có sẵn quyền này; quản trị viên gỡ được | ADR-0060 đ.3 |
| R4 | Chỉ **Thư ký hiện tại của chính cuộc họp đó**, kiểm ở mỗi lần gọi; không phải thì `404` như cuộc họp không tồn tại. Bị thay giữa chừng là mất quyền ngay | ADR-0060 đ.4 |
| R5 | Chỉ họp **trực tiếp hoặc kết hợp**, **không Mật**, chưa huỷ. **Thu** được khi **Đã công bố** hoặc **Đang họp** (`MSG477`); sau đó vẫn xem, nghe, xoá | ADR-0060 đ.5 |
| R6 | Mẫu thu hộ thuộc về người được thu. Thư ký chỉ thấy, nghe, xoá mẫu thu ở cuộc họp của mình; thu lại = xoá rồi thu mới. Đủ 5 mẫu thu hộ (tính mọi cuộc họp): `MSG476` | ADR-0060 đ.6 |
| R7 | Mỗi lần thu, chủ mẫu **nhận thông báo**. Mỗi lần Thư ký **nghe lại** một mẫu ghi nhật ký. Thu, xoá mẫu thu hộ có nhật ký | ADR-0060 đ.7 |
| R8 | Chủ mẫu thấy mọi mẫu thu hộ của mình (cuộc họp nào, ai thu), nghe và xoá được | ADR-0060 đ.6 |
| R9 | Rút đồng ý Voice ID (hoặc bị vô hiệu hoá tài khoản) **xoá cả hai bộ** | ADR-0060 đ.9 |
| R10 | Máy so giọng với cả hai bộ, lấy mẫu khớp nhất; mẫu thu hộ được tự điền tên như mẫu tự thu | ADR-0060 đ.8 |
| R11 | Người được thu là người trong danh sách thành phần hiện tại, **không gồm Khách** | contract `GET …/voice-collection` |

## 2. Việc BA

Thứ tự làm từ trên xuống. Report 3: sửa DOCX trong `docs/08-reports/source/`, sync + `check_docx.py` một lần ở cuối.
Nhớ [quy tắc ngôn ngữ nghiệp vụ](../../08-reports/README.md): không tên endpoint, không tên quyền kiểu `VoiceCollector` trong SRS.

### 2.1. UC

**Cách chia (đã chốt 13/11/2026):** cùng tác nhân chính, cùng mục đích, khác cách làm → **Alternative Flow** trong UC
có sẵn. Khác tác nhân chính → **UC riêng**. Luồng y hệt, chỉ khác nhãn → **chỉ sửa câu chữ**, không thêm AF.

- Phía chủ mẫu: **không thêm UC con**. Người dùng vẫn là tác nhân, mục đích vẫn là quản lý Voice ID của mình.
- Phía Thư ký: **UC mới UC-86**. Mục đích giống UC-81c (có giọng mẫu) nhưng tác nhân là Thư ký, có điều kiện riêng
  (quyền, đúng Thư ký của cuộc họp, hình thức họp, trạng thái), màn khác, mã khác. Không gộp được làm AF của UC-81c.

- [ ] **UC-81a View My Voice ID** — thêm **AF-2** (luồng khác thật: tab riêng, cột khác, không có nút thu, mở từ thông báo):
  > AF-2 – View samples recorded by a Secretary
  > 1. At Step 3, the actor opens the Recorded by Secretary tab, or opens My voice from the notification that a Secretary recorded a sample.
  > 2. The system shows the samples that Secretaries recorded for the actor on the meeting room microphone, each with its Meeting, the Secretary who recorded it, its length and date, and how many of the 5 allowed samples are used. The actor cannot record a sample in this tab. (BR-448, BR-452)
  > 3. The flow resumes at Step 3 of the Main Flow.
  - Bước 3 Main Flow: "the list of voice samples" → "the voice samples the actor recorded".
- [ ] **UC-81b Give Voice ID Consent** — Description, bước 1: thông báo **1.3** nói Thư ký cuộc họp mình dự có thể thu giọng mẫu bằng micro phòng. AF-1 "Accept new terms" có sẵn, giữ; chỉ thêm ý "after accepting, a Secretary can record samples for the actor" (BR-449).
- [ ] **UC-81c Record Voice Sample** — chỉ sửa câu: "while fewer than 5 samples exist" → "fewer than 5 samples **the actor recorded**; samples recorded by a Secretary are counted separately" (BR-448).
- [ ] **UC-81d Play Voice Sample** — chỉ sửa câu, **không thêm AF** (luồng y hệt): Description "own voice samples, recorded by himself or herself or by a Secretary"; bước 1 "next to a voice sample in either tab".
- [ ] **UC-81e Delete Voice Sample** — chỉ sửa câu như 81d; thêm POST: mẫu do Thư ký thu bị xoá thì Thư ký không còn thấy, và chỗ trống được tính lại.
- [ ] **UC-81f Withdraw Voice ID Consent** — xoá **cả hai bộ** mẫu (BR-454 hoặc sửa BR rút đồng ý cũ).
- [ ] **UC mới cho Thư ký — UC-86 Collect Voice Samples for Attendees** (UC-86 chưa dùng), cùng FE với UC-81:
  - [ ] UC-86a View Voice Collection: danh sách người trong cuộc họp, đã đồng ý chưa, số mẫu thu hộ/5, mẫu thu ở cuộc họp này.
  - [ ] UC-86b Record Voice Sample for Attendee: `MSG473`; EX `MSG475` (chưa đồng ý 1.3), `MSG476` (đủ 5), `MSG477` (sai trạng thái), lỗi chất lượng/định dạng dùng lại `MSG309`, `MSG292`, `MSG312`.
  - [ ] UC-86c Play Collected Voice Sample: có nhật ký mỗi lần nghe.
  - [ ] UC-86d Delete Collected Voice Sample: `MSG474`.
  - PRE chung: có quyền Thu giọng mẫu hộ, là Thư ký hiện tại, họp trực tiếp/kết hợp, không Mật, chưa huỷ.
  - UC-86b dẫn lại kiểm tra chất lượng của UC-81c (BR-363), không chép lại luồng.
- [ ] Cập nhật `docs/01-requirements/00-master-use-case-inventory.md` (UC-86a–d; mô tả UC-81 có mẫu thu hộ).

### 2.2. BR (số dự kiến, lấy tiếp từ BR-447; note 45, 46 lấy số sau)

- [ ] BR-448 — R1 hai bộ mẫu, một đồng ý, mỗi bộ tối đa 5.
- [ ] BR-449 — R2 thông báo 1.3 và đồng ý lại một lần; Thư ký không đồng ý thay được.
- [ ] BR-450 — R3 + R4 quyền Thu giọng mẫu hộ, chỉ Thư ký hiện tại của chính cuộc họp.
- [ ] BR-451 — R5 hình thức họp, mức mật, trạng thái.
- [ ] BR-452 — R6 + R8 mẫu thuộc về người được thu; ai thấy gì.
- [ ] BR-453 — R7 thông báo mỗi lần thu, nhật ký mỗi lần nghe/thu/xoá.
- [ ] BR-454 — R9 rút đồng ý hoặc vô hiệu hoá tài khoản xoá cả hai bộ (hoặc sửa thẳng BR cũ, khi đó không cần số mới).
- [ ] Sửa BR Voice ID cũ có câu "rút đồng ý xoá mọi mẫu" thành "cả hai bộ" (R9); sửa BR giới hạn 5 mẫu thành "mỗi bộ".

### 2.3. MSG (Report 3 §V)

- [ ] Thêm `MSG473`–`MSG477`, câu theo [danh mục](../../03-api/00-danh-muc-ma-thong-bao.md) dòng 589–593.
- [ ] `MSG471`, `MSG472`: nếu Report 3 đã có thì đánh dấu **ngừng dùng**, không tái cấp; chưa có thì không thêm.

### 2.4. Ma trận phân quyền (Report 3 §I.4.4)

- [ ] Thêm cột/dòng **"Thu giọng mẫu hộ"**: Thư ký có sẵn, vai trò khác không.

### 2.5. Sơ đồ

- [ ] Sequence: sửa `UC81a` (nhánh `alt` AF-2), `UC81b` (thông báo 1.3), `UC81f` (xoá hai bộ) trong `docs/06-sequencediagram/transcription-voice-id/`; `UC81d`, `UC81e` không đổi luồng. Thêm `UC86a`–`UC86d`. Xuất PNG, thêm tab drawio.
- [ ] Sơ đồ use case FE Voice ID: thêm UC-86 cho Thư ký.
- [ ] ERD [02-erd.md](../../02-design/02-erd.md) §7: `voice_samples` thêm `collected_by_id`, `meeting_id` (cả hai null = mẫu tự thu, cả hai có = mẫu thu hộ); `voice_consents` vẫn một dòng hiệu lực mỗi người. Vẽ lại ERD mục tiêu.

### 2.6. Màn hình — PENDING

Giọng nói của tôi (tab Thư ký thu, `/mgmt/my-voice?tab=proxy`), hộp thoại thu hộ ở tab Bản ghi. Đặc tả màn chờ như các note trước.

### 2.7. Jira

- [ ] Comment SCRUM-152: UC/BR/MSG đã sửa, PR docs + commit, còn lại màn hình. Đổi trạng thái theo [DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update) **sau khi** PR docs vào `main`.

## 3. Câu hỏi

- ~~Q1: tách UC-81g–81i hay gộp?~~ **Đã chốt 13/11/2026: gộp** (AF-2 ở UC-81a, sửa câu ở 81b–81f). Ghi lý do vào comment
  Jira SCRUM-152, vì bot ghi "thêm UC con".

## 4. Dự thảo luồng UC (câu chữ cho Report 3) — chờ duyệt

Câu chữ tiếng Anh, chỉ ghi **phần đổi**; phần không nhắc giữ nguyên. Quy tắc chia: cùng tác nhân + cùng mục đích + khác
bước → AF; luồng y hệt → chỉ sửa câu; khác tác nhân → UC riêng; hệ thống tự chạy không ai bấm → BR, không viết AF.
`BR-new-x` là BR chưa đánh số.

### 4.1. Voice ID, phía chủ mẫu (sửa UC-81)

#### UC-81a View My Voice ID

- **Description** thêm: "…and which voice samples it holds: those the actor recorded and those Secretaries recorded for the actor on a meeting room microphone."
- **Main Flow bước 3**: "the list of voice samples" → "the list of voice samples **the actor recorded**".
- **AF-1** bước 2 thêm: "…including that the Secretary of a Meeting the actor attends may record voice samples of the actor on the meeting room microphone."
- **AF-2 (mới)** – View samples recorded by a Secretary
  1. At Step 3, the actor opens the Recorded by Secretary tab, or opens My voice from the notification that a Secretary recorded a sample.
  2. The system shows the samples Secretaries recorded for the actor, each with its Meeting, the Secretary who recorded it, its length and date, and how many of the 5 allowed samples are used. The actor cannot record a sample in this tab. (BR-448, BR-452)
  3. The flow resumes at Step 3 of the Main Flow.

#### UC-81b Give Voice ID Consent

- **Description**: "version 1.2" → "version 1.3"; thêm "…and that the Secretary of a Meeting the person attends may record voice samples of the person on the meeting room microphone."
- **Main Flow bước 1**: phần mục đích thêm ý trên. (BR-449)
- **AF-1** bước 3 thêm: "From then on a Secretary may also record voice samples for the actor (UC-86b)."

#### UC-81c Record Voice Sample

- Chỗ nói giới hạn: "fewer than 5 samples" → "fewer than 5 samples **recorded by the actor**. Samples recorded by a Secretary are counted separately. (BR-448)"
- EX giới hạn (`MSG310`): "the actor already has 5 samples **the actor recorded**".

#### UC-81d Play Voice Sample — chỉ sửa câu

- **Description**: "…one of his or her own voice samples, recorded by himself or herself or by a Secretary."
- **Bước 1**: "…next to a voice sample, in either tab."

#### UC-81e Delete Voice Sample — chỉ sửa câu

- **Description** như 81d. **Bước 1** như 81d.
- **POST mới**: "A sample recorded by a Secretary no longer appears to that Secretary, and no longer counts toward the 5 samples a Secretary may record for the actor. (BR-448, BR-452)"

#### UC-81f Withdraw Voice ID Consent

- **Description**, **POST-1**, **bước 2**, **bước 4**: "all of the actor's voice samples" → "all of the actor's voice samples, **both those the actor recorded and those Secretaries recorded**". (BR-454)

---

### 4.2. UC-86 Collect Voice Samples for Attendees (mới, tác nhân Thư ký)

Chung cho UC-86a–86d:

- **Primary Actor**: Secretary. **Secondary**: None.
- **PRE chung**: PRE-1 The actor holds the Collect voice samples permission and is the current Secretary of the Meeting. (BR-450) PRE-2 The Meeting is In-person or Hybrid, is not Secret and is not Cancelled. (BR-451)
- **EX chung "No access"**: At the check step, the actor lacks the permission or is no longer the current Secretary, or the Meeting no longer meets PRE-2. Nothing is done and the system answers as if the Meeting did not exist. The use case ends. (BR-450, BR-451)

#### UC-86a View Voice Collection

- **Description**: Allows the Secretary to see, for each person on the roster, whether the person agreed to Voice ID and which samples were recorded for the person at this Meeting.
- **Main Flow**
  1. On the Meeting detail, the Secretary selects the Recordings tab.
  2. The system checks the actor and the Meeting. (BR-450, BR-451)
  3. The system shows each internal person on the current roster (Guests are not listed). For a person who agreed under notice 1.3 or later, it shows how many samples Secretaries have recorded for the person in all Meetings, out of 5, the samples recorded at this Meeting with who recorded them and when, and Record while fewer than 5 exist. For any other person it shows Not agreed. (BR-448, BR-449)
- **AF-1** – Meeting is not Published or In Progress
  1. At Step 3, the Meeting is Draft, Waiting for approval, Approved, Rejected or Ended.
  2. The system shows the list without Record and explains that samples can be recorded only while the Meeting is Published or In Progress. Samples already recorded can still be played and deleted. (BR-451)
  3. The use case ends.
- **EX-1** No access (chung). **EX-2** – List cannot be loaded: nothing is shown; no catalogue message is assigned to this case yet.

#### UC-86b Record Voice Sample for Attendee

- **PRE thêm**: The Meeting is Published or In Progress. The person agreed to Voice ID under notice 1.3 or later. (BR-449, BR-451)
- **Main Flow**
  1. Next to a person, the Secretary selects Record.
  2. The system shows a passage to read and reminds the Secretary to use the meeting room microphone and to let the person speak naturally, as when taking the floor.
  3. The Secretary selects Start recording, the person reads the passage, the Secretary selects Stop. Nothing is sent yet: the system plays the recording back, and the Secretary selects Save sample or Record again.
  4. When the Secretary saves, the system receives the recording.
  5. The system checks the actor and the Meeting, that the person is on the roster and agreed under notice 1.3 or later, and that fewer than 5 samples were recorded for the person by Secretaries. (BR-448–BR-451)
  6. The system checks that the recording has 6 to 60 seconds of clear speech from one person. (BR-363)
  7. The system learns the features of the voice and stores the sample protected, as the person's own data, with the Meeting and the Secretary who recorded it. (BR-364, BR-369, BR-452)
  8. The system notifies the person and records the sample in the audit log. (BR-453)
  9. The system displays MSG473 – "Voice sample recorded for the attendee." *(câu tiếng Anh tạm; tiếng Việt: "Đã lưu giọng mẫu thu hộ.")*
- **EX**
  - EX-1 No access (chung).
  - EX-2 Person has not agreed under the current notice → MSG475. Sample not kept. Use case ends.
  - EX-3 Person already has 5 samples recorded by Secretaries → MSG476. Sample not kept. Use case ends.
  - EX-4 Meeting is not Published or In Progress → MSG477. Sample not kept. Use case ends.
  - EX-5 Unsupported audio type → MSG292. Resumes at Step 1.
  - EX-6 Sample does not meet the quality requirements → MSG309 with the reason. Resumes at Step 1. (BR-363)
  - EX-7 Voice service unavailable → MSG312. Use case ends.
  - EX-8 Person is not on the current roster → as if the person did not exist. Use case ends.

#### UC-86c Play Collected Voice Sample

- **Main Flow**
  1. The Secretary selects Play next to a sample recorded at this Meeting.
  2. The system checks the actor and the Meeting and that the sample was recorded at this Meeting. (BR-450, BR-452)
  3. The system records the playback in the audit log, unprotects the recording and plays it. (BR-453)
- **EX-1** No access, or the sample was recorded at another Meeting or by the person: nothing plays, as if the sample did not exist.

#### UC-86d Delete Collected Voice Sample

- **Main Flow**
  1. The Secretary selects Delete on a sample recorded at this Meeting.
  2. The system asks for confirmation.
  3. The Secretary confirms.
  4. The system checks as in UC-86c, deletes the sample, its recording and the features of its voice, and records the deletion in the audit log. (BR-452, BR-453)
  5. The system displays MSG474 – "Voice sample deleted." *(tiếng Việt: "Đã xoá giọng mẫu thu hộ.")*
- **AF-1** – Record again: after Step 5 the Secretary may record a new sample for the person (UC-86b). Recording again always means delete then record.
- **EX-1** như UC-86c. **EX-2** cannot delete → MSG315 *(kiểm lại code trả mã nào trước khi chép)*.
- Ghi chú: xoá được cả khi cuộc họp đã Ended (không cần PRE trạng thái).

- [ ] Duyệt UC-81a–81f và UC-86a–86d
