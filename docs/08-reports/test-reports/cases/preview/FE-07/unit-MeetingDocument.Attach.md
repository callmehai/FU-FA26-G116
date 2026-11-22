# MeetingDocument.Attach — Unit (FE-07, DOC)

Description: Adding a library document to a meeting, removing and detaching meeting documents.

Precondition: Meeting with documents; personal library documents.

## MeetingDocumentService.AttachAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:192`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published; Chair; agenda item A1 exists; documents M1, M2 are in the meeting and not linked to A1. | agendaItemId: A1; source: existing meeting documents; documentIds: [M1, M2]. | Return: No failure; endpoint answers MSG238.<br>Exception: N/A<br>Log Message: Two links (M1-A1, M2-A1) added; links to other items untouched. | Normal | UC-33b · BR-277 · MSG238 |
| UTC002 | M1 already linked to A1. | agendaItemId: A1; documentIds: [M1]. | Return: Failure AlreadyLinked (HTTP 409, MSG243).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-277 · MSG243 |
| UTC003 | Same meeting. | agendaItemId: A1; documentIds: []. | Return: Failure NotInMeeting (HTTP 404, MSG170).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · MSG170 |
| UTC004 | Ten files whose lengths add to more than 100 MB. | candidates: 10 files of 11 MB each (115,343,360 bytes; each also exceeds the 10 MB per-file limit, but the submission-total gate fires first). | Return: Failure TooMany (HTTP 400, MSG288).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-278 · MSG242 |
| UTC005 | One candidate of 10 MB plus 1 byte. | candidates: [{FileName "lon.pdf", Length 10,485,761}]. | Return: Failure Invalid (MSG164); errors["files[0]"] = ["lon.pdf: tệp lớn hơn 10 MB."]; the bytes are not read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-184 · MSG240 |
| UTC006 | Meeting Ended. | agendaItemId: A1; documentIds: [M1]. | Return: Failure Ended (HTTP 409, MSG247).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-195 · MSG247 |
