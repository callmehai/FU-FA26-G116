# MeetingDocument.OpenContent — Unit (FE-07, DOC)

Description: Opening document content: who may read a meeting or library file.

Precondition: Documents with sensitivity and readers seeded.

## MeetingDocumentService.OpenContentAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/MeetingDocumentService.cs:47`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published (Normal sensitivity); reader is a Member; M1 is a PDF in the meeting; the file exists in storage. | meetingDocumentId: M1; download: false. | Return: Content stream, file name, content type and size of M1; no audit row.<br>Exception: N/A<br>Log Message: None. | Normal | UC-32b · BR-183 · BR-216 |
| UTC002 | Meeting is Secret. | meetingDocumentId: M1; download: true. | Return: Failure DownloadForbidden (HTTP 403, MSG159); the file is not opened.<br>Exception: N/A<br>Log Message: No audit row. | Abnormal | UC-32b · BR-188 · MSG159 |
| UTC003 | Reader cannot read the meeting (not creator/participant). | meetingDocumentId: M1. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-32b · MSG156 |
| UTC004 | One candidate of exactly 10 MB with a valid PDF body. | candidates: [{FileName "lon.pdf", Length 10,485,760}]. | Return: No failure; the file size limit is inclusive.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-32b · BR-253 |
| UTC005 | Stored object is gone for good (storage reports file not found). | meetingDocumentId: M1; download: false. | Return: Failure FileMissing (HTTP 410, MSG382).<br>Exception: N/A<br>Log Message: Error log "Failed to open stored object". | Abnormal | UC-32b · MSG382 |
| UTC006 | Storage is down or not configured; viewing. | meetingDocumentId: M1; download: false. | Return: Failure ReadFailed (HTTP 503, MSG158).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-32b · MSG158 |
