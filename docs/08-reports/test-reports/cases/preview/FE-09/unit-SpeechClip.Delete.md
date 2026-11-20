# SpeechClip.Delete — Unit (FE-09, REC)

Description: Editing, deleting and excluding a speech clip.

Precondition: Existing clips.

## SpeechClipService.DeleteAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:229`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | An existing sentence with audio; the actor is its speaker (active user). | clipId; actorId: the speaker. | Return: Success (200, MSG322); the row is removed and its audio file is deleted after the commit.<br>Exception: N/A<br>Log Message: Audit SpeechClipDeleted (sentence, meeting, speaker, byOwner true); a ClipDeleted change is published. | Normal | UC-80 · BR-355 · MSG322 |
| UTC002 | An existing sentence of another speaker; the actor is the meeting's Secretary. | clipId; actorId: the Secretary. | Return: Success (200, MSG322); the row and its audio are removed.<br>Exception: N/A<br>Log Message: Audit SpeechClipDeleted (byOwner false); a ClipDeleted change is published. | Normal | UC-46d · BR-355 · MSG322 |
| UTC003 | An existing sentence; the actor is an active Member who is neither its speaker nor the Secretary. | clipId; actorId: the other Member. | Return: Forbidden (403, MSG323).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46d · BR-355 · MSG323 |
| UTC004 | No sentence with this id. | clipId: a random id; actorId: a Member. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46d · BR-355 |
| UTC005 | The actor's account is deactivated. | clipId; actorId: the deactivated user. | Return: Inactive (403).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46d · BR-355 |
| UTC006 | 1. An existing sentence with audio; the actor is its speaker.<br>2. The database fails while saving the removal. | clipId; actorId: the speaker. | Return: SaveFailed (503, MSG304); the row and its audio file are kept.<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to delete speech clip'. | Abnormal | UC-80 · BR-355 · MSG304 |
