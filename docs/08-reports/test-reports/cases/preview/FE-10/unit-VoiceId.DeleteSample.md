# VoiceId.DeleteSample — Unit (FE-10, TRANS)

Description: Voice samples: add under consent, delete, read audio.

Precondition: User with consent; speech worker replaced.

## VoiceIdService.DeleteSampleAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceIdService.cs:181`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | User keeps an own sample. | actorId: the sample owner; sampleId: an own sample. | Return: Success with the profile without that sample (MSG311 "Đã xoá giọng mẫu.").<br>Exception: N/A<br>Log Message: Saved: sample removed; audit row records the deletion. | Normal | UC-81e · MSG311 |
| UTC002 | 1. A secretary recorded a sample for the user.<br>2. The user reads it on their own voice page. | actorId: the sample owner; sampleId: a secretary-recorded sample. | Return: Success (MSG311); samples a secretary took are the person own too, and theirs to delete.<br>Exception: N/A<br>Log Message: Saved: sample removed; audit row records the deletion with the meeting scope. | Normal | UC-81e · BR-452 · MSG311 |
| UTC003 | The sample belongs to another person. | actorId: a user; sampleId: another person sample. | Return: Failure NotFound; someone else sample is answered exactly like one that does not exist.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-81e |
