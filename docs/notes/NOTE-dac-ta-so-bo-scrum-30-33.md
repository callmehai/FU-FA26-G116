# Đặc tả sơ bộ SCRUM-30 (ghi chú thư ký) và SCRUM-33 (duyệt, ban hành biên bản)

**Ngày:** 2026-11-06 · **Người viết:** BE (Huy Nguyễn Trường) · **Trạng thái:** sơ bộ, đủ để BE làm theo

| Jira | Tên | Trạng thái lúc viết |
|---|---|---|
| [SCRUM-30](https://sep490g116.atlassian.net/browse/SCRUM-30) | Ghi chú thời gian thực cho thư ký | To Do, Sprint 3 |
| [SCRUM-33](https://sep490g116.atlassian.net/browse/SCRUM-33) | Phê duyệt và ban hành biên bản | To Do, Sprint 3 |

Đây là bước *BE: basic UC Specification + Sequence Diagram* của
[DEVELOPMENT_WORKFLOW.md §2](../DEVELOPMENT_WORKFLOW.md). Note này chưa phải Report 3: UC, BR ở đây là
**đề xuất**. BA đưa vào DOCX ở `08-reports/source/` lúc BA Final Review, rồi sinh lại
`05-use-case-specifications.md` và `06-business-rules.md`. Không chép tay sang hai file đó.

- Mã `MSG414`–`MSG433` đã **dành sẵn** trong [danh mục](../03-api/00-danh-muc-ma-thong-bao.md) cùng commit với note này.
- Số BR-395 → BR-413 là số đề xuất, nối tiếp BR-394. BA giữ hoặc đổi số khi đưa vào Report 3.
- Số UC mới (UC-82, UC-83) nối tiếp UC-81. Không liên quan tới UC-82, UC-83 **cũ** ở bảng đổi số ngày
  10/10/2026 (đã đổi thành UC-63, UC-64), giống cách UC-78 → UC-81 đã làm.

## 1. Hiện trạng code (06/11/2026)

| Phần | Đã có | Chưa có |
|---|---|---|
| Ghi chú (SCRUM-30) | Bảng `meeting_notes`: một ghi chú cho mỗi người viết ở mỗi mục nghị sự, `AgendaItemId = null` là ghi chú chung; có `UpdatedAt` | Endpoint, quyền, sự kiện hub, FE, việc đưa ghi chú vào biên bản |
| Mục đang bàn | `AgendaItemStatus.InProgress`, sự kiện `agendaChanged` trên `/hubs/meetings` (UC-24) | — |
| Biên bản (SCRUM-33) | `MeetingMinutes` có `RevisionNumber`, `SupersedesId`, `SubmittedAt`, `ApprovedById/At`, `RejectionReason`, `IssuedAt`; `MinutesStatus` = `Draft, WaitingApproval, Rejected, Issued, Superseded`; loại thông báo `MinutesWaitingApproval`, `MinutesIssued` | Endpoint gửi duyệt, duyệt, trả lại, đính chính; người khác xem bản đã ban hành; thông báo |
| Bản nháp (SCRUM-32) | UC-58, UC-59, UC-60 chạy thật; `MSG400` đã chặn sửa khi biên bản không còn là nháp | — |

## 2. Chỗ lệch khỏi ticket, cần ghi lên Jira

| # | Ticket | Đặc tả này | Lý do |
|---|---|---|---|
| D1 | SCRUM-33: "Ban hành chuyển cuộc họp sang trạng thái `MinutesIssued`" | Cuộc họp giữ `Ended`. Trạng thái "đã ban hành" nằm ở `MeetingMinutes.Status = Issued` | [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md) đã bỏ `MinutesIssued` khỏi vòng đời cuộc họp (PO chốt Q10 ngày 10/10/2026) |
| D2 | SCRUM-30: "Đánh dấu nhiệm vụ giao cho ai" | Chỉ có nhãn chữ *Nhiệm vụ* ở đầu dòng. Không lưu người được giao, hạn hay tiến độ | README, BR-392: TrustCabinet không theo dõi nhiệm vụ |
| D3 | SCRUM-33 chỉ liệt kê `submit`, `approve`, `reject` | Thêm thao tác tạo bản đính chính (UC-83) | Ticket yêu cầu "sửa sau ban hành phải tạo bản đính chính mới" nhưng không có endpoint nào làm việc đó |
| D4 | Inventory: UC-61 *Gửi ký*, UC-64 *Hoàn tất / niêm phong* (Secretary), UC-65 *Yêu cầu sửa* (người ký) | UC-61 *Gửi duyệt*, UC-64 *Duyệt và ban hành* (Chair), UC-65 *Trả lại* (Chair). UC-62, UC-63 giữ cho ký số mô phỏng (SCRUM-37) | Đều là placeholder, chưa có code. SCRUM-33 là duyệt của Chủ toạ, còn ký là việc riêng của SCRUM-37 (Could). Xem câu hỏi Q4 |

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
| **Normal Flow** | Take Meeting Notes<br>1. During the sitting, the actor opens the Ghi chú panel — on the Meeting page or inside the online room, without leaving the call.<br>2. The system loads the actor's notes for the Meeting and opens the note of the agenda item that is InProgress. (BR-397)<br>3. The actor types. A line may start with a mark — Ý kiến, Kết luận or Nhiệm vụ. (BR-401)<br>4. Shortly after the actor stops typing, the system checks that the actor is still the current Secretary, that the Meeting still accepts notes, that the item still belongs to the Meeting, that the text is within 20,000 characters and that the note was not saved elsewhere since the actor read it. (BR-395, BR-396, BR-399, BR-400)<br>5. The system saves the note and shows the time it was saved: MSG414 – “Note saved.” |
| **Alternative Flows** | AF-1 – Note on another item or on the Meeting as a whole<br>1. At Step 2 or Step 3, the actor selects another agenda item, or “Chung” [General].<br>2. The system opens the actor's note of that choice. (BR-397, BR-398)<br>3. The flow resumes at Step 3 of the Main Flow.<br>AF-2 – The Chair moves to another item<br>1. After Step 2, the Chair sets another agenda item as current (UC-24).<br>2. The system keeps the text already typed with the earlier item and opens the note of the new current item for what is typed next. (BR-397)<br>3. The flow resumes at Step 3 of the Main Flow.<br>AF-3 – The connection is lost<br>1. At Step 4, the text cannot reach the system.<br>2. The system keeps the text on the actor's device and displays MSG422 – “You are offline. The note is kept on this device and will be saved when the connection returns.” (BR-403)<br>3. When the connection returns, the flow resumes at Step 4 of the Main Flow.<br>AF-4 – The Chair reads the notes<br>1. At Step 1, the actor is the current Chair.<br>2. The system shows the current Secretary's notes read-only. (BR-395)<br>3. The use case ends successfully. |
| **Exception Flows** | EX-1 – Actor is not the current Secretary<br>1. At Step 4, the actor is no longer the current Secretary. (BR-395)<br>2. Nothing is saved; the typed text stays in the panel so it can be copied. The system displays MSG415 – “Only the Secretary may write meeting notes.”<br>3. The use case ends.<br>EX-2 – The Meeting no longer accepts notes<br>1. At Step 4, the Meeting is not InProgress, or it has Ended and its minutes draft has been built. (BR-396)<br>2. Nothing is saved; the typed text stays in the panel. The system displays MSG416 – “Notes can be written only while the meeting is in progress, or after it has ended until the minutes draft is created.”<br>3. The use case ends.<br>EX-3 – The note was changed in another window<br>1. At Step 4, the note was saved from another window or device after the actor read it. (BR-399)<br>2. Nothing is overwritten. The system shows the latest text next to the actor's unsaved text and displays MSG417 – “This note was changed in another window. Reload to see the latest version.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-4 – The note is too long<br>1. At Step 4, the text is longer than 20,000 characters. (BR-400)<br>2. Nothing is saved. The system displays MSG418 – “The note is too long. A note can hold up to 20,000 characters.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-5 – The agenda item was removed<br>1. At Step 4, the item the note belongs to is no longer part of the Meeting.<br>2. Nothing is saved; the typed text stays in the panel. The system displays MSG419 – “The selected agenda item is no longer part of this meeting.”<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-6 – The notes cannot be loaded<br>1. At Step 2, the system cannot read the notes.<br>2. Nothing is shown. The system displays MSG420 – “Unable to load the meeting notes.”<br>3. The use case ends.<br>EX-7 – The note cannot be saved<br>1. At Step 5, the system cannot save the note.<br>2. The text stays on the actor's device. The system displays MSG421 – “Unable to save the note. It is kept on this device and will be sent again.” (BR-403)<br>3. The flow resumes at Step 4 of the Main Flow.<br>EX-8 – The Meeting is not accessible<br>1. At Step 2, the Meeting is unavailable, or the actor may read it but is neither its current Chair nor its current Secretary.<br>2. The system answers as if the notes did not exist and discloses nothing about them. (BR-395)<br>3. The use case ends. |

### UC-61 – Submit Meeting Minutes for Approval (Gửi duyệt biên bản) — SCRUM-33

Thay placeholder *Submit Meeting Minutes for Signing*. `<<extend>>` UC-58. Sequence Diagram:
[UC61_SubmitMeetingMinutesForApproval.txt](../06-sequencediagram/minutes-management/UC61_SubmitMeetingMinutesForApproval.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | Chair (notified) |
| **Description** | Allows the current Secretary to send the finished minutes draft to the current Chair for approval. From then on nobody can correct or rebuild it until the Chair approves or returns it. (BR-404, BR-388) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Secretary of the Meeting. (BR-404)<br>● PRE-2: The Meeting has Ended. (BR-405)<br>● PRE-3: The latest revision of the minutes is a draft, or was returned by the Chair. (BR-407) |
| **Postconditions** | ● POST-1: The revision is waiting for approval, with who submitted it and when.<br>● POST-2: The revision can no longer be corrected or rebuilt. (BR-388)<br>● POST-3: The current Chair is notified. (BR-411)<br>● POST-4: The Meeting status is unchanged. |
| **Normal Flow** | Submit Meeting Minutes for Approval<br>1. On the Biên bản tab, the actor selects “Gửi duyệt” [Submit for approval] and confirms.<br>2. The system checks the assignment, that the Meeting has Ended and that the latest revision is a draft or was returned, and that it is the copy the actor read. (BR-404, BR-405, BR-389)<br>3. The system marks the revision as waiting for approval and records who submitted it and when.<br>4. The system notifies the current Chair. (BR-411)<br>5. The system displays MSG423 – “Minutes submitted to the Chair for approval.” |
| **Alternative Flows** | AF-1 – Places are still left blank<br>1. At Step 1, the text still holds “……” where the Secretary was to write.<br>2. The system warns that some places are still blank and asks for confirmation.<br>3. The actor confirms and the flow resumes at Step 2 of the Main Flow.<br>AF-2 – Submit again after the Chair returned the minutes<br>1. At Step 1, the latest revision was returned; the system shows the Chair's reason above the paper. (BR-407)<br>2. The actor corrects the text (UC-60) or submits it unchanged.<br>3. The flow resumes at Step 2 of the Main Flow. |
| **Exception Flows** | EX-1 – Actor is not the current Secretary<br>1. At Step 2, the actor is not the current Secretary. (BR-404)<br>2. The minutes stay as they are. The system displays MSG424 – “Only the Secretary may submit the minutes for approval.”<br>3. The use case ends.<br>EX-2 – The Meeting has not ended<br>1. At Step 2, the Meeting is still InProgress. (BR-405)<br>2. The minutes stay a draft. The system displays MSG425 – “The minutes can be submitted only after the meeting has ended.”<br>3. The use case ends.<br>EX-3 – The minutes were already submitted or issued<br>1. At Step 2, the latest revision is waiting for approval or has been issued. (BR-388)<br>2. Nothing changes. The system displays MSG400 – “The minutes have been submitted or issued and can no longer be changed.”<br>3. The use case ends.<br>EX-4 – Someone saved after the actor read the minutes<br>1. At Step 2, the draft was saved by another person after the actor read it. (BR-389)<br>2. Nothing is submitted. The system displays MSG401 – “The minutes were just changed by someone else. Reload to see the latest version.”<br>3. The flow resumes at Step 1 of the Main Flow.<br>EX-5 – The change cannot be saved<br>1. At Step 3, the system cannot save the new state.<br>2. The minutes stay a draft and nobody is notified. The system displays MSG404 – “Unable to save the minutes. Please try again.”<br>3. The use case ends.<br>EX-6 – The Meeting is not accessible<br>1. At Step 2, the Meeting is unavailable or the actor may no longer read it.<br>2. The system answers as if the Meeting did not exist and discloses nothing about its minutes.<br>3. The use case ends. |

### UC-64 – Approve and Issue Meeting Minutes (Duyệt và ban hành biên bản) — SCRUM-33

Thay placeholder *Finalize Meeting Minutes*; actor đổi từ Secretary sang Chair. `<<extend>>` UC-58. Sequence Diagram:
[UC64_ApproveAndIssueMeetingMinutes.txt](../06-sequencediagram/minutes-management/UC64_ApproveAndIssueMeetingMinutes.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | Secretary, every person who may read the issued minutes (notified) |
| **Description** | Allows the current Chair to approve the minutes waiting for approval. Approval issues them at once: they are locked, and the people of the Meeting are notified and can read them. (BR-406, BR-408) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-406)<br>● PRE-2: The latest revision of the minutes is waiting for approval. |
| **Postconditions** | ● POST-1: The revision is issued, with who approved it and when, and can never be changed again. (BR-408, BR-409)<br>● POST-2: When the revision corrects an earlier issued one, the earlier one is marked superseded and stays readable. (BR-409)<br>● POST-3: Everyone who may read the issued minutes is notified. (BR-411, BR-412)<br>● POST-4: The Meeting stays Ended. (BR-408) |
| **Normal Flow** | Approve and Issue Meeting Minutes<br>1. The actor opens the notification or the Biên bản tab and reads the minutes as the paper that will be printed.<br>2. The actor selects “Duyệt và ban hành” [Approve and issue] and confirms.<br>3. The system checks the assignment, that the revision is still waiting for approval and that it is the copy the actor read. (BR-406, BR-389)<br>4. The system marks the revision as issued and records who approved it and when. (BR-408)<br>5. The system notifies the people who may read the issued minutes. (BR-411, BR-412)<br>6. The system displays MSG426 – “Minutes approved and issued.” |
| **Alternative Flows** | AF-1 – Issue a correction<br>1. At Step 4, the revision corrects an earlier issued revision.<br>2. The system marks the earlier revision as superseded; it stays readable in the history of the minutes. (BR-409)<br>3. The flow resumes at Step 5 of the Main Flow. |
| **Exception Flows** | EX-1 – Actor is not the current Chair<br>1. At Step 3, the actor is not the current Chair. (BR-406)<br>2. The minutes stay as they are. The system displays MSG427 – “Only the Chair may approve or return the minutes.”<br>3. The use case ends.<br>EX-2 – The minutes are not waiting for approval<br>1. At Step 3, the revision is a draft, was returned, or has already been issued.<br>2. Nothing changes. The system displays MSG428 – “These minutes are not waiting for approval.”<br>3. The use case ends.<br>EX-3 – The minutes changed after the actor opened them<br>1. At Step 3, the revision is not the copy the actor read. (BR-389)<br>2. Nothing is issued. The system displays MSG401 – “The minutes were just changed by someone else. Reload to see the latest version.”<br>3. The flow resumes at Step 1 of the Main Flow.<br>EX-4 – The change cannot be saved<br>1. At Step 4, the system cannot save the new state.<br>2. The minutes stay waiting for approval and nobody is notified. The system displays MSG404 – “Unable to save the minutes. Please try again.”<br>3. The use case ends.<br>EX-5 – The Meeting is not accessible<br>1. At Step 3, the Meeting is unavailable or the actor may no longer read it.<br>2. The system answers as if the Meeting did not exist and discloses nothing about its minutes.<br>3. The use case ends. |

### UC-65 – Return Meeting Minutes for Revision (Trả lại biên bản) — SCRUM-33

Thay placeholder *Request Revision of Meeting Minutes*; actor đổi từ người ký sang Chair. `<<extend>>` UC-58. Sequence Diagram:
[UC65_ReturnMeetingMinutesForRevision.txt](../06-sequencediagram/minutes-management/UC65_ReturnMeetingMinutesForRevision.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | Secretary (notified) |
| **Description** | Allows the current Chair to send the minutes waiting for approval back to the Secretary with a reason, so the Secretary can correct them and submit them again. (BR-406, BR-407) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-406)<br>● PRE-2: The latest revision of the minutes is waiting for approval. |
| **Postconditions** | ● POST-1: The revision is returned, with the reason, who returned it and when. (BR-407)<br>● POST-2: The Chair and the Secretary can correct the revision again and the Secretary can submit it again. (BR-407)<br>● POST-3: The current Secretary is notified. (BR-411) |
| **Normal Flow** | Return Meeting Minutes for Revision<br>1. On the Biên bản tab, the actor selects “Trả lại” [Return].<br>2. The system asks for the reason; the actor enters it and confirms.<br>3. The system checks the assignment, that the revision is still waiting for approval and that the reason is not empty and within 2,000 characters. (BR-406, BR-407)<br>4. The system marks the revision as returned and records the reason, who returned it and when.<br>5. The system notifies the current Secretary. (BR-411)<br>6. The system displays MSG429 – “Minutes returned to the Secretary.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is not the current Chair<br>1. At Step 3, the actor is not the current Chair. (BR-406)<br>2. The minutes stay as they are. The system displays MSG427 – “Only the Chair may approve or return the minutes.”<br>3. The use case ends.<br>EX-2 – The minutes are not waiting for approval<br>1. At Step 3, the revision is a draft, was returned, or has been issued.<br>2. Nothing changes. The system displays MSG428 – “These minutes are not waiting for approval.”<br>3. The use case ends.<br>EX-3 – The reason is missing or too long<br>1. At Step 3, the reason is empty or longer than 2,000 characters. (BR-407)<br>2. Nothing changes. The system displays MSG430 – “Enter a reason for returning the minutes, up to 2,000 characters.”<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-4 – The change cannot be saved<br>1. At Step 4, the system cannot save the new state.<br>2. The minutes stay waiting for approval and nobody is notified. The system displays MSG404 – “Unable to save the minutes. Please try again.”<br>3. The use case ends.<br>EX-5 – The Meeting is not accessible<br>1. At Step 3, the Meeting is unavailable or the actor may no longer read it.<br>2. The system answers as if the Meeting did not exist and discloses nothing about its minutes.<br>3. The use case ends. |

### UC-83 – Correct Issued Meeting Minutes (Tạo bản đính chính biên bản) — SCRUM-33

UC mới, `<<extend>>` UC-58. Sequence Diagram:
[UC83_CorrectIssuedMeetingMinutes.txt](../06-sequencediagram/minutes-management/UC83_CorrectIssuedMeetingMinutes.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to start a correction of issued minutes. The correction is a new revision that starts from the issued text; the issued revision is never changed and stays the one people read until the correction is issued. (BR-409, BR-410) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-386)<br>● PRE-2: The latest revision of the minutes is issued. |
| **Postconditions** | ● POST-1: A new draft revision exists, numbered after the issued one, holding the issued text and pointing at the revision it corrects. (BR-409)<br>● POST-2: The issued revision is unchanged and stays the one other people read. (BR-409)<br>● POST-3: Nobody is notified until the correction is issued (UC-64). |
| **Normal Flow** | Correct Issued Meeting Minutes<br>1. On the Biên bản tab, the actor selects “Tạo bản đính chính” [Create correction] and confirms.<br>2. The system checks the assignment, that the latest revision is issued and that no correction is already in progress. (BR-386, BR-410)<br>3. The system creates the new draft revision from the issued text, numbered after it. (BR-409)<br>4. The system opens the new revision for editing (UC-60) and displays MSG431 – “Correction draft created.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor is neither the current Chair nor the current Secretary. (BR-386)<br>2. No revision is created. The system displays MSG397 – “Only the Chair or Secretary may write the minutes.”<br>3. The use case ends.<br>EX-2 – The minutes are not issued<br>1. At Step 2, the latest revision is still a draft, waiting for approval or returned.<br>2. No revision is created. The system displays MSG432 – “Only issued minutes can be corrected.”<br>3. The use case ends.<br>EX-3 – A correction is already in progress<br>1. At Step 2, another correction of these minutes has not been issued yet. (BR-410)<br>2. No revision is created. The system displays MSG433 – “A correction of these minutes is already in progress.”<br>3. The use case ends.<br>EX-4 – The revision cannot be saved<br>1. At Step 3, the system cannot save the new revision.<br>2. No revision is created. The system displays MSG404 – “Unable to save the minutes. Please try again.”<br>3. The use case ends.<br>EX-5 – The Meeting is not accessible<br>1. At Step 2, the Meeting is unavailable or the actor may no longer read it.<br>2. The system answers as if the Meeting did not exist and discloses nothing about its minutes.<br>3. The use case ends. |

### Thay đổi ở các UC đã đặc tả

| UC | Thay đổi |
|---|---|
| UC-58 View Meeting Minutes | Thêm actor "người đọc được cuộc họp" (BR-412). AF mới: người không phải Chủ toạ/Thư ký thấy bản **đã ban hành** mới nhất, không thấy bản nháp, bản chờ duyệt hay bản đính chính đang soạn. AF mới: xem lịch sử các bản đã ban hành (bản bị đính chính hiện nhãn *Đã thay thế*). Bước 4 hiện thêm người gửi duyệt, người duyệt và thời điểm, lý do trả lại |
| UC-59 Generate Meeting Minutes | Bước 4: khi cuộc họp chưa có dự thảo AI, ghi chú của Thư ký điền vào mục II dưới từng mục nghị sự, các dòng *Kết luận* vào mục III, các dòng *Nhiệm vụ* vào dòng "Nhiệm vụ, sản phẩm và thời hạn" (BR-402). Tạo lại không áp dụng cho bản đính chính (BR-413). Bước 2 thêm điều kiện bản mới nhất là nháp **hoặc đã bị trả lại** |
| UC-60 Edit Meeting Minutes | PRE-2 thêm "hoặc đã bị Chủ toạ trả lại" (BR-407) và áp dụng cho bản đính chính |
| UC-24 Set Current Agenda Item | Ghi chú mới của Thư ký chuyển sang mục vừa đặt (UC-82 AF-2) |
| BR-386, BR-388 | BR-388 đổi "submitted for signing" thành "submitted for approval", và nói rõ bản bị trả lại sửa được lại. BR-386 giữ nguyên |

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
| `BR-404` | Only the current Secretary may submit the minutes for approval. | UC-61 | SCRUM-33 — "thư ký gửi duyệt" |
| `BR-405` | The minutes may be submitted for approval only after the Meeting has Ended. | UC-61 | SCRUM-33 — the minutes record a finished sitting |
| `BR-406` | Only the current Chair may approve or return minutes waiting for approval. | UC-64, UC-65 | SCRUM-33 — "chủ toạ duyệt hoặc trả lại" |
| `BR-407` | Returning minutes requires a reason of at most 2,000 characters, which the Secretary sees. Returned minutes can be corrected and submitted again. | UC-61, UC-65, UC-60 | SCRUM-33 — "trả lại kèm góp ý" |
| `BR-408` | Approving the minutes issues them at once. The Meeting keeps its Ended status; being issued is a state of the minutes. | UC-64 | SCRUM-33 / ADR-0013 — no MinutesIssued meeting state |
| `BR-409` | Issued minutes are never changed. A correction is a new revision that starts from the issued text, is numbered after it and follows the same submission and approval; once the correction is issued, the earlier revision is superseded and stays readable. | UC-64, UC-83 | SCRUM-33 — "sửa sau ban hành phải tạo bản đính chính mới, giữ nguyên bản cũ" |
| `BR-410` | The minutes of a Meeting have at most one revision that is not yet issued. | UC-83 | SCRUM-33 — one draft per meeting (BR-388), one correction at a time |
| `BR-411` | Submitting the minutes notifies the current Chair; returning them notifies the current Secretary; issuing them notifies everyone who may read the issued minutes. | UC-61, UC-64, UC-65 | SCRUM-33 — "thành phần tham dự nhận thông báo" |
| `BR-412` | Issued minutes can be read by everyone who may read the Meeting, except Guests. The minutes of a Secret Meeting are read on the screen only. | UC-58, UC-64 | SCRUM-33 / BR-390 — xem Q2 |
| `BR-413` | A correction cannot be rebuilt from the template; it starts from the issued text and is corrected by hand. | UC-59, UC-83 | SCRUM-33 — the record behind the issued text has been sealed (BR-310) |

## 5. Mã thông báo (dành sẵn)

| Mã | HTTP dự kiến | UC | Thông điệp đặc tả | Câu tiếng Việt cho UI |
|---|---|---|---|---|
| `MSG414` | 200 | UC-82 Main Flow | *Note saved.* | `Đã lưu ghi chú.` |
| `MSG415` | 403 | UC-82 EX-1 | *Only the Secretary may write meeting notes.* | `Chỉ Thư ký được ghi chú cuộc họp.` |
| `MSG416` | 409 | UC-82 EX-2 | *Notes can be written only while the meeting is in progress, or after it has ended until the minutes draft is created.* | `Chỉ ghi chú khi cuộc họp đang diễn ra, hoặc sau khi kết thúc mà chưa tạo bản nháp biên bản.` |
| `MSG417` | 409 | UC-82 EX-3 | *This note was changed in another window. Reload to see the latest version.* | `Ghi chú vừa được sửa ở cửa sổ khác. Tải lại để xem bản mới nhất.` |
| `MSG418` | 400 | UC-82 EX-4 | *The note is too long. A note can hold up to 20,000 characters.* | `Ghi chú quá dài, tối đa 20.000 ký tự.` |
| `MSG419` | 409 | UC-82 EX-5 | *The selected agenda item is no longer part of this meeting.* | `Mục nghị sự này không còn trong cuộc họp.` |
| `MSG420` | 500 | UC-82 EX-6 | *Unable to load the meeting notes.* | `Không tải được ghi chú.` |
| `MSG421` | 503 | UC-82 EX-7 | *Unable to save the note. It is kept on this device and will be sent again.* | `Không lưu được ghi chú. Nội dung vẫn giữ trên máy này và sẽ gửi lại.` |
| `MSG422` | FE | UC-82 AF-3 | *You are offline. The note is kept on this device and will be saved when the connection returns.* | `Mất kết nối. Ghi chú vẫn giữ trên máy này và sẽ lưu khi có mạng lại.` |
| `MSG423` | 200 | UC-61 Main Flow | *Minutes submitted to the Chair for approval.* | `Đã gửi biên bản cho Chủ toạ duyệt.` |
| `MSG424` | 403 | UC-61 EX-1 | *Only the Secretary may submit the minutes for approval.* | `Chỉ Thư ký được gửi duyệt biên bản.` |
| `MSG425` | 409 | UC-61 EX-2 | *The minutes can be submitted only after the meeting has ended.* | `Chỉ gửi duyệt biên bản sau khi cuộc họp kết thúc.` |
| `MSG426` | 200 | UC-64 Main Flow | *Minutes approved and issued.* | `Đã duyệt và ban hành biên bản.` |
| `MSG427` | 403 | UC-64 EX-1, UC-65 EX-1 | *Only the Chair may approve or return the minutes.* | `Chỉ Chủ toạ được duyệt hoặc trả lại biên bản.` |
| `MSG428` | 409 | UC-64 EX-2, UC-65 EX-2 | *These minutes are not waiting for approval.* | `Biên bản không ở trạng thái chờ duyệt.` |
| `MSG429` | 200 | UC-65 Main Flow | *Minutes returned to the Secretary.* | `Đã trả lại biên bản cho Thư ký.` |
| `MSG430` | 400 | UC-65 EX-3 | *Enter a reason for returning the minutes, up to 2,000 characters.* | `Nhập lý do trả lại, tối đa 2.000 ký tự.` |
| `MSG431` | 201 | UC-83 Main Flow | *Correction draft created.* | `Đã tạo bản đính chính.` |
| `MSG432` | 409 | UC-83 EX-2 | *Only issued minutes can be corrected.* | `Chỉ đính chính được biên bản đã ban hành.` |
| `MSG433` | 409 | UC-83 EX-3 | *A correction of these minutes is already in progress.* | `Biên bản đang có một bản đính chính chưa ban hành.` |

Dùng lại, cùng tình huống: `MSG397` (không phải Chủ toạ/Thư ký, UC-83), `MSG400` (gửi duyệt khi đã gửi hoặc đã ban
hành), `MSG401` (thao tác trên bản đã cũ), `MSG403` (lỗi đọc), `MSG404` (lỗi lưu).

## 6. API dự kiến (cho BE, chưa phải contract)

Contract chính thức viết vào `docs/03-api/` trong PR code. Mọi endpoint kiểm tra lại quyền ở server. Người không phải
Chủ toạ/Thư ký hỏi tới ghi chú, bản nháp hay bản chờ duyệt thì nhận `404`, không có mã.

**Ghi chú — SCRUM-30**

| Method | Đường dẫn | Ghi chú |
|---|---|---|
| `GET` | `/api/meetings/{id}/notes` | `{ notes: [{ agendaItemId \| null, body, updatedAt, author }], currentAgendaItemId, canWrite }`. Chủ toạ thấy ghi chú của Thư ký hiện tại, `canWrite: false` |
| `PUT` | `/api/meetings/{id}/notes` | Body `{ agendaItemId \| null, body, updatedAt \| null }`. `updatedAt: null` nghĩa là ghi chú mới. `200 MSG414`, `409 MSG417` khi `updatedAt` lệch |

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

**Biên bản — SCRUM-33**

| Method | Đường dẫn | Ghi chú |
|---|---|---|
| `POST` | `/api/minutes/{id}/submit` | Body `{ updatedAt }`. `200 MSG423`. `Draft`/`Rejected` → `WaitingApproval` |
| `POST` | `/api/minutes/{id}/approve` | Body `{ updatedAt }`. `200 MSG426`. `WaitingApproval` → `Issued`, bản cũ (nếu là đính chính) → `Superseded` |
| `POST` | `/api/minutes/{id}/reject` | Body `{ reason, updatedAt }`. `200 MSG429`. `WaitingApproval` → `Rejected` |
| `POST` | `/api/minutes/{id}/corrections` | `201 MSG431`. Tạo bản `Draft` mới, `RevisionNumber + 1`, `SupersedesId` trỏ bản đã ban hành |
| `GET` | `/api/meetings/{id}/minutes` | Thêm `history: [{ id, revisionNumber, status, issuedAt }]`. Người đọc được cuộc họp (trừ Khách) nhận bản `Issued` mới nhất |

- Migration nhỏ: `MeetingMinutes` chưa có `SubmittedById`, `ReturnedById`, `ReturnedAt`. Hoặc ghi người và thời điểm
  vào `audit_log` thay vì thêm cột — chọn lúc làm, ghi vào PR.
- Thông báo: `MinutesWaitingApproval` và `MinutesIssued` đã có trong enum; thêm `MinutesReturned`. Enum lưu dạng chuỗi
  nên không cần migration cho giá trị mới.
- `IssuedAt` khác null là khoá nội dung; `PUT /api/minutes/{id}` và `POST …/minutes` đã trả `MSG400`, cần mở lại cho
  `Rejected` (BR-407).

## 7. Câu hỏi cần PO / BA chốt

| # | Câu hỏi | Đề xuất trong note |
|---|---|---|
| Q1 | Dự thảo AI (UC-47) có đọc ghi chú của Thư ký không, hay chỉ bản gỡ băng? | Chưa đọc. BR-402 chỉ áp dụng khi cuộc họp chưa có dự thảo AI. Đưa ghi chú vào AI là sửa ADR-0054 |
| Q2 | Khách có xem được biên bản đã ban hành không? | Không (BR-412): Khách vào cuộc họp bằng lời mời, phiên hết khi bị gỡ (UC-28c) |
| Q3 | Ngoài Thư ký, Chủ toạ có được tự ghi chú riêng không? | Không. Bảng cho phép mỗi người một ghi chú, nhưng ticket chỉ nói thư ký |
| Q4 | FE-12 đổi nghĩa UC-61, UC-64, UC-65 như mục 2 D4, hay giữ chúng cho luồng ký và cấp số mới cho duyệt? Còn hướng gộp UC-60 + UC-61 và thêm UC ban hành trong [NOTE-uc-chua-dac-ta-cho-sua.md](NOTE-uc-chua-dac-ta-cho-sua.md) | Đổi nghĩa, vì cả năm UC là placeholder và SCRUM-37 (ký) là Could |
| Q5 | Thư ký có được rút lại biên bản đã gửi duyệt khi Chủ toạ chưa xử lý không? | Không làm trong SCRUM-33 |
| Q6 | Thông báo ban hành có gửi email không? | Chỉ trong ứng dụng, như các thông báo cuộc họp khác. Email là việc riêng nếu PO cần |

## 8. Việc còn lại

- [ ] Ghi D1–D4 lên SCRUM-30 và SCRUM-33 bằng comment (skill `jira-deviation-note`).
- [ ] Xuất PNG cho 5 sequence `.txt` mới (`node scripts/export-diagrams.js …`) và thêm tab vào
      `TrustCabinet_Sequence_Diagrams.drawio` — để lúc BA Final Review nếu đặc tả còn đổi.
- [ ] BA: đưa UC-82, UC-83, nội dung UC-61/64/65 và BR-395 → BR-413 vào Report 3 DOCX; cập nhật inventory và UC diagram.
- [ ] BE: contract trong `docs/03-api/`, code, test, đổi `MSG414`–`MSG433` từ *Dành sẵn* sang *BE* khi có code.
