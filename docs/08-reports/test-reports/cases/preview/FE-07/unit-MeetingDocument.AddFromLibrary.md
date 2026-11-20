# MeetingDocument.AddFromLibrary — Unit (FE-07, DOC)

Description: Adding a library document to a meeting, removing and detaching meeting documents.

Precondition: Meeting with documents; personal library documents.

## MeetingDocumentService.AddFromLibraryAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:172`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published; actor is Chair; two library documents owned by the Chair, files not yet in the meeting. | documentIds: [D1, D2]. | Return: No failure; two UploadedMeetingDocument rows; endpoint answers MSG161.<br>Exception: N/A<br>Log Message: Two MeetingDocument rows added pointing at the same stored files (no bytes copied); one save. | Normal | UC-33a · BR-209 · MSG161 |
| UTC002 | Same meeting. | documentIds: [D1, D1]. | Return: No failure; one document added; duplicate IDs are collapsed.<br>Exception: N/A<br>Log Message: One MeetingDocument row. | Boundary | UC-33a · BR-209 |
| UTC003 | Same meeting. | documentIds: [] (nothing chosen). | Return: Failure LibraryUnavailable (HTTP 400, MSG249).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · MSG249 |
| UTC004 | D2 belongs to another user. | documentIds: [D1, D2]. | Return: Failure LibraryUnavailable (HTTP 400, MSG249); D1 is not added either.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · BR-209 · MSG249 |
| UTC005 | D1 was deleted from the library. | documentIds: [D1]. | Return: Failure LibraryUnavailable (HTTP 400, MSG249).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · BR-209 · MSG249 |
| UTC006 | The meeting already holds the file of D1 as a general document. | documentIds: [D1]. | Return: Failure AlreadyInMeeting (HTTP 409, MSG165).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · BR-193 · MSG165 |
| UTC007 | Chair earlier removed the file of D1 from the meeting. | documentIds: [D1]. | Return: No failure; a new MeetingDocument row is created and the old comments do not come back.<br>Exception: N/A<br>Log Message: New MeetingDocument row (old removed row kept as history). | Boundary | UC-33a · BR-212 · BR-284 |
| UTC008 | Actor is a Member of the meeting. | documentIds: [D1]. | Return: Failure Forbidden (HTTP 403, MSG162).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · BR-184 · MSG162 |
| UTC009 | Meeting Ended. | documentIds: [D1]. | Return: Failure Ended (HTTP 409, MSG172).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · BR-195 · MSG172 |
| UTC010 | Meeting Cancelled. | documentIds: [D1]. | Return: Failure Cancelled (HTTP 409, MSG349).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · BR-299 · MSG349 |
| UTC011 | Meeting not found or not readable by the actor. | documentIds: [D1]. | Return: Failure NotFound (HTTP 404, MSG174).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a · MSG174 |
| UTC012 | Actor deactivated. | documentIds: [D1]. | Return: Failure Inactive; the endpoint answers 403 with no message code.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-33a |
| UTC013 | Database error on save. | documentIds: [D1]. | Return: Failure StoreFailed (HTTP 503, MSG166).<br>Exception: N/A<br>Log Message: Error log "Failed to add library documents to meeting". | Abnormal | UC-33a · MSG166 |
