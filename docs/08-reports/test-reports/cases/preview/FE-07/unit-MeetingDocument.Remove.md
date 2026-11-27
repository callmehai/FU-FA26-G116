# MeetingDocument.Remove — Unit (FE-07, DOC)

Description: Adding a library document to a meeting, removing and detaching meeting documents.

Precondition: Meeting with documents; personal library documents.

## MeetingDocumentService.RemoveAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:177`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting Published; actor is Chair; MeetingDocument M1 in the meeting with a link to agenda item 2.<br>2. Clock: 2026-11-18T03:00Z. | meetingDocumentId: M1; actorId: Chair. | Return: No failure; endpoint answers MSG167.<br>Exception: N/A<br>Log Message: M1 stamped RemovedAt 2026-11-18T03:00Z, RemovedById Chair, UpdatedAt set; its agenda-item links deleted; the row and its comments kept as history. | Normal | UC-36c · BR-011 · BR-074–BR-076 · MSG167 |
| UTC002 | M1 already removed. | meetingDocumentId: M1. | Return: Failure NotInMeeting (HTTP 404, MSG170).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-36c · BR-074–BR-076 · MSG170 |
| UTC003 | Actor is a Member. | meetingDocumentId: M1. | Return: Failure Forbidden (HTTP 403, MSG169).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-36c · BR-070, BR-071 · MSG169 |
| UTC004 | Meeting Ended. | meetingDocumentId: M1. | Return: Failure Ended (HTTP 409, MSG248).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-36c · BR-070, BR-071 · MSG248 |
