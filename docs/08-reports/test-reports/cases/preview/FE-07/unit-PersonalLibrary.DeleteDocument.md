# PersonalLibrary.DeleteDocument — Unit (FE-07, DOC)

Description: Personal library documents: upload with quota, rename, move, delete.

Precondition: Owner with folders; file storage recorder.

## PersonalLibraryService.DeleteDocumentAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/PersonalLibraryService.cs:230`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Owner U1 has D1 used by two meetings. | documentId: D1. | Return: None; endpoint answers MSG180.<br>Exception: N/A<br>Log Message: Library row of D1 deleted; the meeting copies keep their stored file (DocumentId becomes null). | Normal | UC-71a · BR-186 · BR-213 · MSG180 |
| UTC002 | D9 missing or another user's. | documentId: D9. | Return: NotFound (HTTP 404, MSG175).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-71a · BR-242 · MSG175 |
| UTC003 | D1 already deleted. | documentId: D1. | Return: NotFound (HTTP 404, MSG175); delete affects 0 rows.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-71a · MSG175 |
| UTC004 | Database error. | documentId: D1. | Return: SaveFailed (HTTP 503, MSG181); the library is unchanged.<br>Exception: N/A<br>Log Message: Error log "Failed to delete a document". | Abnormal | UC-71a · MSG181 |
