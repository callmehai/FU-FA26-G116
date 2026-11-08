# Đặc tả sơ bộ SCRUM-33 (duyệt, ban hành biên bản)

**Ngày:** 2026-11-07 · **Người viết:** BE (Huy Nguyễn Trường) · **Trạng thái:** sơ bộ, luồng đã chốt, đủ để BE làm theo; sửa theo review PR #279 ngày 08/11/2026

| Jira | Tên | Trạng thái lúc viết |
|---|---|---|
| [SCRUM-33](https://sep490g116.atlassian.net/browse/SCRUM-33) | Phê duyệt và ban hành biên bản | To Do, Sprint 3 |

Đây là bước *BE: basic UC Specification + Sequence Diagram* của
[DEVELOPMENT_WORKFLOW.md §2](../DEVELOPMENT_WORKFLOW.md). Note này chưa phải Report 3: UC, BR ở đây là
**đề xuất**. BA đưa vào DOCX ở `08-reports/source/` lúc BA Final Review, rồi sinh lại
`05-use-case-specifications.md` và `06-business-rules.md`. Không chép tay sang hai file đó.

- Phần này đã có trong bản đầu của note gộp SCRUM-30 + SCRUM-33 (PR #271), bị tách ra trước khi merge để chốt luồng;
  note SCRUM-30 nằm ở [NOTE-dac-ta-so-bo-scrum-30.md](NOTE-dac-ta-so-bo-scrum-30.md). Nội dung giữ nguyên bản đó,
  chỉ đổi số mã.
- Mã `MSG424`–`MSG434` đã **dành sẵn** trong [danh mục](../03-api/00-danh-muc-ma-thong-bao.md) cùng commit với note này
  (bản gộp dùng `MSG423`–`MSG433`; `MSG423` nay thuộc ghi chú SCRUM-30 nên cả dải dời lên một số). `MSG456` dành
  thêm theo review cho tạo lại bản đính chính (UC-59, BR-413); `MSG435`–`MSG455` đã thuộc SCRUM-137/142 (PR #282).
- Số BR-404 → BR-413 là số đề xuất, nối tiếp BR-403 của SCRUM-30. BA giữ hoặc đổi số khi đưa vào Report 3.
- UC mới UC-83 nối tiếp UC-82 (SCRUM-30). Không liên quan tới UC-83 **cũ** ở bảng đổi số ngày 10/10/2026 (đã đổi
  thành UC-64).
- Số mục lệch (D) và câu hỏi (Q) giữ như bản gộp để khớp với trao đổi đã dẫn; D2, Q1, Q3 của bản gộp thuộc SCRUM-30.

## Luồng đã chốt

**Một người duyệt, duyệt là ban hành luôn, không tách bước ký.**

```
Nháp / Bị trả lại → Gửi duyệt (Thư ký) → Chờ duyệt → Duyệt và ban hành (Chủ toạ, khoá vĩnh viễn)
                                                    ↘ Trả lại kèm lý do (Chủ toạ) → Thư ký sửa → gửi lại
Sửa sau ban hành = bản đính chính mới; bản cũ giữ nguyên để đọc.
```

| `MinutesStatus` | Ai làm gì | Sang |
|---|---|---|
| `Draft`, `Rejected` | Thư ký gửi duyệt (UC-61) | `WaitingApproval` |
| `WaitingApproval` | Chủ toạ duyệt (UC-64) | `Issued`; bản nó đính chính → `Superseded` |
| `WaitingApproval` | Chủ toạ trả lại kèm lý do (UC-65) | `Rejected` |
| `Issued` | Chủ toạ hoặc Thư ký tạo bản đính chính (UC-83) | bản mới `Draft`, bản `Issued` giữ nguyên |

Cuộc họp giữ `Ended` suốt luồng này (D1).

## 1. Hiện trạng code (07/11/2026)

| Phần | Đã có | Chưa có |
|---|---|---|
| Biên bản | `MeetingMinutes` có `RevisionNumber`, `SupersedesId`, `SubmittedAt`, `ApprovedById/At`, `RejectionReason`, `IssuedAt`; `MinutesStatus` = `Draft, WaitingApproval, Rejected, Issued, Superseded`; loại thông báo `MinutesWaitingApproval`, `MinutesIssued` | Endpoint gửi duyệt, duyệt, trả lại, đính chính; người ngoài Chủ toạ/Thư ký xem bản đã ban hành; thông báo |
| Bản nháp (SCRUM-32) | UC-58, UC-59, UC-60 chạy thật; `MSG400` đã chặn sửa khi biên bản không còn là nháp; tải Word/PDF, Mật không tải được (`MSG405`) | Mở lại cho bản `Rejected` |

## 2. Chỗ lệch khỏi ticket, cần ghi lên Jira

| # | Ticket | Đặc tả này | Lý do |
|---|---|---|---|
| D1 | SCRUM-33: "Ban hành chuyển cuộc họp sang trạng thái `MinutesIssued`" | Cuộc họp giữ `Ended`. Trạng thái "đã ban hành" nằm ở `MeetingMinutes.Status = Issued` | [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md) đã bỏ `MinutesIssued` khỏi vòng đời cuộc họp (PO chốt Q10 ngày 10/10/2026) |
| D3 | SCRUM-33 chỉ liệt kê `submit`, `approve`, `reject` | Thêm thao tác tạo bản đính chính (UC-83) | Ticket yêu cầu "sửa sau ban hành phải tạo bản đính chính mới" nhưng không có endpoint nào làm việc đó |
| D4 | Inventory: UC-61 *Gửi ký*, UC-64 *Hoàn tất / niêm phong* (Secretary), UC-65 *Yêu cầu sửa* (người ký) | UC-61 *Gửi duyệt*, UC-64 *Duyệt và ban hành* (Chair), UC-65 *Trả lại* (Chair). UC-62 *Ký*, UC-63 *Trạng thái ký* để dành cho ký số mô phỏng (SCRUM-37, Could) | Đều là placeholder, chưa có code. SCRUM-33 là duyệt của Chủ toạ, ký là việc riêng của SCRUM-37 |

## 3. Use Case đề xuất

### UC-61 – Submit Meeting Minutes for Approval (Gửi duyệt biên bản)

Thay placeholder *Submit Meeting Minutes for Signing*. `<<extend>>` UC-58. Sequence Diagram:
[UC61_SubmitMeetingMinutesForApproval.txt](../06-sequencediagram/minutes-management/UC61_SubmitMeetingMinutesForApproval.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | Chair (notified) |
| **Description** | Allows the current Secretary to send the finished minutes draft to the current Chair for approval. From then on nobody can correct or rebuild it until the Chair approves or returns it. (BR-404, BR-388) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Secretary of the Meeting. (BR-404)<br>● PRE-2: The Meeting has Ended. (BR-405)<br>● PRE-3: The latest revision of the minutes is a draft, or was returned by the Chair. (BR-407) |
| **Postconditions** | ● POST-1: The revision is waiting for approval, with who submitted it and when.<br>● POST-2: The revision can no longer be corrected or rebuilt. (BR-388)<br>● POST-3: The current Chair is notified. (BR-411)<br>● POST-4: The Meeting status is unchanged. |
| **Normal Flow** | Submit Meeting Minutes for Approval<br>1. On the Biên bản tab, the actor selects “Gửi duyệt” [Submit for approval] and confirms.<br>2. The system checks the assignment, that the Meeting has Ended and that the latest revision is a draft or was returned, and that it is the copy the actor read. (BR-404, BR-405, BR-389)<br>3. The system marks the revision as waiting for approval and records who submitted it and when.<br>4. The system notifies the current Chair. (BR-411)<br>5. The system displays MSG424 – “Minutes submitted to the Chair for approval.” |
| **Alternative Flows** | AF-1 – Places are still left blank (proposed; the screen warns, the server does not block)<br>1. At Step 1, the text still holds “……” where the Secretary was to write.<br>2. The screen warns that some places are still blank and asks for confirmation.<br>3. The actor confirms and the flow resumes at Step 2 of the Main Flow.<br>AF-2 – Submit again after the Chair returned the minutes<br>1. At Step 1, the latest revision was returned; the system shows the Chair's reason above the paper. (BR-407)<br>2. The actor corrects the text (UC-60) or submits it unchanged.<br>3. The flow resumes at Step 2 of the Main Flow. |
| **Exception Flows** | EX-1 – Actor is the current Chair, not the Secretary<br>1. At Step 2, the actor is the current Chair but not the current Secretary. (BR-404)<br>2. The minutes stay as they are. The system displays MSG425 – “Only the Secretary may submit the minutes for approval.”<br>3. The use case ends.<br>EX-2 – The Meeting has not ended<br>1. At Step 2, the Meeting is still InProgress. (BR-405)<br>2. The minutes stay a draft. The system displays MSG426 – “The minutes can be submitted only after the meeting has ended.”<br>3. The use case ends.<br>EX-3 – The minutes were already submitted or issued<br>1. At Step 2, the latest revision is waiting for approval or has been issued. (BR-388)<br>2. Nothing changes. The system displays MSG400 – “The minutes have been submitted or issued and can no longer be changed.”<br>3. The use case ends.<br>EX-4 – Someone saved after the actor read the minutes<br>1. At Step 2, the draft was saved by another person after the actor read it. (BR-389)<br>2. Nothing is submitted. The system displays MSG401 – “The minutes were just changed by someone else. Reload to see the latest version.”<br>3. The flow resumes at Step 1 of the Main Flow.<br>EX-5 – The change cannot be saved<br>1. At Step 3, the system cannot save the new state.<br>2. The minutes stay a draft and nobody is notified. The system displays MSG404 – “Unable to save the minutes. Please try again.”<br>3. The use case ends.<br>EX-6 – The minutes are not accessible<br>1. At Step 2, the Meeting is unavailable, the actor may no longer read it, or the actor is neither the current Chair nor the current Secretary. (BR-386)<br>2. The system answers as if the minutes did not exist, with no message code, and discloses nothing about them.<br>3. The use case ends. |

### UC-64 – Approve and Issue Meeting Minutes (Duyệt và ban hành biên bản)

Thay placeholder *Finalize Meeting Minutes*; actor đổi từ Secretary sang Chair. `<<extend>>` UC-58. Sequence Diagram:
[UC64_ApproveAndIssueMeetingMinutes.txt](../06-sequencediagram/minutes-management/UC64_ApproveAndIssueMeetingMinutes.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | Secretary, every person who may read the issued minutes (notified) |
| **Description** | Allows the current Chair to approve the minutes waiting for approval. Approval issues them at once: they are locked, and the people of the Meeting are notified and can read them. (BR-406, BR-408) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-406)<br>● PRE-2: The latest revision of the minutes is waiting for approval. |
| **Postconditions** | ● POST-1: The revision is issued, with who approved it and when, and can never be changed again. (BR-408, BR-409)<br>● POST-2: When the revision corrects an earlier issued one, the earlier one is marked superseded and stays readable. (BR-409)<br>● POST-3: Everyone who may read the issued minutes is notified. (BR-411, BR-412)<br>● POST-4: The Meeting stays Ended. (BR-408) |
| **Normal Flow** | Approve and Issue Meeting Minutes<br>1. The actor opens the notification or the Biên bản tab and reads the minutes as the paper that will be printed.<br>2. The actor selects “Duyệt và ban hành” [Approve and issue] and confirms.<br>3. The system checks the assignment, that the revision is still waiting for approval and that it is the copy the actor read. (BR-406, BR-389)<br>4. The system marks the revision as issued and records who approved it and when. (BR-408)<br>5. The system notifies the people who may read the issued minutes. (BR-411, BR-412)<br>6. The system displays MSG427 – “Minutes approved and issued.” |
| **Alternative Flows** | AF-1 – Issue a correction<br>1. At Step 4, the revision corrects an earlier issued revision.<br>2. The system marks the earlier revision as superseded; it stays readable in the history of the minutes. (BR-409)<br>3. The flow resumes at Step 5 of the Main Flow. |
| **Exception Flows** | EX-1 – Actor is the current Secretary, not the Chair<br>1. At Step 3, the actor is the current Secretary but not the current Chair. (BR-406)<br>2. The minutes stay as they are. The system displays MSG428 – “Only the Chair may approve or return the minutes.”<br>3. The use case ends.<br>EX-2 – The minutes are not waiting for approval<br>1. At Step 3, the revision is a draft, was returned, or has already been issued.<br>2. Nothing changes. The system displays MSG429 – “These minutes are not waiting for approval.”<br>3. The use case ends.<br>EX-3 – The minutes changed after the actor opened them<br>1. At Step 3, the revision is not the copy the actor read. (BR-389)<br>2. Nothing is issued. The system displays MSG401 – “The minutes were just changed by someone else. Reload to see the latest version.”<br>3. The flow resumes at Step 1 of the Main Flow.<br>EX-4 – The change cannot be saved<br>1. At Step 4, the system cannot save the new state.<br>2. The minutes stay waiting for approval and nobody is notified. The system displays MSG404 – “Unable to save the minutes. Please try again.”<br>3. The use case ends.<br>EX-5 – The minutes are not accessible<br>1. At Step 3, the Meeting is unavailable, the actor may no longer read it, or the actor is neither the current Chair nor the current Secretary. (BR-386)<br>2. The system answers as if the minutes did not exist, with no message code, and discloses nothing about them.<br>3. The use case ends. |

### UC-65 – Return Meeting Minutes for Revision (Trả lại biên bản)

Thay placeholder *Request Revision of Meeting Minutes*; actor đổi từ người ký sang Chair. `<<extend>>` UC-58. Sequence Diagram:
[UC65_ReturnMeetingMinutesForRevision.txt](../06-sequencediagram/minutes-management/UC65_ReturnMeetingMinutesForRevision.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | Secretary (notified) |
| **Description** | Allows the current Chair to send the minutes waiting for approval back to the Secretary with a reason, so the Secretary can correct them and submit them again. (BR-406, BR-407) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-406)<br>● PRE-2: The latest revision of the minutes is waiting for approval. |
| **Postconditions** | ● POST-1: The revision is returned, with the reason, who returned it and when. (BR-407)<br>● POST-2: The Chair and the Secretary can correct the revision again and the Secretary can submit it again. (BR-407)<br>● POST-3: The current Secretary is notified. (BR-411) |
| **Normal Flow** | Return Meeting Minutes for Revision<br>1. On the Biên bản tab, the actor selects “Trả lại” [Return].<br>2. The system asks for the reason; the actor enters it and confirms.<br>3. The system checks the assignment, that the revision is still waiting for approval, that it is the copy the actor read and that the reason is not empty and within 2,000 characters. (BR-406, BR-389, BR-407)<br>4. The system marks the revision as returned and records the reason, who returned it and when.<br>5. The system notifies the current Secretary. (BR-411)<br>6. The system displays MSG430 – “Minutes returned to the Secretary.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is the current Secretary, not the Chair<br>1. At Step 3, the actor is the current Secretary but not the current Chair. (BR-406)<br>2. The minutes stay as they are. The system displays MSG428 – “Only the Chair may approve or return the minutes.”<br>3. The use case ends.<br>EX-2 – The minutes are not waiting for approval<br>1. At Step 3, the revision is a draft, was returned, or has been issued.<br>2. Nothing changes. The system displays MSG429 – “These minutes are not waiting for approval.”<br>3. The use case ends.<br>EX-3 – The reason is missing or too long<br>1. At Step 3, the reason is empty or longer than 2,000 characters. (BR-407)<br>2. Nothing changes. The system displays MSG431 – “Enter a reason for returning the minutes, up to 2,000 characters.”<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-4 – The change cannot be saved<br>1. At Step 4, the system cannot save the new state.<br>2. The minutes stay waiting for approval and nobody is notified. The system displays MSG404 – “Unable to save the minutes. Please try again.”<br>3. The use case ends.<br>EX-5 – The minutes are not accessible<br>1. At Step 3, the Meeting is unavailable, the actor may no longer read it, or the actor is neither the current Chair nor the current Secretary. (BR-386)<br>2. The system answers as if the minutes did not exist, with no message code, and discloses nothing about them.<br>3. The use case ends.<br>EX-6 – The minutes changed after the actor opened them<br>1. At Step 3, the revision is not the copy the actor read. (BR-389)<br>2. Nothing is returned. The system displays MSG401 – “The minutes were just changed by someone else. Reload to see the latest version.”<br>3. The flow resumes at Step 1 of the Main Flow. |

### UC-83 – Correct Issued Meeting Minutes (Tạo bản đính chính biên bản)

UC mới, `<<extend>>` UC-58. Sequence Diagram:
[UC83_CorrectIssuedMeetingMinutes.txt](../06-sequencediagram/minutes-management/UC83_CorrectIssuedMeetingMinutes.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to start a correction of issued minutes. The correction is a new revision that starts from the issued text; the issued revision is never changed and stays the one people read until the correction is issued. (BR-409, BR-410) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-386)<br>● PRE-2: The selected revision of the minutes is issued. |
| **Postconditions** | ● POST-1: A new draft revision exists, numbered after the issued one, holding the issued text and pointing at the revision it corrects. (BR-409)<br>● POST-2: The issued revision is unchanged and stays the one other people read. (BR-409)<br>● POST-3: Nobody is notified until the correction is issued (UC-64). |
| **Normal Flow** | Correct Issued Meeting Minutes<br>1. On the Biên bản tab, the actor selects “Tạo bản đính chính” [Create correction] on the issued revision and confirms.<br>2. The system checks the assignment, that the selected revision is issued and that no later revision of these minutes is still unissued. (BR-386, BR-410)<br>3. The system creates the new draft revision from the issued text, numbered after it. (BR-409)<br>4. The system opens the new revision for editing (UC-60) and displays MSG432 – “Correction draft created.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor may read the selected revision (it is issued or superseded) but is neither the current Chair nor the current Secretary. (BR-386, BR-412)<br>2. No revision is created. The system displays MSG397 – “Only the Chair or Secretary may write the minutes.”<br>3. The use case ends.<br>EX-2 – The selected revision is not issued<br>1. At Step 2, the selected revision is a draft, waiting for approval, returned, or superseded by a later issued revision.<br>2. No revision is created. The system displays MSG433 – “Only issued minutes can be corrected.”<br>3. The use case ends.<br>EX-3 – A correction is already in progress<br>1. At Step 2, the selected revision is issued, but a later revision correcting it has not been issued yet. (BR-410)<br>2. No revision is created. The system displays MSG434 – “A correction of these minutes is already in progress.”<br>3. The use case ends.<br>EX-4 – The revision cannot be saved<br>1. At Step 3, the system cannot save the new revision.<br>2. No revision is created. The system displays MSG404 – “Unable to save the minutes. Please try again.”<br>3. The use case ends.<br>EX-5 – The minutes are not accessible<br>1. At Step 2, the Meeting is unavailable, the actor may no longer read it, or the actor is neither the current Chair nor the current Secretary and the selected revision is not issued or superseded. (BR-386)<br>2. The system answers as if the minutes did not exist, with no message code, and discloses nothing about them.<br>3. The use case ends. |

### Thay đổi ở các UC đã đặc tả

| UC | Thay đổi |
|---|---|
| UC-58 View Meeting Minutes | Thêm actor "người đọc được cuộc họp" (BR-412). AF mới: người không phải Chủ toạ/Thư ký thấy bản **đã ban hành** mới nhất, không thấy bản nháp, bản chờ duyệt hay bản đính chính đang soạn. AF mới: xem lịch sử các bản đã ban hành (bản bị đính chính hiện nhãn *Đã thay thế*) và mở đọc nội dung một bản trong đó (`GET /api/minutes/{id}`, §6). Bước 4 hiện thêm người gửi duyệt, người duyệt và thời điểm, lý do trả lại. Người ngoài hỏi tới biên bản chưa ban hành vẫn nhận `404`. Tải Word/PDF **giữ như contract SCRUM-32**: chỉ Chủ toạ/Thư ký tải được, người đọc khác nhận `403` `MSG397` và chỉ xem trên màn hình; cuộc họp Mật không ai tải được (`MSG405`). Mở tải cho người đọc là câu hỏi Q7 (BR-412) |
| UC-59 Generate Meeting Minutes | Tạo lại không áp dụng cho bản đính chính (BR-413): `POST /api/meetings/{id}/minutes` với `rebuild: true` khi bản mới nhất là bản đính chính (`SupersedesId` khác null) trả `409` `MSG456`, không đổi gì. Bước 2 thêm điều kiện bản mới nhất là nháp **hoặc đã bị trả lại**. EX mới: *The latest revision is a correction* → `MSG456` |
| UC-60 Edit Meeting Minutes | PRE-2 thêm "hoặc đã bị Chủ toạ trả lại" (BR-407) và áp dụng cho bản đính chính |
| BR-386, BR-388 | BR-388 đổi "submitted for signing" thành "submitted for approval", và nói rõ bản bị trả lại sửa được lại. BR-386 giữ nguyên |

## 4. Business Rules đề xuất

| BR | Rule Statement | UC | Nguồn |
|---|---|---|---|
| `BR-404` | Only the current Secretary may submit the minutes for approval. | UC-61 | SCRUM-33 — "thư ký gửi duyệt" |
| `BR-405` | The minutes may be submitted for approval only after the Meeting has Ended. | UC-61 | SCRUM-33 — the minutes record a finished sitting |
| `BR-406` | Only the current Chair may approve or return minutes waiting for approval. | UC-64, UC-65 | SCRUM-33 — "chủ toạ duyệt hoặc trả lại" |
| `BR-407` | Returning minutes requires a reason of at most 2,000 characters, which the Secretary sees. Returned minutes can be corrected and submitted again. | UC-61, UC-65, UC-60 | SCRUM-33 — "trả lại kèm góp ý" |
| `BR-408` | Approving the minutes issues them at once. The Meeting keeps its Ended status; being issued is a state of the minutes. | UC-64 | SCRUM-33 / ADR-0013 — no MinutesIssued meeting state |
| `BR-409` | Issued minutes are never changed. A correction is a new revision that starts from the issued text, is numbered after it and follows the same submission and approval; once the correction is issued, the earlier revision is superseded and stays readable. | UC-64, UC-83 | SCRUM-33 — "sửa sau ban hành phải tạo bản đính chính mới, giữ nguyên bản cũ" |
| `BR-410` | The minutes of a Meeting have at most one revision that is not yet issued. | UC-83 | SCRUM-33 — one draft per meeting (BR-388), one correction at a time |
| `BR-411` | Submitting the minutes notifies the current Chair; returning them notifies the current Secretary; issuing them notifies everyone who may read the issued minutes. | UC-61, UC-64, UC-65 | SCRUM-33 — "thành phần tham dự nhận thông báo" |
| `BR-412` | Issued minutes, and the revisions they superseded, can be read on the screen by everyone who may read the Meeting, except Guests. Only the current Chair and the current Secretary may download them, and nobody may download the minutes of a Secret Meeting (BR-390). | UC-58, UC-64 | SCRUM-33 / BR-390 — PO chốt Q2 ngày 07/11/2026: Khách không bao giờ thấy biên bản; tải về giữ như SCRUM-32 (Q7) |
| `BR-413` | A correction cannot be rebuilt from the template; it starts from the issued text and is corrected by hand. | UC-59, UC-83 | SCRUM-33 — the record behind the issued text has been sealed (BR-310) |

## 5. Mã thông báo (dành sẵn)

| Mã | HTTP dự kiến | UC | Thông điệp đặc tả | Câu tiếng Việt cho UI |
|---|---|---|---|---|
| `MSG424` | 200 | UC-61 Main Flow | *Minutes submitted to the Chair for approval.* | `Đã gửi biên bản cho Chủ toạ duyệt.` |
| `MSG425` | 403 | UC-61 EX-1 | *Only the Secretary may submit the minutes for approval.* | `Chỉ Thư ký được gửi duyệt biên bản.` |
| `MSG426` | 409 | UC-61 EX-2 | *The minutes can be submitted only after the meeting has ended.* | `Chỉ gửi duyệt biên bản sau khi cuộc họp kết thúc.` |
| `MSG427` | 200 | UC-64 Main Flow | *Minutes approved and issued.* | `Đã duyệt và ban hành biên bản.` |
| `MSG428` | 403 | UC-64 EX-1, UC-65 EX-1 | *Only the Chair may approve or return the minutes.* | `Chỉ Chủ toạ được duyệt hoặc trả lại biên bản.` |
| `MSG429` | 409 | UC-64 EX-2, UC-65 EX-2 | *These minutes are not waiting for approval.* | `Biên bản không ở trạng thái chờ duyệt.` |
| `MSG430` | 200 | UC-65 Main Flow | *Minutes returned to the Secretary.* | `Đã trả lại biên bản cho Thư ký.` |
| `MSG431` | 400 | UC-65 EX-3 | *Enter a reason for returning the minutes, up to 2,000 characters.* | `Nhập lý do trả lại, tối đa 2.000 ký tự.` |
| `MSG432` | 201 | UC-83 Main Flow | *Correction draft created.* | `Đã tạo bản đính chính.` |
| `MSG433` | 409 | UC-83 EX-2 | *Only issued minutes can be corrected.* | `Chỉ đính chính được biên bản đã ban hành.` |
| `MSG434` | 409 | UC-83 EX-3 | *A correction of these minutes is already in progress.* | `Biên bản đang có một bản đính chính chưa ban hành.` |
| `MSG456` | 409 | UC-59 EX mới | *A correction cannot be rebuilt from the template. Correct it by hand.* | `Bản đính chính không tạo lại từ mẫu được, hãy sửa trực tiếp.` |

`MSG456` không liền dải vì `MSG435`–`MSG455` đã thuộc SCRUM-137/142 (PR #282) trước khi review thêm mã này.

Dùng lại, cùng tình huống: `MSG397` (không phải Chủ toạ/Thư ký, UC-83; người đọc tải bản đã ban hành, UC-58),
`MSG400` (gửi duyệt khi đã gửi hoặc đã ban hành), `MSG401` (thao tác trên bản đã cũ: UC-61, UC-64, UC-65), `MSG403` (lỗi đọc), `MSG404` (lỗi lưu), `MSG405` (tải biên bản của cuộc họp
Mật).

## 6. API dự kiến (cho BE, chưa phải contract)

Contract chính thức viết vào `docs/03-api/` trong PR code. Mọi endpoint kiểm tra lại quyền ở server. Người không phải
Chủ toạ/Thư ký hỏi tới bản nháp, bản chờ duyệt hay bản đính chính đang soạn thì nhận `404`, không có mã.

| Method | Đường dẫn | Ghi chú |
|---|---|---|
| `POST` | `/api/minutes/{id}/submit` | Body `{ updatedAt }`. `200 MSG424`. `Draft`/`Rejected` → `WaitingApproval` |
| `POST` | `/api/minutes/{id}/approve` | Body `{ updatedAt }`. `200 MSG427`. `WaitingApproval` → `Issued`, bản cũ (nếu là đính chính) → `Superseded` |
| `POST` | `/api/minutes/{id}/reject` | Body `{ reason, updatedAt }`. `200 MSG430`. `WaitingApproval` → `Rejected` |
| `POST` | `/api/minutes/{id}/corrections` | `{id}` là bản đã ban hành được chọn. `201 MSG432`. Tạo bản `Draft` mới, `RevisionNumber + 1`, `SupersedesId` trỏ bản đó. Bản `{id}` không phải `Issued` → `409 MSG433`; đã có bản sau nó chưa ban hành → `409 MSG434` |
| `GET` | `/api/meetings/{id}/minutes` | Thêm `history: [{ id, revisionNumber, status, issuedAt }]`. Chủ toạ/Thư ký nhận mọi bản. Người đọc được cuộc họp (trừ Khách) nhận `minutes` là bản `Issued` mới nhất, và `history` **lọc ở server** chỉ còn dòng `Issued` và `Superseded`: họ không biết có bản đính chính đang soạn hay đang chờ duyệt |
| `GET` | `/api/minutes/{id}` | Mới: đọc nội dung một bản, cùng dạng `minutes`, để mở bản `Superseded` từ lịch sử. Chủ toạ/Thư ký đọc mọi bản; người đọc khác chỉ đọc `Issued`/`Superseded`, bản khác `404` không mã |
| `POST` | `/api/meetings/{id}/minutes` | Đã có (UC-59). `rebuild: true` khi bản mới nhất là bản đính chính → `409 MSG456` (BR-413) |

- Migration nhỏ: `MeetingMinutes` chưa có `SubmittedById`, `ReturnedById`, `ReturnedAt`. Hoặc ghi người và thời điểm
  vào `audit_log` thay vì thêm cột — chọn lúc làm, ghi vào PR.
- Thông báo: `MinutesWaitingApproval` và `MinutesIssued` đã có trong enum; thêm `MinutesReturned`. Enum lưu dạng chuỗi
  nên không cần migration cho giá trị mới.
- Khoá nội dung theo `Status`: code hiện khoá khi `Status != Draft` (`MinutesService`), `PUT /api/minutes/{id}` và
  `POST …/minutes` trả `MSG400`. SCRUM-33 đổi thành khoá khi `Status` không phải `Draft` hay `Rejected` (BR-407).
- Cảnh báo "……" còn trống (UC-61 AF-1) là đề xuất cho FE, server không chặn.

## 7. Câu hỏi cần PO / BA chốt

| # | Câu hỏi | Đề xuất trong note |
|---|---|---|
| Q2 | Khách có xem được biên bản đã ban hành không? | **Chốt 07/11/2026: không.** Khách không bao giờ thấy biên bản (BR-412): Khách vào cuộc họp bằng lời mời, phiên hết khi bị gỡ (UC-28c) |
| Q4 | FE-12 đổi nghĩa UC-61, UC-64, UC-65 như mục 2 D4, hay giữ chúng cho luồng ký và cấp số mới cho duyệt? Còn hướng gộp UC-60 + UC-61 và thêm UC ban hành trong [NOTE-uc-chua-dac-ta-cho-sua.md](NOTE-uc-chua-dac-ta-cho-sua.md) | **Chốt 07/11/2026: đổi nghĩa.** Cả năm UC là placeholder; UC-62, UC-63 để dành cho ký số mô phỏng SCRUM-37 (Could) |
| Q5 | Thư ký có được rút lại biên bản đã gửi duyệt khi Chủ toạ chưa xử lý không? | Không làm trong SCRUM-33 |
| Q6 | Thông báo ban hành có gửi email không? | Chỉ trong ứng dụng, như các thông báo cuộc họp khác. Email là việc riêng nếu PO cần |
| Q7 | Người đọc được cuộc họp (không phải Chủ toạ/Thư ký) có được tải Word/PDF bản đã ban hành không? | Không trong SCRUM-33: giữ như contract SCRUM-32 (`17-du-thao-bien-ban.md`: người khác "Chỉ xem"; `MSG397`), chỉ xem trên màn hình. Nếu PO muốn mở thì đổi `MSG397`, `MinutesService.ExportAsync` và BR-412 trong cùng PR |

## 8. Việc còn lại

- [x] Ghi D1, D3, D4 lên SCRUM-33 bằng comment (skill `jira-deviation-note`).
- [ ] Sửa comment SCRUM-33: Q2 đã chốt (Khách không thấy biên bản), thêm Q7 (người đọc không tải về), `MSG456`.
- [ ] Xuất PNG cho 4 sequence `.txt` (`node scripts/export-diagrams.js docs/06-sequencediagram/minutes-management`) và
      thêm tab vào `TrustCabinet_Sequence_Diagrams.drawio` — để lúc BA Final Review nếu đặc tả còn đổi.
- [ ] BA: đưa UC-83, nội dung UC-61/64/65 và BR-404 → BR-413 vào Report 3 DOCX; cập nhật inventory và UC diagram.
- [ ] BE: contract trong `docs/03-api/`, code, test, đổi `MSG424`–`MSG434` và `MSG456` từ *Dành sẵn* sang *BE* khi có
      code.
