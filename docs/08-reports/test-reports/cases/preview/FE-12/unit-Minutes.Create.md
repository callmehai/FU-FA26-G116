# Minutes.Create — Unit (FE-12, MIN)

Description: Creating the minutes draft from the template: status, existing minutes and sources.

Precondition: Meeting in progress or ended; secretary actor.

## MinutesService.CreateAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesService.cs:50`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Ended meeting with Chair and Secretary assigned.<br>2. No minutes exist for the meeting.<br>3. A reviewed draft exists. | meetingId: the meeting; actor: the current Secretary; rebuild: false. | Return: New Draft revision 1 with content built from the records and the reviewed draft; BuiltFromDraftAt = the draft change time; 201 MSG395.<br>Exception: N/A<br>Log Message: Saved: minutes row (Draft, revision 1) with template content, BuiltFromDraftAt and creation time; realtime minutes-changed push to the meeting's readers. | Normal | UC-59 · MSG395 · Deviation: success also emits a realtime minutes-changed push (working-tree change). |
| UTC002 | 1. Ended meeting the actor can read.<br>2. Actor is an ordinary Member (neither Chair nor Secretary). | meetingId: the meeting; actor: the Member; rebuild: false. | Return: Failure Forbidden; 403 MSG397; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · MSG397 |
| UTC003 | 1. Meeting still InProgress (not ended).<br>2. Actor is the current Secretary. | meetingId: the meeting; actor: the current Secretary; rebuild: false. | Return: Failure WrongMeetingStatus; 409 MSG398; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · MSG398 |
| UTC004 | 1. Ended meeting; actor is the current Secretary.<br>2. A Draft revision already exists. | meetingId: the meeting; actor: the current Secretary; rebuild: false. | Return: Failure Exists; 409 MSG399; the existing draft is unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · MSG399 |
| UTC005 | 1. Ended meeting; actor is the current Secretary.<br>2. The latest revision is WaitingApproval. | meetingId: the meeting; actor: the current Secretary; rebuild: true. | Return: Failure Locked; 409 MSG400; a sent revision is refused even with rebuild.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · MSG400 |
| UTC006 | 1. Ended meeting; actor is the current Secretary.<br>2. The database fails while saving the new draft. | meetingId: the meeting; actor: the current Secretary; rebuild: false. | Return: Failure SaveFailed; 503 MSG404; nothing saved.<br>Exception: N/A<br>Log Message: Error logged with the meeting id. | Abnormal | UC-59 · MSG404 |
