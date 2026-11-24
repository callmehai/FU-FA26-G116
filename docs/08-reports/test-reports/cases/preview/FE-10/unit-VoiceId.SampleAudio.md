# VoiceId.SampleAudio — Unit (FE-10, TRANS)

Description: Voice samples: add under consent, delete, read audio.

Precondition: User with consent; speech worker replaced.

## VoiceIdService.SampleAudioAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceIdService.cs:202`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | User owns a sample whose recording was kept. | actorId: the sample owner; sampleId: a sample with kept audio. | Return: The decrypted bytes with their content type.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-81d · BR-364 |
| UTC002 | The sample belongs to another person. | actorId: a user; sampleId: another person sample. | Return: Null; someone else sample is answered exactly like one that does not exist.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-81d · BR-364 |
| UTC003 | User owns a sample recorded under notice "1.0", which kept no recording. | actorId: the sample owner; sampleId: a sample without kept audio. | Return: Null; only samples under a consent that says so keep their recording.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-81d · BR-362 · BR-364 |
