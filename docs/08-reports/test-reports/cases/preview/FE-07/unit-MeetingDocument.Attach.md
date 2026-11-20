# MeetingDocument.Attach — Unit (FE-07, DOC)

Description: Adding a library document to a meeting, removing and detaching meeting documents.

Precondition: Meeting with documents; personal library documents.

## MeetingDocumentService.AttachAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:192`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published; Chair; agenda item A1 exists; documents M1, M2 are in the meeting and not linked to A1. | agendaItemId: A1; source: existing meeting documents; documentIds: [M1, M2]. | Return: No failure; endpoint answers MSG238.<br>Exception: N/A<br>Log Message: Two links (M1-A1, M2-A1) added; links to other items untouched. | Normal | UC-33b · BR-277 · MSG238 |
| UTC002 | M1 is already linked to a different item A2. | agendaItemId: A1; documentIds: [M1]. | Return: No failure; M1 is now linked to A1 and still to A2.<br>Exception: N/A<br>Log Message: Link M1-A1 added. | Boundary | UC-33b · BR-277 |
| UTC003 | M1 already linked to A1. | agendaItemId: A1; documentIds: [M1]. | Return: Failure AlreadyLinked (HTTP 409, MSG243).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-277 · MSG243 |
| UTC004 | M1 not linked, M2 already linked to A1. | agendaItemId: A1; documentIds: [M1, M2]. | Return: Failure AlreadyLinked (MSG243); no link is added.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-277 · MSG243 |
| UTC005 | Same meeting. | agendaItemId: A1; documentIds: []. | Return: Failure NotInMeeting (HTTP 404, MSG170).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · MSG170 |
| UTC006 | M9 belongs to another meeting. | agendaItemId: A1; documentIds: [M1, M9]. | Return: Failure NotInMeeting (HTTP 404, MSG170).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-211 · MSG170 |
| UTC007 | Agenda item removed or from another meeting. | agendaItemId: A9; documentIds: [M1]. | Return: Failure AgendaItemNotFound (HTTP 404, MSG242).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-278 · MSG242 |
| UTC008 | Library documents D1 (owned by the Chair, file not in the meeting). | agendaItemId: A1; source: Library; documentIds: [D1]. | Return: No failure; one new meeting document added and linked to A1.<br>Exception: N/A<br>Log Message: MeetingDocument row with an agenda link to A1. | Normal | UC-33b · BR-277 · MSG238 |
| UTC009 | D1 file already in the meeting as M1, not linked to A1. | agendaItemId: A1; source: Library; documentIds: [D1]. | Return: No failure; no new document, the existing meeting document M1 is linked to A1.<br>Exception: N/A<br>Log Message: Link M1-A1 added only. | Boundary | UC-33b · BR-193 |
| UTC010 | D1 file already in the meeting and already linked to A1. | agendaItemId: A1; source: Library; documentIds: [D1]. | Return: Failure AlreadyLinked (HTTP 409, MSG243).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · MSG243 |
| UTC011 | D1 not in the actor's library. | agendaItemId: A1; source: Library; documentIds: [D1]. | Return: Failure LibraryUnavailable (HTTP 400, MSG249).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-209 · MSG249 |
| UTC012 | Actor is a Member. | agendaItemId: A1; documentIds: [M1]. | Return: Failure Forbidden (HTTP 403, MSG240).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-184 · MSG240 |
| UTC013 | Meeting Ended. | agendaItemId: A1; documentIds: [M1]. | Return: Failure Ended (HTTP 409, MSG247).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-195 · MSG247 |
| UTC014 | Meeting Cancelled. | agendaItemId: A1; documentIds: [M1]. | Return: Failure Cancelled (HTTP 409, MSG349).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33b · BR-299 · MSG349 |
| UTC015 | Database error on save. | agendaItemId: A1; documentIds: [M1]. | Return: Failure StoreFailed (HTTP 503, MSG244).<br>Exception: N/A<br>Log Message: Error log "Failed to link documents in meeting". | Abnormal | UC-33b · MSG244 |
