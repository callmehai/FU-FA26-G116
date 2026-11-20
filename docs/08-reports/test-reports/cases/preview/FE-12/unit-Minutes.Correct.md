# Minutes.Correct — Unit (FE-12, MIN)

Description: Minutes approval workflow: submit, approve (issue), return, correct.

Precondition: Minutes in the needed status; approver and secretary actors.

## MinutesService.CorrectAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesService.cs:274`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Ended meeting; actor is the current Secretary.<br>2. Revision 1 Issued; no later revision exists. | minutesId: the Issued revision; actor: the current Secretary. | Return: New Draft revision 2 with the issued content and SupersedesId = revision 1; 201 MSG432; nobody is notified until the correction is issued.<br>Exception: N/A<br>Log Message: Saved: Draft revision (number + 1) with the issued content and the replaced-revision id; no notification. | Normal | UC-83 · BR-410 · MSG432 |
| UTC002 | 1. Revision 1 Issued.<br>2. Actor is a Member who reads the meeting. | minutesId: the Issued revision; actor: the Member. | Return: Failure Forbidden; 403 MSG397; no revision created.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-83 · MSG397 |
| UTC003 | 1. Draft revision exists.<br>2. Actor reads no meeting. | minutesId: the Draft revision; actor: the stranger. | Return: Failure NotFound; 404 with no message code; no revision created.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-83 |
| UTC004 | 1. Ended meeting; actor is the current Secretary.<br>2. Revision still Draft (not the issued one). | minutesId: the Draft revision; actor: the current Secretary. | Return: Failure NotIssued; 409 MSG433; no revision created.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-83 · MSG433 |
| UTC005 | 1. Ended meeting; actor is the current Secretary.<br>2. Revision 1 Issued with an unissued Draft correction (revision 2) already started. | minutesId: revision 1; actor: the current Secretary. | Return: Failure CorrectionInProgress; 409 MSG434; no revision created.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-83 · BR-410 · MSG434 |
| UTC006 | 1. Ended meeting; actor is the current Secretary.<br>2. Revision 1 Issued with no later revision.<br>3. A simultaneous request corrects the same revision at the same time. | minutesId: revision 1; actor: the current Secretary; the database's revision-number index rejects the save. | Return: Failure CorrectionInProgress; 409 MSG434; the other request's correction stands.<br>Exception: N/A<br>Log Message: Warning logged: concurrent correction of the revision; the existing correction stands. | Abnormal | UC-83 · BR-410 · MSG434 |
| UTC007 | 1. Ended meeting; actor is the current Secretary; revision 1 Issued.<br>2. The database fails while saving the correction. | minutesId: revision 1; actor: the current Secretary. | Return: Failure SaveFailed; 503 MSG404; no revision created.<br>Exception: N/A<br>Log Message: Error logged with the action and minutes id. | Abnormal | UC-83 · MSG404 |
