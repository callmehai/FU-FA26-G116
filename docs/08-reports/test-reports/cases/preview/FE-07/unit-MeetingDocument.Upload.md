# MeetingDocument.Upload — Unit (FE-07, DOC)

Description: Uploading a document to a meeting: pre-check, quota and persistence.

Precondition: Meeting in a document-preparation status; file storage replaced by a recorder.

## MeetingDocumentService.UploadAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:158`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting "Họp HĐQT" planned 2026-12-15 (Vietnam time), Published; actor is Chair with no upload folder yet.<br>2. Storage has room; storage recorder accepts writes. | files: one validated PDF "Bao-cao.pdf", 2 MB, 12 pages. | Return: Outcome with no failure and one document (name "Bao-cao.pdf", application/pdf, 2,097,152 bytes, 12 pages); endpoint answers MSG161.<br>Exception: N/A<br>Log Message: StoredFile row saved and object written under stored-files/<id>; library folder "Họp HĐQT – 15/12/2026" created for the Chair; Document and MeetingDocument rows committed in one transaction. | Normal | UC-33a · BR-192 · BR-226 · MSG161 |
| UTC002 | Meeting Ended while the files were uploading (re-checked under the meeting lock). | files: one validated PDF. | Return: Failure Ended (HTTP 409, MSG172); no document is added.<br>Exception: N/A<br>Log Message: Written objects deleted and StoredFile rows stamped purged (released). | Abnormal | UC-33a · BR-195 · MSG172 |
| UTC003 | Actor lost the Secretary role while uploading. | files: one validated PDF. | Return: Failure Forbidden (HTTP 403, MSG162).<br>Exception: N/A<br>Log Message: Objects released. | Abnormal | UC-33a · BR-184 · MSG162 |
| UTC004 | Storage used is 100 MB; limit is 100 MB + 1 MB (stored files total). | files: one file of 1 MB. | Return: No failure; used plus new equals the limit exactly and is accepted.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-33a · BR-319 |
| UTC005 | Storage used is 100 MB; limit is 100 MB + 1 MB - 1 byte. | files: one file of 1 MB. | Return: Failure StorageFull (HTTP 409, MSG346); nothing is stored.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-33a · MSG346 |
| UTC006 | The database fails while the StoredFile rows are reserved. | files: one validated PDF. | Return: Failure StoreFailed (HTTP 503, MSG166).<br>Exception: N/A<br>Log Message: Error log "Failed to record uploaded files" | Abnormal | UC-33a · MSG166 |
