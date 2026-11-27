# MeetingDocument.OpenContent — Unit (FE-07, DOC)

Description: Opening document content: who may read a meeting or library file.

Precondition: Documents with sensitivity and readers seeded.

## MeetingDocumentService.OpenContentAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:47`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published (Normal sensitivity); reader is a Member; M1 is a PDF in the meeting; the file exists in storage. | meetingDocumentId: M1; download: false. | Return: Content stream, file name, content type and size of M1; no audit row.<br>Exception: N/A<br>Log Message: None. | Normal | UC-35b · BR-068 |
| UTC002 | Meeting is Secret. | meetingDocumentId: M1; download: true. | Return: Failure DownloadForbidden (HTTP 403, MSG159); the file is not opened.<br>Exception: N/A<br>Log Message: No audit row. | Abnormal | UC-35b · BR-081 · MSG159 |
| UTC003 | Reader cannot read the meeting (not creator/participant). | meetingDocumentId: M1. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35b · MSG156 |
| UTC004 | Meeting InProgress; reader is the Secretary; M1 is attached only to a draft vote; the file exists in storage. | meetingDocumentId: M1; download: false. | Return: Content stream, file name, content type and size of M1; Chair and Secretary see draft-vote documents.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-35b · BR-095 |
| UTC005 | Stored object is gone for good (storage reports file not found). | meetingDocumentId: M1; download: false. | Return: Failure FileMissing (HTTP 410, MSG382).<br>Exception: N/A<br>Log Message: Error log "Failed to open stored object". | Abnormal | UC-35b · MSG382 |
| UTC006 | Meeting Published (Internal sensitivity); reader is a verified Guest of this meeting; M1 is a PDF in the meeting. | meetingDocumentId: M1; download: true. | Return: Failure DownloadForbidden (HTTP 403, MSG485); the file is not opened.<br>Exception: N/A<br>Log Message: No audit row. | Abnormal | UC-35b · BR-055 · BR-081 · MSG485 |
| UTC007 | Meeting Published (Internal sensitivity); reader is an internal Member; M1 is a PDF in the meeting; the file exists in storage. | meetingDocumentId: M1; download: true. | Return: Content stream, file name, content type and size of M1; audit row "document.downloaded" recorded before streaming.<br>Exception: N/A<br>Log Message: Audit row document.downloaded recorded. | Normal | UC-35b · BR-010 · BR-081 |
