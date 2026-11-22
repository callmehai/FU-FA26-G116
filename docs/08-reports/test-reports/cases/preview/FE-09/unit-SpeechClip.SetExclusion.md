# SpeechClip.SetExclusion — Unit (FE-09, REC)

Description: Editing, deleting and excluding a speech clip.

Precondition: Existing clips.

## SpeechClipService.SetExclusionAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:255`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | An existing sentence in the record; the actor is the meeting's Chair (active user). | clipId; actorId: the Chair; excluded true; reason 'Leak'. | Return: Success (200, MSG351) with true; ExcludedAt, ExcludedBy and reason Leak are set.<br>Exception: N/A<br>Log Message: Audit SpeechClipExcluded (sentence, meeting, speaker, reason); a ClipEdited change is published. | Normal | UC-46c · BR-357 · MSG351 |
| UTC002 | An excluded sentence; the actor is the meeting's Secretary. | clipId; actorId: the Secretary; excluded false (reason ignored). | Return: Success (200, MSG352) with false; ExcludedAt, ExcludedBy and reason are cleared.<br>Exception: N/A<br>Log Message: Audit SpeechClipRestored; a ClipEdited change is published. | Normal | UC-46c · BR-357 · MSG352 |
| UTC003 | An existing sentence; the actor is an active Member (neither Chair nor Secretary). | clipId; actorId: the Member; excluded true; reason 'Other'. | Return: Forbidden (403, MSG353).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46c · BR-357 · MSG353 |
| UTC004 | An existing sentence; the actor is the meeting's Chair. | clipId; actorId: the Chair; excluded true; reason 'Noise'. | Return: Invalid (400, MSG354) with an error on reason: “Lý do phải là Leak hoặc Other.”.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46c · BR-357 · MSG354 |
| UTC005 | No sentence with this id. | clipId: a random id; actorId: the Chair; excluded true; reason 'Leak'. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46c · BR-357 |
| UTC006 | 1. An existing sentence; the actor is the meeting's Chair.<br>2. The database fails while saving. | clipId; actorId: the Chair; excluded true; reason 'Other'. | Return: SaveFailed (503, MSG304).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to change the exclusion of speech clip'. | Abnormal | UC-46c · BR-357 · MSG304 |
