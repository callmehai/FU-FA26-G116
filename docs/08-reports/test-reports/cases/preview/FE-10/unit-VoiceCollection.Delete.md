# VoiceCollection.Delete — Unit (FE-10, TRANS)

Description: Collecting voice samples from a meeting: who may collect, add, hear and delete.

Precondition: Meeting with participants and consent.

## VoiceCollectionService.DeleteAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceCollectionService.cs:190`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A sample was collected at the meeting; actor is its current secretary. | meetingId: the meeting; actorId: its current secretary; sampleId: a sample collected there. | Return: Success with the sample id (MSG474 "Đã xoá giọng mẫu thu hộ.").<br>Exception: N/A<br>Log Message: Saved: sample removed; audit row records the deletion with the meeting scope. | Normal | UC-86d · BR-452 · MSG474 |
| UTC002 | The sample is one the person recorded themself; actor is a secretary. | meetingId: a meeting; actorId: its current secretary; sampleId: the person own self-recorded sample. | Return: Failure NotFound; a secretary never touches the person own set.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86d · BR-452 · BR-448 |
| UTC003 | A sample was collected at the meeting; actor takes part but is not the secretary. | meetingId: the meeting; actorId: a member who is not the secretary; sampleId: a sample collected there. | Return: Failure NotFound; answered as if the meeting does not exist.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86d · BR-450 |
