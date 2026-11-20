# VoiceCollection.Audio — Unit (FE-10, TRANS)

Description: Collecting voice samples from a meeting: who may collect, add, hear and delete.

Precondition: Meeting with participants and consent.

## VoiceCollectionService.AudioAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceCollectionService.cs:168`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A sample was collected at the meeting; actor is its current secretary. | meetingId: the meeting; actorId: its current secretary; sampleId: a sample collected there. | Return: The decrypted bytes with their content type.<br>Exception: N/A<br>Log Message: Saved: audit row records the play with the owner and the meeting. | Normal | UC-86c · BR-452 · BR-453 |
| UTC002 | The sample was collected at another meeting; actor is the current secretary of this one. | meetingId: this meeting; actorId: its current secretary; sampleId: a sample collected elsewhere. | Return: Null; a secretary only hears samples taken in their own meeting.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86c · BR-452 |
| UTC003 | A sample was collected at the meeting; actor takes part but is not the secretary. | meetingId: the meeting; actorId: a member who is not the secretary; sampleId: a sample collected there. | Return: Null; answered as if the meeting does not exist.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86c · BR-450 |
