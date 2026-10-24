# Patch Report 3 — SCRUM-18 / SCRUM-20 (UC-14, UC-17, UC-28a, UC-29, UC-30)

**Ngày:** 24/10/2026 · **Lượt 4** theo [05-plan](../05-plan-sua-uc-br-msg-diagram.md) mục 8 (phần việc mục 2) · **Nguồn:** [01](../01-scrum-18-20-loi-moi-va-danh-sach.md),
[API cuộc họp](../../../03-api/05-quan-ly-cuoc-hop.md), [API thành phần tham dự §4.1](../../../03-api/09-thanh-phan-tham-du.md), catalogue MSG105–MSG108, MSG154, MSG277, MSG285, MSG286.
PO Hải chốt 20/10 và 21/10/2026; FE ở PR #111. Câu cũ lấy nguyên văn từ `Report_3_...docx` ngày 24/10/2026 (đã có patch lượt 1 SCRUM-26; chưa có lượt 2–3 ở các mục dưới đây).

**Đã áp dụng vào DOCX ngày 24/10/2026** (`Report_3_...docx`, rồi `python scripts/sync_reports.py --report 3`). Mục 9 (Change Log) và mục 10 (ngoài phạm vi) chưa dán; xem README.
Lượt này **không** sửa tay `05-use-case-specifications.md`, `06-business-rules.md` (đã kiểm: `sync_reports.py` không ghi hai file này, nên chúng chưa có phần lượt 4); bảng tiếng Anh trong `00-danh-muc-ma-thong-bao.md` đã sửa.
Các đoạn không ghi ở đây giữ nguyên. Trong ô DOCX các bước dính liền nhau (không xuống dòng), nên cột *Câu cũ* ghi đúng như vậy.

Tên cột: **Mục** · **Ô / bước** · **Câu cũ** · **Câu mới**.

## 0. Mặc định đã dùng (plan mục 9 chưa có trả lời)

1. **UC-30 giữ riêng**, chỉ bỏ bộ đếm; không gộp vào UC-27.
2. **"Mở sẵn bộ lọc Đang diễn ra" không thành BR**; chỉ ghi ở UC-17 (và đặc tả màn hình, plan 06).
3. **UC-15 không đổi**: PUT không nhận `participants` (400 MSG31), nên popup sửa không có bước chọn thành phần; popup chỉ là cách trình bày, không nằm trong luồng UC-15.
4. **Hai BR mới:** BR-320 (thành phần ban đầu khi tạo cuộc họp) và BR-321 (chưa trả lời = tham dự; từ chối chỉ ẩn khỏi lịch). "Chủ tọa không trả lời" gộp vào BR-105, không tạo BR riêng.
5. **MSG277** không có trong V.2 (bảng dừng ở MSG274, rồi MSG288–MSG289) nhưng UC-14 mới dẫn tới nó, nên patch thêm luôn cùng MSG285, MSG286. Cả ba thêm ở **cuối bảng V.2** (số thứ tự 258–260) để không đánh lại cột `#`.

## 1. I.4.2 Use Case List

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| I.4.2 | UC-14, cột mô tả | `Create one independent Meeting with its planned start and end times, mode, location, confidentiality and other required details.` | `Create one independent Meeting with its planned start and end times, mode, location, confidentiality, other required details and its initial participants.` |
| I.4.2 | UC-17, cột mô tả | `View, search and filter Meetings by their own schedule, status (including Cancelled), name and confidentiality level.` | `View, search and filter Meetings by their own schedule, status (including Cancelled), name and confidentiality level. The list opens filtered to In Progress Meetings; the time filter offers Today, This week, Not yet held and Already held.` |
| I.4.2 | UC-28, cột mô tả | `Add participants, manage meeting roles and additional permissions, replace the Chair before publication, or remove participants; see UC-28a–UC-28c. From publication onward the Chair hands over the role through UC-73.` | `Add participants after the Meeting is created (the initial participants are chosen in UC-14), manage meeting roles and additional permissions, replace the Chair before publication, or remove participants; see UC-28a–UC-28c. From publication onward the Chair hands over the role through UC-73.` |
| I.4.2 | UC-29, cột mô tả | `Allow an invited internal participant or external Guest to accept or decline their own Meeting invitation, and change a previous response while responses are allowed.` | `Allow an invited internal participant (other than the Chair) or external Guest to accept or decline their own Meeting invitation once; the answer cannot be changed afterwards.` |
| I.4.2 | UC-30, cột mô tả | `View the latest invitation response of current internal participants and external Guests and a response-status summary.` | `View the invitation response of current internal participants and external Guests as a label beside each name.` |

UC-15 không đổi (xem mặc định 3). Cột Primary Actor của UC-29 nếu có ở bảng I.4.2 hoặc Master UC Inventory: bỏ **Chair** (Master UC Inventory là việc chung mục 7 của plan).

## 2. II.4.1 UC-14 – Create Meeting

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| II.4.1 | Description | `Allows an authorized Meeting Organizer to create one independent Meeting as a Draft, including its planned schedule, mode, location and confidentiality.` | `Allows an authorized Meeting Organizer to create one independent Meeting as a Draft, including its planned schedule, mode, location, confidentiality and its initial participants.` |
| II.4.1 | Postconditions, POST-3 | `● POST-3: The Meeting may subsequently be assigned participants, prepared and submitted for approval.` | `● POST-3: The selected participants are saved with the Meeting, each with a Pending invitation response; no invitation is sent while the Meeting is Draft. The Meeting may subsequently be prepared and submitted for approval, and a Secretary may still be assigned later through UC-28a. (BR-320)` |
| II.4.1 | Normal Flow, bước 2 | `2. The system displays the new Meeting form.` | `2. The system displays the new Meeting form as a dialog.` |
| II.4.1 | Normal Flow, bước 3 | `3. The Organizer enters Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location, Confidentiality Level and optional Description.` | `3. The Organizer enters Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location, Confidentiality Level and optional Description, and chooses the initial participants with a Meeting Role for each: exactly one Chair, and optionally a Secretary and Members. (BR-320)` |
| II.4.1 | Normal Flow, bước 5 | `5. The system checks the required values, confirms Planned Start Time is later than the current time and Planned End Time is later than Planned Start Time. (BR-24, BR-25, BR-62, BR-162)` | `5. The system checks the required values, confirms Planned Start Time is later than the current time and Planned End Time is later than Planned Start Time, and checks the chosen participants: each is an existing active internal user listed once, with a valid Meeting Role, and exactly one is the Chair. (BR-24, BR-25, BR-62, BR-162, BR-320)` |
| II.4.1 | Normal Flow, bước 6 | `6. The system creates exactly one Meeting in Draft status and records the creator. (BR-26)` | `6. The system creates exactly one Meeting in Draft status, records the creator and saves the chosen participants with Pending responses, all in the same save. (BR-26, BR-320)` |
| II.4.1 | Exception Flows, thêm sau EX-2 | — | `EX-3 – Invalid Participants` / `1. At Step 5, no participant is chosen, a participant is not an existing active internal user or is listed twice, a Meeting Role is invalid, or the choice does not contain exactly one Chair. (BR-320)` / `2. The system displays MSG277 – "The meeting participant list is not valid." and highlights the participant list.` / `3. No Meeting is created. The Organizer may correct the participants and resubmit at Step 4.` |

Không đổi: Primary Actors, PRE-1, PRE-2, POST-1, POST-2, bước 1, 4, 7, EX-1, EX-2. Lưu ý EX-2 (MSG35): nếu lưu Meeting hoặc thành phần thất bại thì không có Meeting nào được tạo (cùng một transaction).

## 3. II.4.4 UC-17 – View Meeting List

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| II.4.4 | Normal Flow, bước 1 | `1. The user selects Meetings from the navigation menu.` | `1. The user selects Meetings from the navigation menu. The system opens the list with the Status filter preset to In Progress; the user may change or clear it.` |
| II.4.4 | Normal Flow, bước 3 | `3. The system sorts and displays only those authorized meetings, including Cancelled Meetings with their status. (BR-297)` | `3. The system sorts and displays only those authorized meetings that match the preset or selected criteria, including Cancelled Meetings with their status and Meetings the user has declined. (BR-297, BR-321)` |
| II.4.4 | AF-2, bước 1 | `1. At Step 4 of the Main Flow, the user selects one or more filter criteria, such as Meeting Status (including Cancelled), Confidentiality Level, or Time Range.` | `1. At Step 4 of the Main Flow, the user selects one or more filter criteria, such as Meeting Status (including Cancelled), Confidentiality Level, or Time. Time offers four fixed choices: Today, This week, Not yet held and Already held; there is no date-range input.` |
| II.4.4 | AF-2, bước 2 | `2. The system applies the selected criteria to the meetings available to the user. For Time Range, a Meeting matches when its own planned start/end time overlaps the selected time range. (BR-30)` | `2. The system applies the selected criteria to the meetings available to the user. Today and This week match a Meeting whose planned start/end time overlaps that day or week in the user's time zone; Not yet held matches a Meeting whose planned start time is later than the current time; Already held matches a Meeting whose planned start time is the current time or earlier, so a Meeting in progress counts as Already held. (BR-30)` |
| II.4.4 | Alternative Flows, thêm sau AF-3 | — | `AF-4 – View Meetings on the Calendar` / `1. At Step 1 of the Main Flow, the user opens the Calendar instead of the list.` / `2. The system shows the same authorized meetings by week but omits the Meetings the user has declined through UC-29. (BR-321)` / `3. The user browses the displayed meetings.` |

Không đổi: Description, PRE/POST, bước 2, 4, AF-1, AF-3, EX-1, EX-2.

## 4. II.6.2.1 UC-28a – Add Meeting Participant

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| II.6.2.1 | Description | `Allows the Organizer to assign a Secretary when none is current in Draft, Rejected or Approved, initially or after removal; current Secretaries add internal participants or invite a Guest.` | `Allows the Organizer to assign a Secretary when none is current in Draft, Rejected or Approved, initially or after removal; current Secretaries add internal participants or invite a Guest. The initial participants of a new Meeting are chosen in the Create Meeting form (UC-14); this use case adds people afterwards.` |

POST-2 ("New invitations start with Pending response") giữ nguyên. UC-28b, UC-28c không đổi trong lượt này.

## 5. II.6.3 UC-29 – Respond to Meeting Invitation

Ý chính: trả lời là xác nhận trước, **một lần**; Chủ tọa không trả lời; chưa trả lời coi như tham dự; từ chối chỉ ẩn cuộc họp khỏi lịch của người đó. Điểm vào ở đầu màn chi tiết và thẻ cuộc họp trên lịch, không phải tab Thành phần.

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| II.6.3 | Primary Actors | `Chair, Secretary, Participant, Guest` | `Secretary, Participant, Guest` |
| II.6.3 | Description | `Allows an invitee to accept or decline their own Meeting invitation, or change an earlier response while responses are allowed.` | `Allows an invitee other than the Chair to confirm in advance whether they will attend, by accepting or declining their own Meeting invitation. The answer is given once and cannot be changed afterwards. The Chair does not respond because the Chair runs the Meeting. (BR-105, BR-106)` |
| II.6.3 | Preconditions, PRE-2 | `● PRE-2: The actor owns a current invitation; a Guest has verified access through UC-04. (BR-104–BR-106, BR-148–BR-156)` | `● PRE-2: The actor owns a current invitation and is not the Chair of the Meeting; a Guest has verified access through UC-04. (BR-104–BR-106, BR-148–BR-156)` |
| II.6.3 | Postconditions, POST-1 | `● POST-1: The latest response and response time are recorded.` | `● POST-1: The response and its response time are recorded once and are not changed afterwards.` |
| II.6.3 | Postconditions, POST-2 | `● POST-2: Meeting Role, attendance and additional permissions are unchanged. (BR-96, BR-115)` | `● POST-2: Meeting Role, attendance, additional permissions and the invitee's access to the Meeting, its documents and its online room are unchanged. A Declined response only hides the Meeting from the invitee's own calendar, and an invitee who has not answered is treated as attending. (BR-96, BR-115, BR-321)` |
| II.6.3 | Normal Flow, bước 1 | `1. The invitee opens their current Meeting invitation.` | `1. The invitee sees the attendance question at the top of the Meeting detail or on the Meeting card in the calendar list view, and the invitee has not answered yet.` |
| II.6.3 | Normal Flow, bước 2 | `2. The system identifies the invitee and displays the latest response, if any.` | `2. The system identifies the invitee and offers the question only while the invitation has no answer.` |
| II.6.3 | Normal Flow, bước 4 | `4. The system rechecks invitation ownership, Meeting status and response value.` | `4. The system rechecks invitation ownership, that the actor is not the Chair, Meeting status and response value, and compares the value with any response already recorded. (BR-105, BR-106)` |
| II.6.3 | Normal Flow, bước 5 | `5. The system saves the latest response and response time.` | `5. The system saves the response and response time once.` |
| II.6.3 | AF-1, tiêu đề + bước 1–2 | `AF-1 – Change previous response` / `1. At Step 2, the invitee has already responded and the Meeting is still Published or InProgress.` / `2. The invitee selects a different response and continues at Step 3. The selected response and current response time replace the prior current values. (BR-106, BR-115)` | `AF-1 – Same response submitted again` / `1. At Step 4, the invitee submits the same response that is already recorded.` / `2. The system keeps the recorded response and response time unchanged and displays MSG154 – "No changes to save." The use case ends successfully. (BR-106)` |
| II.6.3 | Exception Flows, thêm sau EX-3 | — | `EX-4 – Different response after answering` / `1. At Step 4, the invitee has already answered and submits a different response. (BR-106)` / `2. The recorded response and response time remain unchanged. The system displays MSG285 – "You have already answered this invitation. Your answer cannot be changed." The use case ends unsuccessfully.` |
| II.6.3 | Exception Flows, thêm sau EX-4 | — | `EX-5 – Chair cannot respond` / `1. At Step 4, the actor is the Chair of the Meeting. (BR-105)` / `2. No response is recorded; the Chair's response stays Pending and is not displayed. The system displays MSG286 – "The Chair runs the Meeting, so no attendance confirmation is needed." The use case ends unsuccessfully.` |

Không đổi: Secondary Actors, PRE-1, bước 3, bước 6 (MSG105), EX-1 (MSG107), EX-2 (MSG106), EX-3 (MSG108).

## 6. II.6.4 UC-30 – View Participant Response Status

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| II.6.4 | Description | `Allows authorized internal participants and verified Guests to view current invitation responses and totals for the Meeting.` | `Allows authorized internal participants and verified Guests to view the current invitation responses of the Meeting, shown as a label beside each invitee's name.` |
| II.6.4 | Postconditions, POST-1 | `● POST-1: Latest responses for current invitees and totals by response status are displayed.` | `● POST-1: The responses of current invitees are displayed as labels beside their names. The Chair and an invitee who has not answered have no label. No totals are shown.` |
| II.6.4 | Normal Flow, bước 1 | `1. The actor selects Response Status for an accessible Meeting.` | `1. The actor opens the Participants tab of an accessible Meeting.` |
| II.6.4 | Normal Flow, bước 3 | `3. The system displays each current invitee’s name, email and latest response with totals by status. Response-view permission includes identity in response rows but does not grant roster browsing or search. (BR-103, BR-115, BR-176)` | `3. The system displays each current invitee’s name and email with a label for the response given, Accepted or Declined; an invitee who has not answered and the Chair have no label. No totals by status are shown. Response-view permission includes identity in response rows but does not grant roster browsing or search. (BR-103, BR-115, BR-176, BR-321)` |
| II.6.4 | Normal Flow, bước 4 | `4. The actor reviews the summary.` | `4. The actor reviews the responses.` |
| II.6.4 | EX-1, bước 2 | `2. The system does not disclose response identities or totals and returns the generic not-found result required for inaccessible Meeting data. The use case ends unsuccessfully. (BR-121)` | `2. The system does not disclose response identities or responses and returns the generic not-found result required for inaccessible Meeting data. The use case ends unsuccessfully. (BR-121)` |
| II.6.4 | EX-2, bước 2 và 3 | `2. No partial response summary is presented as complete. The system displays MSG95 – “Unable to load meeting participants. Please try again.”` / `3. The use case ends unsuccessfully; the actor may retry by reopening Response Status.` | `2. No partial response list is presented as complete. The system displays MSG95 – “Unable to load meeting participants. Please try again.”` / `3. The use case ends unsuccessfully; the actor may retry by reopening the Participants tab.` |

Không đổi: Primary Actors, PRE-1, PRE-2, POST-2, bước 2, EX-1 bước 1, EX-2 bước 1.

## 7. V.1 Business Rules

Cột: BR · mô tả · UC · nguồn · nhóm.

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| V.1 | BR-30, cột mô tả | `Meeting time-range search matches when its own planned start/end interval overlaps the selected range.` | `The Meeting list offers fixed time choices. Today and This week match a Meeting whose own planned start/end interval overlaps that day or week in the viewer's time zone. Not yet held matches a Meeting whose planned start time is later than the current time; Already held matches a Meeting whose planned start time is the current time or earlier, so a Meeting in progress counts as Already held.` |
| V.1 | BR-30, cột nguồn | (trống) | `SCRUM-18 (PR #111) — fixed time choices as in AI-eCabinet` |
| V.1 | BR-105, cột mô tả | `Current internal participants and external Guests may respond only Accepted or Declined. Responding does not transfer or change any Meeting Role.` | `Current internal participants and external Guests may respond only Accepted or Declined. The Chair does not respond because the Chair runs the Meeting. Responding does not transfer or change any Meeting Role.` |
| V.1 | BR-105, cột nguồn | (trống) | `PO 21/10/2026 — Chair does not respond` |
| V.1 | BR-106, cột mô tả | `Invitation Responses may be changed while the Meeting is Published or InProgress.` | `Each invitation is answered once while the Meeting is Published or InProgress. After an Accepted or Declined response is recorded it cannot be changed; submitting the same response again is not an error and changes nothing.` |
| V.1 | BR-106, cột nguồn | (trống) | `PO 21/10/2026 — answer once, as in AI-eCabinet` |
| V.1 | BR-115, cột mô tả | `The latest Invitation Response and its response time are recorded for each current invitation.` | `The Invitation Response and its response time are recorded once for each current invitation; an invitation that is not answered stays Pending. The Chair's response stays Pending and is not displayed.` |
| V.1 | BR-176, cột mô tả | `... View Invitation Responses includes names and emails in response rows but does not grant roster browsing/search. ...` (nguyên đoạn: `Before publication, a Chair, Member or Guest cannot view the invited participant roster or invitation responses. From Published onward, Chair has both views by default and Secretary access follows existing rules; a current Member or verified current Guest needs the matching additional permission. View Invitation Responses includes names and emails in response rows but does not grant roster browsing/search. The connected-room list in UC-40 is available to connected Members and Guests without either additional grant.`) | `Before publication, a Chair, Member or Guest cannot view the invited participant roster or invitation responses. From Published onward, Chair has both views by default and Secretary access follows existing rules; a current Member or verified current Guest needs the matching additional permission. View Invitation Responses includes names and emails in response rows but does not grant roster browsing/search. Responses are shown as a label beside each name; there are no totals, and the Chair has no label. The connected-room list in UC-40 is available to connected Members and Guests without either additional grant.` |
| V.1 | **BR-320** (dòng mới cuối bảng, sau BR-319) | — | Mô tả: `When a Meeting is created, the Organizer chooses 1 to 100 initial participants. Each is an existing active internal user listed once, with the Meeting Role Chair, Secretary or Member, and exactly one participant is the Chair. A Secretary is optional, the Organizer need not be a participant, and Guests cannot be added at creation. The participants are saved with the Meeting in one save, each with a Pending response, and no invitation is sent while the Meeting is Draft.` · UC: `UC-14` · Nguồn: `PO 20/10/2026` · Nhóm: `Meeting Management` |
| V.1 | **BR-321** (dòng mới cuối bảng, sau BR-320) | — | Mô tả: `An invitee who has not answered is treated as attending. A Declined response only hides the Meeting from that invitee's own calendar; it does not change the invitee's Meeting Role, access to the Meeting, its documents or its online room, and the Meeting still appears in the Meeting list. The Chair is never hidden, even when an earlier Declined response exists.` · UC: `UC-17, UC-29, UC-30` · Nguồn: `PO 21/10/2026 — as in AI-eCabinet` · Nhóm: `Meeting Participant Management` |

BR-115 trong DOCX gốc không nhắc "totals" và BR-176 cũng không nhắc "totals" (plan nói "nếu có"); BR-176 chỉ thêm câu về nhãn. BR-31 và BR-116 không đổi.
Cột UC của BR-30 (`UC-17`) giữ nguyên. Nếu Khánh muốn đưa quy tắc "Mở sẵn bộ lọc Đang diễn ra" thành BR thì thêm BR-322 (mặc định: không).

## 8. V.2 System Messages

Bảng V.2 (cột: Code · Message type · Where · Message · #). Ba dòng mới **thêm cuối bảng**, sau MSG289 (#257).

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| V.2 | MSG154, cột *Where* (dòng #150) | `UC-28b / Unchanged selection UI; no separate exception flow` | `UC-28b / Unchanged selection UI; no separate exception flow. UC-29 / AF-1 – the same response is submitted again` |
| V.2 | MSG105–MSG108 | — | Không đổi câu, loại hiển thị, *Where*. Đối chiếu: MSG105 (bước 6), MSG106 (EX-2), MSG107 (EX-1), MSG108 (EX-3) vẫn đúng với UC-29 mới. |
| V.2 | Dòng mới #258 | — | `MSG285` · `Toast` · `UC-29 – Respond to Meeting Invitation / EX-4` · `You have already answered this invitation. Your answer cannot be changed.` |
| V.2 | Dòng mới #259 | — | `MSG286` · `Toast` · `UC-29 – Respond to Meeting Invitation / EX-5` · `The Chair runs the Meeting, so no attendance confirmation is needed.` |
| V.2 | Dòng mới #260 | — | `MSG277` · `Form error` · `UC-14 – Create Meeting / EX-3` · `The meeting participant list is not valid.` |

Không xoá mã, không tạo mã mới ngoài ba mã BE đã cấp.

## 9. Change Log (Report 3, cuối bảng)

| Date | A/M/D | Person in charge | Description |
|---|---|---|---|
| 24/10/2026 | M | KhanhPG | Align UC-14, UC-17, UC-28a, UC-29 and UC-30 with SCRUM-18/SCRUM-20 as implemented: UC-14 chooses the initial participants in the creation dialog (new EX-3, MSG277, BR-320); UC-17 opens on In Progress and offers fixed time choices (BR-30), calendar hides declined Meetings (AF-4); UC-29 is answered once, the Chair does not respond, same answer again is MSG154, a different answer is MSG285, the Chair gets MSG286 (BR-105, BR-106, BR-115, BR-321); UC-30 shows labels instead of totals (BR-176). |

(Nếu dùng chung một dòng Change Log với lượt khác cùng ngày 24/10/2026, gộp mô tả.)

## 10. Ngoài phạm vi — ghi Jira, không có câu dán

Ghi vào comment Jira SCRUM-18 / SCRUM-20 (hoặc ticket đặc tả màn hình, [plan 06](../06-plan-dac-ta-man-hinh.md)):

1. **I.3.2 entity** *InvitationResponse* (participant và guest): mô tả hiện chỉ nói "Answer to the meeting invitation. Values: Pending, Accepted, Declined" — không ghi "latest/may change" nên không cần sửa. Có thể thêm "set once; the Chair's stays Pending" nếu BA muốn.
2. **Permission Matrix I.4.4**: dòng UC-29 bỏ cột Chair. Ô Primary Actors của UC-29 ở các bảng actor khác (Master UC Inventory) cũng bỏ Chair.
3. **Ảnh I.4.3.4** (use case diagram theo actor): thay bằng `uc-chair.png` mới — đã xuất lại (xem commit); Khánh chèn vào Report 3.
4. **Danh mục màn hình I.4.5 (6.4 Invitation Response)**: mô tả "…and to change the response until the meeting ends" còn sót; thuộc đặc tả màn hình.
5. **Đặc tả màn hình III** (plan 06):
   - 4.3 Meeting List (dòng 6059 "Filter … by status and by time range (month)"): đổi thành bộ lọc Đang diễn ra mở sẵn + 4 lựa chọn thời gian.
   - 4.4 Create / Edit Meeting (dòng 6097–6100): popup; thêm chọn thành phần; Update không có thành phần.
   - Participants tab (dòng 6438 "latest invitation response and the response totals", 6463 "latest response, response time and totals"): nhãn cạnh tên, không bộ đếm, Chủ tọa không nhãn.
   - 6.4 Invitation Response (dòng 6538, 6543 "Change Response"): bỏ "Change Response"; điểm vào ở đầu chi tiết và thẻ lịch, không trả lời trong tab Thành phần.
   - Email lời mời (dòng 7365 "may accept or decline and change their own response while responses are allowed (BR-105)"): đổi thành "answer once".
6. **Ghi nhận khác** (không thuộc lượt này): UC-28a/UC-28b còn nói "additional permissions" và "Recovery Secretary" dù PO 20/10 đã bỏ routes `secretary-recovery`, `guest-invitations/{id}/permissions` (API 09 §4.1 cuối); MSG152/MSG153 đã ngừng dùng mà Report 3 vẫn dẫn. Nên rà ở lượt riêng.
7. **Sequence PNG**: xuất bằng draw.io CLI cho 4 sequence + SM-04 + use case Chair; xem [README](README.md) nếu Khánh muốn xuất lại bằng `sync_sequence_drawio_png.py`.
