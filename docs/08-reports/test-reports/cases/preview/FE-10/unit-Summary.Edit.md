# Summary.Edit — Unit (FE-10, TRANS)

Description: Editing and reviewing a summary draft.

Precondition: Summary draft exists.

## SummaryService.EditAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/SummaryService.cs:125`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A Ready draft exists; actor is the current chair. | meetingId: a meeting with a Ready draft; actorId: its current chair; edit: three corrected sections. | Return: Success with the fresh state (MSG388 "Đã lưu dự thảo."); the corrected text is stored and the review is cleared, so others stop seeing the draft until it is reviewed again.<br>Exception: N/A<br>Log Message: Saved: corrected content with editor and edit time; review cleared. | Normal | UC-49 · BR-377 · MSG388 |
| UTC002 | No draft exists yet; actor is the current chair. | meetingId: a meeting without a draft; actorId: its current chair; edit: three sections. | Return: Failure NotDrafted (MSG390 "Chưa có dự thảo để sửa hoặc duyệt."); nothing is saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-49 · MSG390 |
| UTC003 | A Ready draft exists; actor is the current chair. | meetingId: a meeting with a Ready draft; actorId: its current chair; edit: sections at exactly 2.000, 12.000 and 8.000 characters. | Return: Success (MSG388); sections at the limits are accepted.<br>Exception: N/A<br>Log Message: Saved: corrected content with editor and edit time; review cleared. | Boundary | UC-49 · MSG388 |
| UTC004 | A Ready draft exists; actor takes part but is neither chair nor secretary. | meetingId: a meeting with a Ready draft; actorId: an ordinary member; edit: three sections. | Return: Failure Forbidden (MSG385 "Chỉ Chủ toạ hoặc Thư ký được tạo, sửa và duyệt dự thảo."); nothing is saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-49 · BR-374 · MSG385 |
