# SCRUM-129 — Khách ghi âm theo micro của mình; ERD còn thiếu `speech_clips`

**Ghi ngày:** 03/11/2026. **Nguồn:** tin nhắn bot Slack ngày 03/11/2026, [PR #245](https://github.com/callmehai/FU-FA26-G116/pull/245)
(merged 15:01 03/11/2026), comment Jira `10418` trên [SCRUM-129](https://sep490g116.atlassian.net/browse/SCRUM-129) và
[ADR-0051](../../05-decisions/0051-khach-ghi-theo-micro-cua-minh.md).

Note này gom việc BA; **chưa sửa tài liệu nào** trong lượt ghi note (yêu cầu 03/11: chỉ ghi chú trước, chưa đụng
`02-erd.md`, chưa vẽ lại ERD, chưa sửa Report 3).

## 1. Bot báo gì

- SCRUM-129 (Khách ghi âm theo micro trong phòng họp trực tuyến) đã merge ở #245.
- **7 điểm lệch** so với ticket, ghi đủ ở comment `10418` — **đọc comment này trước khi sửa đặc tả**; note dưới đây chỉ
  chép 3 điểm bot tóm tắt, không thay comment.
- Ba điểm chính:
  1. Khách chỉ được ghi sau khi đã **xác nhận thông báo ghi âm** (`400 MSG380`), và chỉ khi cuộc họp `Online`/`Hybrid`
     đang diễn ra (`409 MSG320`).
  2. Khách **đọc, nghe lại và xoá được câu của mình**; câu của người khác trả `404`.
  3. **Không có mã thông báo mới**; quyết định nằm ở **ADR-0051**.
- Docs cần thêm: `02-erd.md` chưa có bảng `speech_clips` (thiếu từ SCRUM-109).

## 2. Tài liệu đã khớp code (không cần sửa)

- [03-api/12 §Khách](../../03-api/12-ban-ghi-cuoc-hop.md#khách-scrum-129-adr-0051) — bảng endpoint theo token Khách,
  `400 MSG380` / `409 MSG320` / `404`, `speakerIsGuest`, câu gắn với lời mời (`ck_speech_clips_one_speaker`), tệp
  `.txt` ghi "Tên (Khách)", Khách không nhận `speechChanged`.
- [03-api/00 danh mục mã](../../03-api/00-danh-muc-ma-thong-bao.md) — `MSG320` thêm nhánh Khách; `MSG380` dùng lại cho
  câu của Khách khi phiên chưa xác nhận thông báo (cùng một tình huống, đúng quy tắc một mã một tình huống).
- `docs/03-api/09-phong-hop-truc-tuyen.md`, `docs/03-api/16-truy-cap-khach-uc04.md` và `docs/05-decisions/README.md` theo
  cùng PR.

## 3. Còn lệch — việc của BA Final Review

### 3.1 `docs/02-design/02-erd.md` thiếu `speech_clips`

- Bảng có thật từ SCRUM-109 (`SpeechClip`, cấu hình `SpeechClipConfiguration`), SCRUM-129 thêm
  `guest_invitation_id` (null được) cùng ràng buộc `ck_speech_clips_one_speaker` buộc đúng một trong `speaker_id` và
  `guest_invitation_id`.
- `02-erd.md` chưa có ba chỗ: dòng ở bảng **Trạng thái triển khai** (mục từ dòng 31), entity và quan hệ trong **mermaid
  nhóm 7** (dòng 399–492), và câu **"đúng 32 thực thể"** ở dòng 9 cùng ảnh `13-erd/muc-tieu` vẫn tính 32.
- Cùng chỗ đó, `voice_samples` và `voice_consents` (ADR-0027) cũng chưa vẽ — mục "Ranh giới đã chốt" (dòng 747) đang ghi
  *"Bảng mới chưa vẽ vào ERD mục tiêu — BA bổ sung ở BA Final Review"*.
- Sửa đúng cách: thêm `SpeechClip` (và hai bảng Voice ID) thành thực thể của mô hình đích, nâng số đếm 32 → 33/34, ghi rõ
  **hình mục tiêu chưa vẽ** để không ai đọc ảnh thành schema đầy đủ; vẽ lại `13-erd/muc-tieu` và `13-erd/chen` bằng
  [drawio-skill](../../../.agents/skills/drawio-skill/SKILL.md) khi tới bước đó.

### 3.2 Report 3

- **UC-80 (9.3) PRE-1** đang ghi *"A Guest cannot use this function. (BR-355, BR-370)"* — trái ADR-0051: Khách đọc, nghe
  lại và xoá câu của mình khi phiên còn, câu của người khác `404`; hết phiên thì nhờ Thư ký xoá.
- **BR-370** còn rationale *"PO 01/11/2026 … (SCRUM-129, not yet built); until then this rule describes the system as
  built"* — đã merge #245, phải viết lại thành hiện thực: Khách ghi theo micro của mình, vẫn không mở bản ghi phòng và
  bản gỡ băng.
- **BR-355** (*"A person may withdraw their own sentence…"*) nên nêu đích danh Khách, hoặc thêm câu cho Khách.
- **UC-46a / UC-46b và màn 10.1**: câu của Khách hiện tên Khách (tự khai, chưa có thì tên được mời), nhãn "Khách",
  `.txt` ghi "Tên (Khách)"; Khách không nhận `speechChanged`.
- **§I.3.1/I.3.2 Entity Descriptions** chỉ có **30 entity** (thiếu `TranscriptSpeaker`, `GuestSession`, `SpeechClip`,
  `VoiceSample`/`VoiceConsent`) — làm cùng đợt với `02-erd.md`, không tách rời.
- **V.2 `MSG380`** hiện chỉ ghi ngữ cảnh UC-37/EX-9; nếu muốn đặc tả nêu cả câu của Khách thì thêm vào cột ngữ cảnh
  (vẫn là cùng một tình huống, không phải mã mới).

### 3.3 Jira

- Sau khi tài liệu vào `main`, comment kết quả vào SCRUM-129 theo
  [DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update); nếu bảng ERD
  được coi là phần của SCRUM-109 thì comment cả SCRUM-109.

## 4. Thứ tự đề xuất khi làm

1. `02-erd.md`: thêm `SpeechClip` (+ Voice ID) vào bảng trạng thái và mermaid nhóm 7, sửa số đếm, ghi rõ hình chưa vẽ.
2. Report 3: UC-80, BR-355, BR-370, UC-46a/46b, màn 10.1; rồi §I.3.1/I.3.2 và Change Log.
3. Vẽ lại ERD mục tiêu (`13-erd/muc-tieu`) và bản `chen`, xuất PNG theo drawio-skill.
4. Comment Jira.

## 5. Câu hỏi còn mở

- **ADR-0051** vẫn ghi trạng thái "Đề xuất" dù PO đã chốt 01/11/2026 và code đã merge — có chuyển "Đã chấp nhận" không?
- **BR-355** có cần câu riêng cho Khách (tự xoá câu khi phiên còn, sau đó nhờ Thư ký) hay coi Khách nằm trong "a person"?
- Bốn điểm lệch còn lại trong comment `10418` (ngoài ba điểm bot tóm tắt) có cần vào note này không — BA đọc và bổ sung.
