# PersonalLibrary.RenameDocument — Unit (FE-07, DOC)

Description: Personal library documents: upload with quota, rename, move, delete.

Precondition: Owner with folders; file storage recorder.

## PersonalLibraryService.RenameDocumentAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/PersonalLibraryService.cs:205`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Owner U1 has document D1 "bao-cao.pdf" (application/pdf).<br>2. Clock 2026-11-18T03:00Z. | documentId: D1; fileName: "Bao cao Q3.pdf". | Return: Value: library row with FileName "Bao cao Q3.pdf"; endpoint answers MSG183.<br>Exception: N/A<br>Log Message: Document FileName and UpdatedAt changed and saved; meeting copies keep their own name. | Normal | UC-71b · BR-276 · MSG183 |
| UTC002 | Same. | fileName: "  Bao-cao.PDF  ". | Return: Value with FileName "Bao-cao.PDF"; trimmed, and the extension is compared case-insensitively.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-71b · MSG183 |
| UTC003 | Same. | fileName: "C:\tmp\moi.pdf". | Return: Value with FileName "moi.pdf" (last path segment).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-71b · MSG183 |
| UTC004 | D9 does not exist or is another user's. | documentId: D9; fileName: "a.pdf". | Return: Failure NotFound (HTTP 404, MSG175).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-71b · BR-242 · MSG175 |
| UTC005 | Same. | fileName: null. | Return: Failure NameInvalid (HTTP 400, MSG347); the document is unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-71b · MSG347 |
| UTC006 | Same. | fileName: ".pdf" (nothing before the extension). | Return: Failure NameInvalid (HTTP 400, MSG347).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-71b · MSG347 |
| UTC007 | Same. | fileName: "   .pdf" (only spaces before the extension). | Return: Failure NameInvalid (HTTP 400, MSG347).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-71b · MSG347 |
| UTC008 | D1 is a PDF. | fileName: "bao-cao.html". | Return: Failure NameInvalid (HTTP 400, MSG347); a PDF cannot be renamed to another extension.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-71b · MSG347 |
| UTC009 | D1 is a PDF. | fileName: "bao-cao.docx". | Return: Failure NameInvalid (MSG347).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-71b · MSG347 |
| UTC010 | D2 is a DOCX. | documentId: D2; fileName: "nghi-quyet.docx". | Return: Value with the new name.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-71b · MSG183 |
| UTC011 | D1 is a PDF. | fileName: 251 characters + ".pdf" (255). | Return: Value; 255 characters is accepted.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-71b · MSG183 |
| UTC012 | D1 is a PDF. | fileName: 252 characters + ".pdf" (256). | Return: Failure NameInvalid (MSG347).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-71b · MSG347 |
| UTC013 | D1 is a PDF. | fileName: "bao<RLO>cao.pdf" containing U+202E. | Return: Failure NameInvalid (MSG347).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-71b · MSG347 |
| UTC014 | Database error on save. | fileName: "moi.pdf". | Return: Failure SaveFailed (HTTP 503, MSG184); the name stays.<br>Exception: N/A<br>Log Message: Error log "Failed to rename a document". | Abnormal | UC-71b · MSG184 |
