# VoiceCollection.Delete — Unit (FE-10, TRANS)

Description: Collecting voice samples for a person: who may collect, add, hear and delete (ADR-0071, ADR-0075).

Precondition: Active person with Voice ID consent; collector holds the Thu giọng mẫu hộ capability.

## VoiceCollectionService.DeletePersonSampleAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceCollectionService.cs:176`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A proxy sample exists for the active person. | actorId: the collector; userId: the person; sampleId: a proxy sample of the person. | Return: Success with the sample id (MSG474 "Đã xoá giọng mẫu thu hộ.").<br>Exception: N/A<br>Log Message: Saved: sample removed; audit row records the deletion with the proxy scope. | Normal | UC-86d · BR-452 · MSG474 |
| UTC002 | The sample is one the person recorded themself. | actorId: the collector; userId: the person; sampleId: the person own self-recorded sample. | Return: Failure NotFound; a collector never touches the person own set.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86d · BR-452 · BR-448 |
| UTC003 | The person account is deactivated; a proxy sample still exists. | actorId: the collector; userId: a deactivated person; sampleId: a proxy sample of that person. | Return: Failure NotFound; answered as if the sample does not exist.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86d · ADR-0049 |
