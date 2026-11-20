# SpeechClip.Edit — Unit (FE-09, REC)

Description: Editing, deleting and excluding a speech clip.

Precondition: Existing clips.

## SpeechClipService.EditAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:195`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | An existing sentence in a meeting whose Secretary is the actor (active user). | clipId; actorId: the Secretary; text: corrected wording. | Return: Success (200, MSG302); the text, EditedBy and EditedAt are updated.<br>Exception: N/A<br>Log Message: Audit SpeechClipEdited (sentence, meeting, speaker). | Normal | UC-46a · BR-359 · MSG302 |
| UTC002 | An existing sentence; the actor is its meeting's Secretary. | clipId; actorId: the Secretary; text: the current text unchanged. | Return: Success (200, MSG302); nothing is changed and no audit row is written.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-46a · BR-359 · MSG302 |
| UTC003 | An existing sentence; the actor is an active Member (not the Secretary). | clipId; actorId: the Member; text: corrected wording. | Return: Forbidden (403, MSG295).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46a · BR-359 · MSG295 |
| UTC004 | An existing sentence; the actor is its meeting's Secretary. | clipId; actorId: the Secretary; text of exactly 1 character. | Return: Success (200, MSG302).<br>Exception: N/A<br>Log Message: Audit SpeechClipEdited. | Boundary | UC-46a · BR-359 · MSG302 |
| UTC005 | An existing sentence; the actor is its meeting's Secretary. | clipId; actorId: the Secretary; text of exactly 8,000 characters. | Return: Success (200, MSG302).<br>Exception: N/A<br>Log Message: Audit SpeechClipEdited. | Boundary | UC-46a · BR-359 · MSG302 |
| UTC006 | An existing sentence; the actor is its meeting's Secretary. | clipId; actorId: the Secretary; text of 8,001 characters. | Return: Invalid (400, MSG303) with an error on text: “Nội dung từ 1 đến 8000 ký tự.”.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-46a · BR-359 · MSG303 |
| UTC007 | An existing sentence; the actor is its meeting's Secretary. | clipId; actorId: the Secretary; text of blanks only. | Return: Invalid (400, MSG303); blanks are trimmed to empty.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-46a · BR-359 · MSG303 |
| UTC008 | No sentence with this id. | clipId: a random id; actorId: a Secretary. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46a · BR-359 |
| UTC009 | The actor is not on the roster of the sentence's meeting. | clipId; actorId: an outsider. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46a · BR-359 |
| UTC010 | The actor's account is deactivated. | clipId; actorId: the deactivated user. | Return: Inactive (403).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46a · BR-359 |
| UTC011 | 1. An existing sentence; the actor is its meeting's Secretary.<br>2. The database fails while saving. | clipId; actorId: the Secretary; text: corrected wording. | Return: SaveFailed (503, MSG304).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to edit speech clip'. | Abnormal | UC-46a · BR-359 · MSG304 |
