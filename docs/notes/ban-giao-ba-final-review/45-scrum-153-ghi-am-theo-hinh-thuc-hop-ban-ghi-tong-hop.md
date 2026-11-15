# SCRUM-153 — Ghi âm theo 3 hình thức họp, bản ghi tổng hợp của họp kết hợp

**Người ghi:** KhánhPG/BA, 13/11/2026. **Nguồn:** note TrustCabinet Bot gửi BA (12/11/2026 23:02 phần "Họp kết hợp",
13/11/2026 00:40); [ADR-0061](../../05-decisions/0061-hop-ket-hop-mot-dong-thoi-gian.md); PR
[#328](https://github.com/callmehai/FU-FA26-G116/pull/328) (merge `f2420ab7`), [#330](https://github.com/callmehai/FU-FA26-G116/pull/330)
(`5f330688`), hotfix [#331](https://github.com/callmehai/FU-FA26-G116/pull/331) (`1701c1c9`, cắt khoảng lặng, trần 12 giờ);
contract [12-ban-ghi-cuoc-hop.md § Bản ghi tổng hợp](../../03-api/12-ban-ghi-cuoc-hop.md#bản-ghi-tổng-hợp-của-họp-kết-hợp-adr-0061-điểm-5);
comment Jira SCRUM-153. PO chốt 12/11 và 13/11/2026.

Làm **sau note 44** (cùng Report 3, cùng drawio sequence; số BR lấy tiếp sau note 44).

## 1. Quy tắc đã chốt

| Hình thức | Ghi thế nào | Tải tệp lên |
|---|---|---|
| Trực tuyến | Theo micro từng người (không đổi) | Không (như cũ, `MSG358` với mic phòng) |
| Trực tiếp | Thư ký ghi bằng mic phòng | **Được**, nhưng hộp thoại cảnh báo: tệp phải ghi bằng đúng micro phòng đã dùng thu giọng mẫu (FE nhắc, server không chặn) |
| Kết hợp | Phòng: Thư ký ghi trực tiếp bằng mic phòng; người trực tuyến: micro của mình | **Không** — `409 MSG478` |

Bản ghi tổng hợp (chỉ họp kết hợp, ADR-0061 đ.5):

- Họp `Ended` rồi, server **tự gộp** mic phòng (từng lượt theo giờ bắt đầu thật) và mọi câu mic trực tuyến (theo giờ nói)
  thành **một tệp**, nhãn **Tổng hợp** trong tab Bản ghi. Không Mật. Chồng tiếng thì nghe chồng.
- Câu đã loại khỏi biên bản không vào bản tổng hợp.
- Dựng khi nguồn ổn: không còn lượt mic phòng đang ghi, không có gì mới trong 1 phút. Nguồn đổi về sau (câu gửi muộn, câu bị
  loại, lượt bị cắt) thì **tự dựng lại**. Thư ký **xoá** thì không dựng lại.
- Nghe, tải về (Chủ toạ, Thư ký), xoá như mọi bản ghi. **Không chuyển thành chữ** (`409 MSG479`).
- Khoảng lặng > 2 giây rút còn 2 giây; bản dài quá 12 giờ không dựng (#331).
- Cùng mục đích, người tạo và thời hạn lưu với bản ghi mic phòng (180 ngày, BR-347).

Bản dự thảo AI (ADR-0061 đ.2–3, chỉ để BA biết, bot không yêu cầu BR riêng): một dòng thời gian theo giờ thật; đoạn mic phòng
trùng giờ (±2 giây) và trùng ≥ 60% số từ với câu người trực tuyến là tiếng loa vọng, bản dự thảo bỏ đi. Tab Bản ghi vẫn hiện đủ.

## 2. Việc BA

### 2.1. UC

- [ ] **UC-44c Upload Meeting Audio File**
  - [ ] Description, PRE-2: chỉ họp **Trực tiếp** (bỏ Kết hợp); In Progress hoặc Ended.
  - [ ] EX mới: họp kết hợp → `MSG478`, không lưu tệp.
  - [ ] Bước chọn tệp/xác nhận: hệ thống nhắc tệp phải ghi bằng đúng micro phòng họp.
**Bản ghi tổng hợp: không thêm UC (đã chốt 13/11/2026)**, theo cùng quy tắc với note 44 §2.1. Người nghe và người tải
vẫn là tác nhân của UC-43, mục đích vẫn là nghe hoặc tải bản ghi, luồng nghe/tải y hệt. Chỉ phần danh sách khác (nhãn,
khi nào xuất hiện), nên phần đó là AF. Việc tự dựng / dựng lại không có người bấm, nên ghi bằng **BR**, không thành UC.

- [ ] **UC-43a View Meeting Recordings** — thêm AF:
  > AF-x – Combined recording of a Hybrid Meeting
  > 1. At Step 3, the Meeting is Hybrid, has Ended, is not Secret, and the system has built its combined recording.
  > 2. The system lists the combined recording with the label Combined, together with the other recordings. It has no transcript. (BR mới §2.2)
  > 3. The flow resumes at Step 3 of the Main Flow.
- [ ] **UC-43b Play**, **UC-43c Download** — chỉ sửa câu ("any recording, including the combined recording"), không thêm AF.
- [ ] **UC-44e Erase Meeting Recording** — thêm POST: xoá bản tổng hợp thì hệ thống không dựng lại.
- [ ] **UC-45c Transcribe** — EX mới: bản ghi tổng hợp → `MSG479`, không đưa vào hàng chờ.
- [ ] Inventory `00-master-use-case-inventory.md`.

### 2.2. BR (số tiếp sau note 44)

- [ ] **Sửa BR-340**: viết lại theo bảng §1 (trực tuyến mic từng người; trực tiếp mic phòng; kết hợp mic phòng ghi trực tiếp + mic từng người, không tải tệp).
- [ ] **Sửa BR-342**: "an audio file may be uploaded" chỉ cho họp Trực tiếp.
- [ ] BR mới: tệp tải lên ở họp trực tiếp phải ghi bằng đúng micro phòng (hệ thống nhắc, không kiểm được).
- [ ] BR mới: họp kết hợp Ended → hệ thống tự dựng bản ghi tổng hợp theo giờ thật; câu bị loại không vào; khoảng lặng > 2 giây còn 2 giây; quá 12 giờ không dựng.
- [ ] BR mới: nguồn đổi thì tự dựng lại; Thư ký xoá thì thôi.
- [ ] BR mới: bản ghi tổng hợp chỉ để nghe, không chuyển chữ.
- [ ] (tuỳ) BR bản dự thảo bỏ tiếng vọng loa phòng — hỏi leader có cần đưa vào SRS không.
- [ ] Kiểm BR-343, BR-347 có cần nhắc bản tổng hợp (cùng mục đích, thời hạn lưu với bản ghi mic phòng).

### 2.3. MSG

- [ ] Report 3 §V thêm `MSG478`, `MSG479` (câu ở [danh mục](../../03-api/00-danh-muc-ma-thong-bao.md) dòng 522–523).

### 2.4. Sơ đồ

- [ ] Sequence `docs/06-sequencediagram/meeting-recording/UC44c_UploadMeetingAudioFile.txt`: nhánh họp kết hợp `MSG478`.
- [ ] Sequence `UC43a_ViewMeetingRecordings.txt`: nhánh `opt` bản tổng hợp. `UC45c_TranscribeMeetingRecording.txt`: nhánh `MSG479`. `UC44e`: không dựng lại sau khi xoá.
- [ ] (tuỳ) Sequence job dựng bản tổng hợp, tác nhân hệ thống, không gắn số UC (như note 39).
- [ ] Xuất PNG, thêm tab drawio.
- [ ] ERD: `RecordingSource` thêm giá trị `Combined` (enum lưu chuỗi, không migration).

### 2.5. Màn hình — PENDING

Nhãn Tổng hợp trong tab Bản ghi, cảnh báo micro phòng trong hộp thoại tải tệp (`ConsentDialog.tsx`).

### 2.6. Jira

- [ ] Comment SCRUM-153 **sau khi PR #350 vào `main`** (kiểm `git log origin/main` có merge commit rồi mới comment).
      Ticket đang `Done` — **giữ nguyên, chỉ comment**, không chuyển trạng thái. Nội dung (tiếng Việt):

      > BA Final Review xong (Report 3, PR #350 <merge-commit>): sửa BR-340/342 theo bảng 3 hình thức họp;
      > UC-44c chỉ họp trực tiếp + EX-8 MSG478 + nhắc micro phòng; UC-43a thêm AF-3 bản ghi tổng hợp;
      > UC-43b/43c sửa câu; UC-44e POST không dựng lại sau khi xoá; UC-45c EX-4 MSG479; BR-455–458
      > (nhắc micro phòng; tự dựng sau Ended theo giờ thật, lặng >2s còn 2s, quá 12h không dựng, không họp
      > Mật; nguồn đổi dựng lại, xoá thì thôi; chỉ nghe không chuyển chữ); MSG478/479; ERD
      > `RecordingSource.Combined`; sequence UC44c/UC43a/UC45c/UC44e (+ nháp SYS bản tổng hợp, DRAFT).
      > Còn lại: đặc tả màn hình (nhãn Tổng hợp, cảnh báo micro phòng) — PENDING; BR-459 (Q2 tiếng vọng
      > loa phòng) còn chờ PO duyệt có đưa vào SRS.

## 3. Câu hỏi

- ~~Q1: bản tổng hợp là UC-43d hay UC riêng?~~ **Đã chốt 13/11/2026: không thêm UC**, AF ở UC-43a + BR (xem §2.1). Ghi lý
  do vào comment Jira SCRUM-153, vì bot ghi "UC mới".
- Q2: có đưa quy tắc bỏ tiếng vọng vào BR không? Khuyên **có**, một BR ngắn ở nhóm Minutes (bản dự thảo), vì nó đổi
  nội dung bản dự thảo người dùng thấy.

## 4. Dự thảo luồng UC (câu chữ cho Report 3) — chờ duyệt

Câu chữ tiếng Anh, chỉ ghi **phần đổi**; phần không nhắc giữ nguyên. Quy tắc chia: cùng tác nhân + cùng mục đích + khác
bước → AF; luồng y hệt → chỉ sửa câu; khác tác nhân → UC riêng; hệ thống tự chạy không ai bấm → BR, không viết AF.
`BR-new-x` là BR chưa đánh số.

### 4.1. Ghi âm theo hình thức họp

#### UC-44c Upload Meeting Audio File

- **Description**: "an In-person or Hybrid Meeting" → "an **In-person** Meeting".
- **PRE-2**: "The Meeting is **In-person** and is In Progress or Ended. (BR-340, BR-342)"
- **Bước 3** thêm: "The system reminds the Secretary that the file must have been recorded with the meeting room microphone used for the voice samples. (BR-new-1)"
- **EX mới** – Hybrid Meeting
  1. At Step 5, the Meeting is Hybrid.
  2. No file is kept. The system displays MSG478 and suggests recording live with the room microphone (UC-44a). (BR-342)
  3. The use case ends.

#### UC-43a View Meeting Recordings

- **Description** thêm: "…and, for a Hybrid Meeting that has ended, one combined recording."
- **Bước 4**: source "(room microphone or uploaded file)" → "(room microphone, uploaded file or combined)".
- **AF-3 (mới)** – Combined recording of a Hybrid Meeting
  1. At Step 4, the Meeting is Hybrid, has Ended and is not Secret, and the system has built its combined recording. (BR-new-2)
  2. The system lists it with the label Combined. It has no transcript and offers no transcription. (BR-new-4)
  3. The flow resumes at Step 5 of the Main Flow.

#### UC-43b Play, UC-43c Download — chỉ sửa câu

- 43b **Description**, **bước 1**: "a room recording" → "a room recording, the combined recording".
- 43c **Description**, **bước 1**: như trên. Quyền tải không đổi (Chủ toạ, Thư ký, không Mật).

#### UC-44e Erase Meeting Recording

- **POST mới**: "When the combined recording of a Hybrid Meeting is erased, the system does not build it again. (BR-new-3)"
- **AF-1** bước 1: thời hạn của bản tổng hợp tính như bản ghi mic phòng của cuộc họp đó. (BR-347)

#### UC-45c Transcribe Meeting Recording

- **EX mới** – Combined recording
  1. At Step 2, the recording is the combined recording of a Hybrid Meeting.
  2. Nothing is queued. The system displays MSG479. (BR-new-4)
  3. The use case ends.

#### BR mới (note 45)

- BR-new-1: An uploaded file of an In-person Meeting must be recorded with the meeting room microphone; the system reminds, it cannot check.
- BR-new-2: After a Hybrid Meeting ends, the system builds one combined recording from the room recording and every online sentence, on the real clock; excluded sentences are left out; silences over 2 s are shortened to 2 s; a track over 12 h is not built. Not for Secret Meetings.
- BR-new-3: When a source changes later, the system builds it again; once the Secretary erases it, never again.
- BR-new-4: The combined recording is for listening only and is never transcribed.
- BR-new-5 (Q2): In the AI draft, a room segment said at the same time (±2 s) as an online sentence and sharing at least 60% of its words is treated as the room speaker's echo and left out.

- [ ] Duyệt UC-44c, 43a–c, 44e, 45c và BR mới
