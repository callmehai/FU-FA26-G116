# Minutes.Create — Unit (FE-12, MIN)

Description: Creating the minutes draft from the template: status, existing minutes and sources.

Precondition: Meeting in progress or ended; secretary actor.

## MinutesService.CreateAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesService.cs:50`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Ended meeting with Chair and Secretary assigned.<br>2. No minutes exist for the meeting.<br>3. A reviewed draft exists. | meetingId: the meeting; actor: the current Secretary; rebuild: false. | Return: New Draft revision 1 with content built from the records and the reviewed draft; BuiltFromDraftAt = the draft change time; 201 MSG395.<br>Exception: N/A<br>Log Message: Saved: minutes row (Draft, revision 1) with template content, BuiltFromDraftAt and creation time. | Normal | UC-59 · MSG395 |
| UTC002 | 1. Ended meeting; actor is the current Secretary.<br>2. A Draft revision exists with the Secretary's own edits. | meetingId: the meeting; actor: the current Secretary; rebuild: true. | Return: Content rebuilt from the records and the current draft; the Secretary's edits are discarded; 201 MSG395.<br>Exception: N/A<br>Log Message: Saved: content, BuiltFromDraftAt and UpdatedAt replaced on the existing revision. | Normal | UC-59 · MSG395 |
| UTC003 | Actor reads no meeting (not organizer, approver or participant). | meetingId: that meeting; actor: the stranger; rebuild: false. | Return: Failure NotFound; 404 with no message code; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 |
| UTC004 | Actor's account is deactivated. | meetingId: an Ended meeting the actor manages; actor: deactivated Secretary; rebuild: false. | Return: Failure Inactive; 403 with no message code; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 |
| UTC005 | 1. Ended meeting the actor can read.<br>2. Actor is an ordinary Member (neither Chair nor Secretary). | meetingId: the meeting; actor: the Member; rebuild: false. | Return: Failure Forbidden; 403 MSG397; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · MSG397 |
| UTC006 | 1. Meeting still in Draft status (not started).<br>2. Actor is the current Secretary. | meetingId: the meeting; actor: the current Secretary; rebuild: false. | Return: Failure WrongMeetingStatus; 409 MSG398; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · MSG398 |
| UTC007 | 1. Ended meeting; actor is the current Secretary.<br>2. A Draft revision already exists. | meetingId: the meeting; actor: the current Secretary; rebuild: false. | Return: Failure Exists; 409 MSG399; the existing draft is unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · MSG399 |
| UTC008 | 1. Ended meeting; actor is the current Secretary.<br>2. The latest revision is WaitingApproval. | meetingId: the meeting; actor: the current Secretary; rebuild: true. | Return: Failure Locked; 409 MSG400; a sent revision is refused even with rebuild.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · MSG400 |
| UTC009 | 1. Ended meeting; actor is the current Secretary.<br>2. The latest revision is a Draft correction of the issued text (it replaces an issued revision). | meetingId: the meeting; actor: the current Secretary; rebuild: true. | Return: Failure CorrectionRebuild; 409 MSG456; the correction keeps its issued content for hand editing.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · UC-83 · BR-413 · MSG456 |
| UTC010 | 1. Ended meeting with no minutes; actor is the current Secretary.<br>2. A simultaneous request builds the first draft at the same time. | meetingId: the meeting; actor: the current Secretary; rebuild: false; the database's one-minutes-per-meeting index rejects the save. | Return: Failure Exists; 409 MSG399; the other request's draft stands.<br>Exception: N/A<br>Log Message: Warning logged: concurrent creation for the meeting; the existing draft stands. | Abnormal | UC-59 · MSG399 |
| UTC011 | 1. Ended meeting; actor is the current Secretary.<br>2. The database fails while saving the new draft. | meetingId: the meeting; actor: the current Secretary; rebuild: false. | Return: Failure SaveFailed; 503 MSG404; nothing saved.<br>Exception: N/A<br>Log Message: Error logged with the meeting id. | Abnormal | UC-59 · MSG404 |
