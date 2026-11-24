# VoiceCollection.Audio — Unit (FE-10, TRANS)

Description: Collecting voice samples for a person: who may collect, add, hear and delete (ADR-0071, ADR-0075).

Precondition: Active person with Voice ID consent; collector holds the Thu giọng mẫu hộ capability.

## VoiceCollectionService.PersonAudioAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceCollectionService.cs:154`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A proxy sample with its recording exists for the active person. | actorId: the collector; userId: the person; sampleId: a proxy sample of the person. | Return: The decrypted bytes with their content type.<br>Exception: N/A<br>Log Message: Saved: audit row records the play with the owner, before the bytes are returned. | Normal | UC-86c · BR-452 · BR-453 |
| UTC002 | The sample is one the person recorded themself. | actorId: the collector; userId: the person; sampleId: the person own self-recorded sample. | Return: Null; a collector only hears samples taken for the person, never the person own set.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86c · BR-452 |
