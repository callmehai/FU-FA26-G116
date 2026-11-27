# Notes.Read — Unit (FE-12, MIN)

Description: Notes folded into the minutes template: discussion per agenda item and reading the meeting notes in order.

Precondition: Minutes records with agenda and notes.

## Notes.Read

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesTemplate.cs:148`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Records with agenda items 1 "Báo cáo" and 2 "Ngân sách". | Notes: item 2 "Chốt giá" is listed before the meeting-wide note "Mở đầu". | Return: Meeting-wide note comes first, then items in agenda order; item 2 keeps its line under item 2.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-70 · UC-28 · Run through MinutesTemplate.Build (the reader is private): the section it feeds is what is read. |
| UTC002 | Records with agenda items 1 "Báo cáo" and 2 "Ngân sách". | Note on item 1: "Kết luận: Duyệt". | Return: Conclusions: "- Báo cáo: Duyệt"; nothing is left in the item's discussion.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-70 · UC-28 · Run through MinutesTemplate.Build (the reader is private): the section it feeds is what is read. |
| UTC003 | Records with agenda items 1 "Báo cáo" and 2 "Ngân sách". | Meeting-wide note: "Nhiệm vụ: Gửi biên bản". | Return: Tasks: "- Gửi biên bản" (no item prefix).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-70 · UC-28 · Run through MinutesTemplate.Build (the reader is private): the section it feeds is what is read. |
| UTC004 | Records with agenda items 1 "Báo cáo" and 2 "Ngân sách". | Note on item 1: "Kết luận:" with nothing after the colon. | Return: Not a conclusion; the line "Kết luận:" stays in item 1's discussion.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-70 · UC-28 · Run through MinutesTemplate.Build (the reader is private): the section it feeds is what is read. |
