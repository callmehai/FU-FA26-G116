# Minutes.Export — Unit (FE-12, MIN)

Description: Minutes export and revision reading.

Precondition: Minutes with revisions.

## MinutesService.ExportAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesService.cs:154`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Issued revision of meeting "Họp quý 3".<br>2. Actor is the current Secretary. | minutesId: the Issued revision; actor: the current Secretary; format: Docx. | Return: File "Biên bản - Họp quý 3.docx" (Word content type) built from the shown HTML; no message code.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-58 · BR-412 |
| UTC002 | 1. No revision has the given id.<br>2. Actor is the current Secretary. | minutesId: unknown; actor: the current Secretary; format: Docx. | Return: Failure NotFound; 404 with no message code; no file.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-58 |
| UTC003 | 1. Draft revision exists.<br>2. Actor is a Member who reads the meeting. | minutesId: the Draft revision; actor: the Member; format: Docx. | Return: Failure NotFound; 404 with no message code; a revision waiting to be approved is not there for anyone else.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-58 · BR-412 |
| UTC004 | 1. Issued revision of a Secret meeting.<br>2. Actor is the current Chair. | minutesId: the Issued revision; actor: the current Chair; format: Docx. | Return: Failure SecretMeeting; 403 MSG405; no file. Secret minutes stay on screen only.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-58 · BR-188 · MSG405 |
| UTC005 | 1. Issued revision of a meeting whose title makes the file name longer than 120 characters.<br>2. Actor is the current Secretary. | minutesId: the Issued revision; actor: the current Secretary; format: Docx. | Return: File name cut to the first 120 characters of "Biên bản - <title>" plus ".docx"; content unchanged.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-58 |
| UTC006 | 1. Issued revision.<br>2. Actor is the current Secretary.<br>3. The database fails while reading. | minutesId: the Issued revision; actor: the current Secretary; format: Docx. | Return: Failure ReadFailed; 500 MSG403; no file.<br>Exception: N/A<br>Log Message: Error logged with the minutes id. | Abnormal | UC-58 · MSG403 |
