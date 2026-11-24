# PersonalLibrary.Upload — Unit (FE-07, DOC)

Description: Personal library documents: upload with quota, rename, move, delete.

Precondition: Owner with folders; file storage recorder.

## PersonalLibraryService.UploadAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/PersonalLibraryService.cs:187`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Owner U1 with folder F1; storage has room. | folderId: F1; files: "Bao-cao.pdf" (2 MB, 12 pages) and "Nghi-quyet.docx" (40 KB). | Return: Value: two LibraryDocument rows (names, content types, sizes, page count 12 and null, FolderId F1, MeetingCount 0); endpoint answers MSG177 with addedCount 2.<br>Exception: N/A<br>Log Message: Two StoredFile rows, two objects written, two Document rows with OwnerId U1 and FolderId F1, in one transaction; library locked for the owner. | Normal | UC-36 · BR-229 · MSG177 |
| UTC002 | Folder F9 was deleted while the files were uploading. | folderId: F9; files: one PDF. | Return: Failure DestinationNotFound (HTTP 404, MSG179).<br>Exception: N/A<br>Log Message: Objects released; StoredFile rows stamped purged. | Abnormal | UC-36 · MSG179 |
| UTC003 | Storage used 10 MB, limit 11 MB. | files: one file of 1 MB + 1 byte. | Return: Failure StorageFull (HTTP 409, MSG346).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-36 · MSG346 |
| UTC004 | Ten files whose lengths add to more than 100 MB. | candidates: 10 files of 11 MB each (115,343,360 bytes; each also exceeds the 10 MB per-file limit, but the submission-total gate fires first). | Return: Failure TooMany (HTTP 400, MSG288).<br>Exception: N/A<br>Log Message: Objects released. | Abnormal | UC-36 · BR-286 · MSG178 |
