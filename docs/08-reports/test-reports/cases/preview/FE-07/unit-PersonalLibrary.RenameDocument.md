# PersonalLibrary.RenameDocument — Unit (FE-07, DOC)

Description: Personal library documents: upload with quota, rename, move, delete.

Precondition: Owner with folders; file storage recorder.

## PersonalLibraryService.RenameDocumentAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/PersonalLibraryService.cs:205`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Owner U1 has document D1 "bao-cao.pdf" (application/pdf).<br>2. Clock 2026-11-18T03:00Z. | documentId: D1; fileName: "Bao cao Q3.pdf". | Return: Value: library row with FileName "Bao cao Q3.pdf"; endpoint answers MSG183.<br>Exception: N/A<br>Log Message: Document FileName and UpdatedAt changed and saved; meeting copies keep their own name. | Normal | UC-40b · MSG183 |
| UTC002 | Same. | fileName: "  Bao-cao.PDF  ". | Return: Value with FileName "Bao-cao.PDF"; trimmed, and the extension is compared case-insensitively.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-40b · MSG183 |
| UTC003 | D9 does not exist or is another user's. | documentId: D9; fileName: "a.pdf". | Return: Failure NotFound (HTTP 404, MSG175).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-40b · BR-083 · MSG175 |
| UTC004 | D1 is a PDF. | fileName: "bao-cao.html". | Return: Failure NameInvalid (HTTP 400, MSG347); a PDF cannot be renamed to another extension.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-40b · MSG347 |
| UTC005 | Owner U1 has document D1 "bao-cao.pdf" (application/pdf). | documentId: D1; fileName: 251 "n" characters + ".pdf" (255 characters total). | Return: Value: library row with the 255-character FileName; endpoint answers MSG183.<br>Exception: N/A<br>Log Message: Document FileName and UpdatedAt changed and saved. | Boundary | UC-40b · MSG183 |
