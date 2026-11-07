# Đặc tả sơ bộ SCRUM-30 (ghi chú thư ký)

**Ngày:** 2026-11-06, sửa theo review PR #271 ngày 07/11/2026 · **Người viết:** BE (Huy Nguyễn Trường) ·
**Trạng thái:** sơ bộ, đủ để BE làm theo

| Jira | Tên | Trạng thái lúc viết |
|---|---|---|
| [SCRUM-30](https://sep490g116.atlassian.net/browse/SCRUM-30) | Ghi chú thời gian thực cho thư ký | To Do, Sprint 3 |

Đây là bước *BE: basic UC Specification + Sequence Diagram* của
[DEVELOPMENT_WORKFLOW.md §2](../DEVELOPMENT_WORKFLOW.md). Note này chưa phải Report 3: UC, BR ở đây là
**đề xuất**. BA đưa vào DOCX ở `08-reports/source/` lúc BA Final Review, rồi sinh lại
`05-use-case-specifications.md` và `06-business-rules.md`. Không chép tay sang hai file đó.

- Mã `MSG415`–`MSG423` đã **dành sẵn** trong [danh mục](../03-api/00-danh-muc-ma-thong-bao.md) cùng commit với note này.
  Bản đầu của note dùng `MSG406`–`MSG414`, rồi `MSG414`–`MSG422`; `main` đã cấp `MSG406`–`MSG414` cho việc khác
  (`MSG414` cho giới hạn số lần thử đăng nhập, SCRUM-132) nên cả dải dời lên.
- Số BR-395 → BR-403 là số đề xuất, nối tiếp BR-394. BA giữ hoặc đổi số khi đưa vào Report 3.
- Số UC mới UC-82 nối tiếp UC-81. Không liên quan tới UC-82 **cũ** ở bảng đổi số ngày 10/10/2026 (đã đổi thành
  UC-63), giống cách UC-78 → UC-81 đã làm.

## 1. Hiện trạng code (06/11/2026)

| Phần | Đã có | Chưa có |
|---|---|---|
| Ghi chú | Bảng `meeting_notes`: một ghi chú cho mỗi người viết ở mỗi mục nghị sự, `AgendaItemId = null` là ghi chú chung; có `UpdatedAt` | Endpoint, quyền, sự kiện hub, FE, việc đưa ghi chú vào biên bản |
| Mục đang bàn | `AgendaItemStatus.InProgress`, sự kiện `agendaChanged` trên `/hubs/meetings` (UC-24) | — |
| Bản nháp biên bản (SCRUM-32) | UC-58, UC-59, UC-60 chạy thật. Từ #263, UC-59 dựng biên bản từ dự thảo AI hiện có, dù đã duyệt hay chưa | Chỗ đưa ghi chú vào bản nháp (BR-402) |

## 2. Chỗ lệch khỏi ticket, cần ghi lên Jira

| # | Ticket | Đặc tả này | Lý do |
|---|---|---|---|
| D1 | SCRUM-30: "Đánh dấu nhiệm vụ giao cho ai" | Chỉ có nhãn chữ *Nhiệm vụ* ở đầu dòng. Không lưu người được giao, hạn hay tiến độ | README, BR-392: TrustCabinet không theo dõi nhiệm vụ |
| D2 | SCRUM-30 không nói gì về mục nghị sự bị xoá | Xoá một mục `Pending` có ghi chú thì ghi chú đó được nối vào ghi chú chung của cùng người viết (`<tên mục>:` + nội dung), không mất chữ | Khoá ngoại `SET NULL` đụng unique một ghi chú chung mỗi người viết, làm xoá mục hỏng với `503 MSG70`. Phát hiện lúc làm BE SCRUM-30 |

## 3. Use Case đề xuất

### UC-82 – Take Meeting Notes (Ghi chú cuộc họp) — SCRUM-30

Thuộc FE-05, `<<extend>>` UC-26. Sequence Diagram:
[UC82_TakeMeetingNotes.txt](../06-sequencediagram/agenda-management/UC82_TakeMeetingNotes.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | Chair (reads the notes) |
| **Description** | Allows the current Secretary to type running notes during the sitting, each attached to the agenda item under discussion or to the Meeting as a whole, so that the minutes are written from what was noted at the time rather than from memory. (BR-395, BR-397) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Secretary of the Meeting. (BR-395)<br>● PRE-2: The Meeting is InProgress, or it has Ended and no minutes draft has been built yet. (BR-396) |
| **Postconditions** | ● POST-1: The note of the chosen agenda item, or of the Meeting as a whole, holds the text the actor typed, with the time it was saved. (BR-398)<br>● POST-2: Text that could not reach the system yet stays on the actor's device until it is sent; for a Secret Meeting, only until the page is closed. (BR-403)<br>● POST-3: No agenda item, conclusion, minutes or other Meeting record is changed. |
| **Normal Flow** | Take Meeting Notes<br>1. During the sitting, the actor opens the Ghi chú panel — on the Meeting page or inside the online room, without leaving the call.<br>2. The system loads the actor's notes for the Meeting and opens the note of the agenda item that is InProgress. (BR-397)<br>3. The actor types. A line may start with a mark — Ý kiến, Kết luận or Nhiệm vụ. (BR-401)<br>4. Shortly after the actor stops typing, the system checks that the actor is still the current Secretary, that the Meeting still accepts notes, that the item still belongs to the Meeting, that the text is within 20,000 characters and that the note was not saved elsewhere since the actor read it. (BR-395, BR-396, BR-399, BR-400)<br>5. The system saves the note and shows the time it was saved: MSG415 – “Note saved.” |
| **Alternative Flows** | AF-1 – Note on another item or on the Meeting as a whole<br>1. At Step 2 or Step 3, the actor selects another agenda item, or “Chung” [General].<br>2. The system opens the actor's note of that choice. (BR-397, BR-398)<br>3. The flow resumes at Step 3 of the Main Flow.<br>AF-2 – The Chair moves to another item<br>1. After Step 2, the Chair sets another agenda item as current (UC-24).<br>2. The system keeps the text already typed with the earlier item and opens the note of the new current item for what is typed next. (BR-397)<br>3. The flow resumes at Step 3 of the Main Flow.<br>AF-3 – The connection is lost<br>1. At Step 4, the text cannot reach the system.<br>2. The system keeps the text on the actor's device and displays MSG423 – “You are offline. The note is kept on this device and will be saved when the connection returns.” (BR-403)<br>3. When the connection returns, the flow resumes at Step 4 of the Main Flow.<br>AF-4 – The Chair reads the notes<br>1. At Step 1, the actor is the current Chair.<br>2. The system shows the current Secretary's notes read-only. (BR-395)<br>3. The use case ends successfully. |
| **Exception Flows** | EX-1 – Actor is not the current Secretary<br>1. At Step 4, the actor is no longer the current Secretary. (BR-395)<br>2. Nothing is saved; the typed text stays in the panel so it can be copied. The system displays MSG416 – “Only the Secretary may write meeting notes.”<br>3. The use case ends.<br>EX-2 – The Meeting no longer accepts notes<br>1. At Step 4, the Meeting is not InProgress, or it has Ended and its minutes draft has been built. (BR-396)<br>2. Nothing is saved; the typed text stays in the panel. The system displays MSG417 – “Notes can be written only while the meeting is in progress, or after it has ended until the minutes draft is created.”<br>3. The use case ends.<br>EX-3 – The note was changed in another window<br>1. At Step 4, the note was saved from another window or device after the actor read it. (BR-399)<br>2. Nothing is overwritten. The system shows the latest text next to the actor's unsaved text and displays MSG418 – “This note was changed in another window. Reload to see the latest version.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-4 – The note is too long<br>1. At Step 4, the text is longer than 20,000 characters. (BR-400)<br>2. Nothing is saved. The system displays MSG419 – “The note is too long. A note can hold up to 20,000 characters.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-5 – The agenda item was removed<br>1. At Step 4, the item the note belongs to is no longer part of the Meeting.<br>2. Nothing is saved; the typed text stays in the panel. The system displays MSG420 – “The selected agenda item is no longer part of this meeting.”<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-6 – The notes cannot be loaded<br>1. At Step 2, the system cannot read the notes.<br>2. Nothing is shown. The system displays MSG421 – “Unable to load the meeting notes.”<br>3. The use case ends.<br>EX-7 – The note cannot be saved<br>1. At Step 5, the system cannot save the note.<br>2. The text stays on the actor's device. The system displays MSG422 – “Unable to save the note. It is kept on this device and will be sent again.” (BR-403)<br>3. The flow resumes at Step 4 of the Main Flow.<br>EX-8 – The Meeting is not accessible<br>1. At Step 2, the Meeting is unavailable, or the actor may read it but is neither its current Chair nor its current Secretary.<br>2. The system answers as if the notes did not exist and discloses nothing about them. (BR-395)<br>3. The use case ends. |

### Thay đổi ở các UC đã đặc tả

| UC | Thay đổi |
|---|---|
| UC-59 Generate Meeting Minutes | Bước 4: khi cuộc họp chưa có dự thảo AI, ghi chú của Thư ký điền vào mục II dưới từng mục nghị sự, các dòng *Kết luận* vào mục III, các dòng *Nhiệm vụ* vào dòng "Nhiệm vụ, sản phẩm và thời hạn" (BR-402) |
| UC-24 Set Current Agenda Item | Ghi chú mới của Thư ký chuyển sang mục vừa đặt (UC-82 AF-2) |

## 4. Business Rules đề xuất

| BR | Rule Statement | UC | Nguồn |
|---|---|---|---|
| `BR-395` | Only the current Secretary of a Meeting may write its meeting notes. The current Chair may read them; nobody else, Guests included, can read them. | UC-82 | SCRUM-30 — the notes are the Secretary's working material |
| `BR-396` | Meeting notes may be written while the Meeting is InProgress, and after it has Ended until its minutes draft is built. After that the notes are read-only. | UC-82 | SCRUM-30 / BR-310 — the same cut-off as item conclusions |
| `BR-397` | A meeting note belongs to one agenda item of the Meeting or to the Meeting as a whole. While an item is InProgress, what the Secretary types goes to that item unless the Secretary chooses another. | UC-82, UC-24 | SCRUM-30 — "gắn vào mục nghị sự đang bàn" |
| `BR-398` | Each author has at most one note per agenda item and one note for the Meeting as a whole. | UC-82 | Data model SCRUM-30 |
| `BR-399` | A note save made from an out-of-date copy is rejected; newer text is never overwritten by an older one, whichever window or device it comes from. | UC-82 | SCRUM-30 bổ sung 30/09/2026 — the room panel and a new tab open together |
| `BR-400` | A meeting note holds at most 20,000 characters. An empty note is allowed. | UC-82 | Data model SCRUM-30 |
| `BR-401` | A line of a note may start with the mark Ý kiến, Kết luận or Nhiệm vụ. A mark is text only; the system tracks no assignee, due date or progress. | UC-82 | SCRUM-30 / BR-392 — no task tracking |
| `BR-402` | When the minutes draft is built and the Meeting has no AI draft, each item's notes are written under that item in section II, lines marked Kết luận are added to section III, and lines marked Nhiệm vụ are added to “Nhiệm vụ, sản phẩm và thời hạn”. | UC-59 | SCRUM-30 — "ghi chú chảy thẳng vào bản nháp biên bản"; từ #263 bản nháp dựng từ dự thảo AI hiện có, dù đã duyệt hay chưa |
| `BR-403` | Text typed into a note that has not reached the system yet is kept on the device it was typed on and sent when the connection returns. For a Secret Meeting it is kept only until the page is closed. Either way it is removed from the device once saved and when the actor leaves the Meeting. | UC-82 | SCRUM-30 — "mất mạng rồi vào lại không mất nội dung"; review #271 — máy dùng chung không được giữ lại ghi chú của cuộc họp Mật |

## 5. Mã thông báo (dành sẵn)

| Mã | HTTP dự kiến | UC | Thông điệp đặc tả | Câu tiếng Việt cho UI |
|---|---|---|---|---|
| `MSG415` | 200 | UC-82 Main Flow | *Note saved.* | `Đã lưu ghi chú.` |
| `MSG416` | 403 | UC-82 EX-1 | *Only the Secretary may write meeting notes.* | `Chỉ Thư ký được ghi chú cuộc họp.` |
| `MSG417` | 409 | UC-82 EX-2 | *Notes can be written only while the meeting is in progress, or after it has ended until the minutes draft is created.* | `Chỉ ghi chú khi cuộc họp đang diễn ra, hoặc sau khi kết thúc mà chưa tạo bản nháp biên bản.` |
| `MSG418` | 409 | UC-82 EX-3 | *This note was changed in another window. Reload to see the latest version.* | `Ghi chú vừa được sửa ở cửa sổ khác. Tải lại để xem bản mới nhất.` |
| `MSG419` | 400 | UC-82 EX-4 | *The note is too long. A note can hold up to 20,000 characters.* | `Ghi chú quá dài, tối đa 20.000 ký tự.` |
| `MSG420` | 409 | UC-82 EX-5 | *The selected agenda item is no longer part of this meeting.* | `Mục nghị sự này không còn trong cuộc họp.` |
| `MSG421` | 500 | UC-82 EX-6 | *Unable to load the meeting notes.* | `Không tải được ghi chú.` |
| `MSG422` | 503 | UC-82 EX-7 | *Unable to save the note. It is kept on this device and will be sent again.* | `Không lưu được ghi chú. Nội dung vẫn giữ trên máy này và sẽ gửi lại.` |
| `MSG423` | FE | UC-82 AF-3 | *You are offline. The note is kept on this device and will be saved when the connection returns.* | `Mất kết nối. Ghi chú vẫn giữ trên máy này và sẽ lưu khi có mạng lại.` |

## 6. API dự kiến (cho BE, chưa phải contract)

Contract chính thức viết vào `docs/03-api/` trong PR code. Mọi endpoint kiểm tra lại quyền ở server. Người không phải
Chủ toạ/Thư ký hỏi tới ghi chú thì nhận `404`, không có mã.

| Method | Đường dẫn | Ghi chú |
|---|---|---|
| `GET` | `/api/meetings/{id}/notes` | `{ notes: [{ agendaItemId \| null, body, updatedAt, author }], currentAgendaItemId, canWrite }`. Chủ toạ thấy ghi chú của Thư ký hiện tại, `canWrite: false` |
| `PUT` | `/api/meetings/{id}/notes` | Body `{ agendaItemId \| null, body, updatedAt \| null }`. `updatedAt: null` nghĩa là ghi chú mới. `200 MSG415`, `409 MSG418` khi `updatedAt` lệch |

- Không cần migration: `meeting_notes` đã có unique `(MeetingId, AgendaItemId, AuthorId)` và `UpdatedAt` của `Entity`.
- Hub `/hubs/meetings` phát `notesChanged { meetingId, agendaItemId, updatedAt }` tới kết nối của Chủ toạ và Thư ký
  hiện tại, để panel trong phòng và tab mới thấy nhau (BR-399). Không gửi nội dung qua hub; client gọi lại `GET`.
- FE tự lưu sau khoảng 1 giây ngừng gõ và giữ nháp chưa gửi theo `meetingId` + `agendaItemId` (BR-403):
  - cuộc họp Thường: trong `localStorage`, để mất mạng hay đóng tab rồi vào lại vẫn còn;
  - cuộc họp Mật: chỉ trong bộ nhớ của trang hoặc `sessionStorage`, không bao giờ `localStorage`, để nội dung không nằm
    lại trên máy dùng chung;
  - xoá nháp khi server trả `200` và khi rời cuộc họp; xoá mọi nháp ghi chú khi đăng xuất. Ghi chú là dữ liệu cá nhân
    của cuộc họp.
- `MinutesService` (UC-59) đọc `MeetingNotes` của Thư ký hiện tại khi dựng bản nháp mà cuộc họp chưa có dự thảo AI
  (BR-402).

## 7. Câu hỏi cần PO / BA chốt

| # | Câu hỏi | Đề xuất trong note |
|---|---|---|
| Q1 | Dự thảo AI (UC-47) có đọc ghi chú của Thư ký không, hay chỉ bản gỡ băng? | Chưa đọc. BR-402 chỉ áp dụng khi cuộc họp chưa có dự thảo AI. Đưa ghi chú vào AI là sửa ADR-0054 |
| Q2 | Ngoài Thư ký, Chủ toạ có được tự ghi chú riêng không? | Không. Bảng cho phép mỗi người một ghi chú, nhưng ticket chỉ nói thư ký |

## 8. Việc còn lại

- [ ] Ghi D1 lên SCRUM-30 bằng comment (skill `jira-deviation-note`).
- [ ] Xuất PNG cho `UC82_TakeMeetingNotes.txt` (`node scripts/export-diagrams.js …`) và thêm tab vào
      `TrustCabinet_Sequence_Diagrams.drawio` — để lúc BA Final Review nếu đặc tả còn đổi.
- [ ] BA: đưa UC-82 và BR-395 → BR-403 vào Report 3 DOCX; cập nhật inventory và UC diagram.
- [ ] Ghi D2 lên SCRUM-30 bằng comment.
- [x] BE (07/11/2026): `GET`/`PUT /api/meetings/{id}/notes`, sự kiện `notesChanged`, ghi chú vào bản nháp biên bản
      (BR-402), gộp ghi chú khi xoá mục (D2); contract [19-ghi-chu-cuoc-hop.md](../03-api/19-ghi-chu-cuoc-hop.md);
      `MSG415`–`MSG422` sang *BE*.
- [x] FE (07/11/2026): `NotesPanel` ở tab Ghi chú và trong phòng họp; nháp chưa gửi giữ trong `localStorage` (cuộc họp
      Thường) hoặc `sessionStorage` (cuộc họp Mật), xoá khi lưu xong, khi rời cuộc họp và khi đăng xuất (BR-403); `MSG423`.
