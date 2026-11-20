# MeetingDocument.OpenContent — Unit (FE-07, DOC)

Description: Opening document content: who may read a meeting or library file.

Precondition: Documents with sensitivity and readers seeded.

## MeetingDocumentService.OpenContentAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:47`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published (Normal sensitivity); reader is a Member; M1 is a PDF in the meeting; the file exists in storage. | meetingDocumentId: M1; download: false. | Return: Content stream, file name, content type and size of M1; no audit row.<br>Exception: N/A<br>Log Message: None. | Normal | UC-32b · BR-183 · BR-216 |
| UTC002 | Same meeting; reader is a Member. | meetingDocumentId: M1; download: true. | Return: Content returned (download allowed for a non-Secret meeting).<br>Exception: N/A<br>Log Message: None. | Normal | UC-32b · BR-188 |
| UTC003 | Meeting is Secret; reader is a Member; viewing. | meetingDocumentId: M1; download: false. | Return: Content returned.<br>Exception: N/A<br>Log Message: Audit "document.viewed" on MeetingDocument M1 by the reader with details meetingId, fileName, sensitivity Secret. | Normal | UC-32b · BR-188 · BR-318 |
| UTC004 | Meeting is Secret. | meetingDocumentId: M1; download: true. | Return: Failure DownloadForbidden (HTTP 403, MSG159); the file is not opened.<br>Exception: N/A<br>Log Message: No audit row. | Abnormal | UC-32b · BR-188 · MSG159 |
| UTC005 | Meeting is Secret; reader is a guest with a current session. | reader: Guest; download: false. | Return: Content returned.<br>Exception: N/A<br>Log Message: Audit "document.viewed" with details including guestInvitationId. | Normal | UC-32b · BR-181 · BR-318 |
| UTC006 | Reader cannot read the meeting (not creator/participant). | meetingDocumentId: M1. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-32b · MSG156 |
| UTC007 | Meeting does not exist. | meetingId: unknown. | Return: Failure NotFound (HTTP 404, MSG156), same answer as unreadable.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-32b · MSG156 |
| UTC008 | M1 was removed from the meeting. | meetingDocumentId: M1. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-32b · BR-211 · MSG156 |
| UTC009 | M2 belongs to another meeting. | meetingDocumentId: M2. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-32b · BR-211 · MSG156 |
| UTC010 | M3 is attached only to a draft vote; reader is a Member. | meetingDocumentId: M3. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-32b · BR-253 · MSG156 |
| UTC011 | Same draft-vote document; reader is the Chair. | meetingDocumentId: M3. | Return: Content returned; managers see draft-vote documents.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-32b · BR-253 |
| UTC012 | Guest whose session is invalid (signed out or expired). | reader: Guest with IsValid = false. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · MSG156 |
| UTC013 | Guest session belongs to another meeting. | reader: Guest of meeting X; meetingId: Y. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · MSG156 |
| UTC014 | Guest session valid but the meeting is Ended. | reader: Guest. | Return: Failure NotFound (HTTP 404, MSG156); guests lose the meeting once it is no longer Published or InProgress.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-04 · BR-181 · MSG156 |
| UTC015 | Stored object is gone for good (storage reports file not found). | meetingDocumentId: M1; download: false. | Return: Failure FileMissing (HTTP 410, MSG382).<br>Exception: N/A<br>Log Message: Error log "Failed to open stored object". | Abnormal | UC-32b · MSG382 |
| UTC016 | Storage is down or not configured; viewing. | meetingDocumentId: M1; download: false. | Return: Failure ReadFailed (HTTP 503, MSG158).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-32b · MSG158 |
| UTC017 | Storage is down; downloading. | meetingDocumentId: M1; download: true. | Return: Failure ReadFailed (HTTP 503, MSG160).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-32b · MSG160 |
| UTC018 | Database error while looking up the document. | meetingDocumentId: M1. | Return: Failure ReadFailed (HTTP 503, MSG158).<br>Exception: N/A<br>Log Message: Error log "Failed to look up document". | Abnormal | UC-32b · MSG158 |
| UTC019 | Meeting is Secret; the audit write fails with a database error. | meetingDocumentId: M1; download: false. | Return: Failure ReadFailed (HTTP 503, MSG158); no content is returned and the opened stream is disposed.<br>Exception: N/A<br>Log Message: No audit row saved. | Abnormal | UC-32b · BR-318 · MSG158 |
| UTC020 | Meeting is Secret; the audit call throws a non-database exception. | meetingDocumentId: M1. | Return: No result; the opened stream is disposed first.<br>Exception: The original exception is rethrown.<br>Log Message: N/A | Abnormal | UC-32b · BR-318 |
| UTC021 | Storage open is cancelled by the caller. | ct cancelled. | Return: No result.<br>Exception: OperationCanceledException<br>Log Message: N/A | Abnormal | UC-32b |
