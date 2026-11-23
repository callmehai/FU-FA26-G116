# MeetingDocument.AddFromLibrary — Unit (FE-07, DOC)

Description: Adding a library document to a meeting, removing and detaching meeting documents.

Precondition: Meeting with documents; personal library documents.

## MeetingDocumentService.AddFromLibraryAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:172`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published; actor is Chair; two library documents owned by the Chair, files not yet in the meeting. | documentIds: [D1, D2]. | Return: No failure; two UploadedMeetingDocument rows; endpoint answers MSG161.<br>Exception: N/A<br>Log Message: Two MeetingDocument rows added pointing at the same stored files (no bytes copied); one save. | Normal | UC-33a · BR-209 · MSG161 |
| UTC002 | Same meeting. | documentIds: [] (nothing chosen). | Return: Failure LibraryUnavailable (HTTP 400, MSG249).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · MSG249 |
| UTC003 | The meeting already holds the file of D1 as a general document. | documentIds: [D1]. | Return: Failure AlreadyInMeeting (HTTP 409, MSG165).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · BR-193 · MSG165 |
| UTC004 | Actor is a Member of the meeting. | documentIds: [D1]. | Return: Failure Forbidden (HTTP 403, MSG162).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · BR-184 · MSG162 |
| UTC005 | Meeting Ended. | documentIds: [D1]. | Return: Failure Ended (HTTP 409, MSG172).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · BR-195 · MSG172 |
| UTC006 | One candidate of exactly 10 MB with a valid PDF body. | candidates: [{FileName "lon.pdf", Length 10,485,760}]. | Return: No failure; the file size limit is inclusive.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · MSG174 |
