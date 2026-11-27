# MeetingDocument.Attach — Unit (FE-07, DOC)

Description: Adding a library document to a meeting, removing and detaching meeting documents.

Precondition: Meeting with documents; personal library documents.

## MeetingDocumentService.AttachAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:192`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published; Chair; agenda item A1 exists; documents M1, M2 are in the meeting and not linked to A1. | agendaItemId: A1; source: existing meeting documents; documentIds: [M1, M2]. | Return: No failure; endpoint answers MSG238.<br>Exception: N/A<br>Log Message: Two links (M1-A1, M2-A1) added; links to other items untouched. | Normal | UC-36b · MSG238 |
| UTC002 | M1 already linked to A1. | agendaItemId: A1; documentIds: [M1]. | Return: Failure AlreadyLinked (HTTP 409, MSG243).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-36b · MSG243 |
| UTC003 | Meeting Published; Secretary; M1 is in the meeting; agendaItemId does not belong to this meeting. | agendaItemId: unknown id; source: existing meeting documents; documentIds: [M1]. | Return: Failure AgendaItemNotFound (HTTP 404, MSG242); no link added.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-36b · BR-078 · MSG242 |
| UTC004 | Meeting Published; actor is a Member (not Chair or Secretary); M1 is in the meeting; agendaItemId A1 exists. | agendaItemId: A1; source: existing meeting documents; documentIds: [M1]. | Return: Failure Forbidden (HTTP 403, MSG240); no link added.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-36b · BR-070, BR-071 · MSG240 |
