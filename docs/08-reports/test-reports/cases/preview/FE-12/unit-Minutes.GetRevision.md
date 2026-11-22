# Minutes.GetRevision — Unit (FE-12, MIN)

Description: Minutes export and revision reading.

Precondition: Minutes with revisions.

## MinutesService.GetRevisionAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesService.cs:184`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Revision 1 Superseded and revision 2 Issued.<br>2. Actor is the current Secretary. | minutesId: revision 1; actor: the current Secretary. | Return: State with revision 1 selected (content, submitter, returner and reason visible); history lists both revisions, newest first.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-58 · BR-386 · BR-412 |
| UTC002 | 1. Revision 1 Superseded and revision 2 Issued.<br>2. Actor is a Member who reads the meeting. | minutesId: revision 2; actor: the Member. | Return: State with revision 2 selected; submitter, returner and reason are null for a reader; history lists only Issued and Superseded revisions.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-58 · BR-386 · BR-412 |
| UTC003 | 1. Draft correction (revision 2) exists besides Issued revision 1.<br>2. Actor is a Member who reads the meeting. | minutesId: revision 2 (Draft); actor: the Member. | Return: Failure NotFound; 404 with no message code; the unissued correction stays hidden.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-58 · BR-412 |
| UTC004 | 1. No revision has the given id.<br>2. Actor is the current Secretary. | minutesId: unknown; actor: the current Secretary. | Return: Failure NotFound; 404 with no message code.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-58 |
| UTC005 | 1. Issued revision.<br>2. Actor is the current Secretary.<br>3. The database fails while reading. | minutesId: the Issued revision; actor: the current Secretary. | Return: Failure ReadFailed; 500 MSG403.<br>Exception: N/A<br>Log Message: Error logged with the minutes id. | Abnormal | UC-58 · MSG403 |
