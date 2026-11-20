# PersonalLibrary.Upload — Unit (FE-07, DOC)

Description: Personal library documents: upload with quota, rename, move, delete.

Precondition: Owner with folders; file storage recorder.

## PersonalLibraryService.UploadAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/PersonalLibraryService.cs:187`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Owner U1 with folder F1; storage has room. | folderId: F1; files: "Bao-cao.pdf" (2 MB, 12 pages) and "Nghi-quyet.docx" (40 KB). | Return: Value: two LibraryDocument rows (names, content types, sizes, page count 12 and null, FolderId F1, MeetingCount 0); endpoint answers MSG177 with addedCount 2.<br>Exception: N/A<br>Log Message: Two StoredFile rows, two objects written, two Document rows with OwnerId U1 and FolderId F1, in one transaction; library locked for the owner. | Normal | UC-36 · BR-229 · MSG177 |
| UTC002 | Owner U1. | folderId: null; files: one PDF. | Return: Value: one document at the library root.<br>Exception: N/A<br>Log Message: Document row with FolderId null. | Normal | UC-36 · MSG177 |
| UTC003 | Owner uploads a file with the same name and bytes twice. | files: two files named "a.pdf" with identical bytes. | Return: Two documents with different IDs.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-36 · BR-229 |
| UTC004 | Folder F9 was deleted while the files were uploading. | folderId: F9; files: one PDF. | Return: Failure DestinationNotFound (HTTP 404, MSG179).<br>Exception: N/A<br>Log Message: Objects released; StoredFile rows stamped purged. | Abnormal | UC-36 · MSG179 |
| UTC005 | Storage used 10 MB, limit 11 MB. | files: one 1 MB file. | Return: Value returned; exactly at the limit.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-36 · ADR-0034 |
| UTC006 | Storage used 10 MB, limit 11 MB. | files: one file of 1 MB + 1 byte. | Return: Failure StorageFull (HTTP 409, MSG346).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-36 · MSG346 |
| UTC007 | Storage write fails. | files: one PDF. | Return: Failure SaveFailed (HTTP 503, MSG178).<br>Exception: N/A<br>Log Message: Objects released. | Abnormal | UC-36 · BR-286 · MSG178 |
| UTC008 | Database error on commit. | files: one PDF. | Return: Failure SaveFailed (HTTP 503, MSG178).<br>Exception: N/A<br>Log Message: Objects released. | Abnormal | UC-36 · MSG178 |
