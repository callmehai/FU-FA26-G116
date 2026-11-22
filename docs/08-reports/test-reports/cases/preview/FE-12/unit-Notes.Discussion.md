# Notes.Discussion — Unit (FE-12, MIN)

Description: Notes folded into the minutes template: discussion per agenda item and reading the meeting notes in order.

Precondition: Minutes records with agenda and notes.

## Notes.Discussion

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesTemplate.cs:145`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Notes with a meeting-wide note "Mở đầu". | itemId: null. | Return: ["Mở đầu"].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 · UC-82 |
| UTC002 | Item 1 has two unmarked lines. | itemId: item 1. | Return: The two lines in note order.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 · UC-82 |
| UTC003 | Item has only a marked line ("Kết luận: Duyệt"). | itemId: that item. | Return: Empty list; the line moved to section III.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 · UC-82 |
| UTC004 | Id of an item without notes or not in the agenda. | itemId: an unknown id. | Return: Empty list.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · UC-82 |
| UTC005 | No meeting-wide note. | itemId: null. | Return: Empty list.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 · UC-82 |
