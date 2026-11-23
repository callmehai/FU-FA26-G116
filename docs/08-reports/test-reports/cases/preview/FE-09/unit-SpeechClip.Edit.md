# SpeechClip.Edit — Unit (FE-09, REC)

Description: Editing, deleting and excluding a speech clip.

Precondition: Existing clips.

## SpeechClipService.EditAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:199`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | An existing sentence in a meeting whose Secretary is the actor (active user). | clipId; actorId: the Secretary; text: corrected wording. | Return: Success (200, MSG302); the text, EditedBy and EditedAt are updated.<br>Exception: N/A<br>Log Message: Audit SpeechClipEdited (sentence, meeting, speaker). | Normal | UC-46a · BR-359 · MSG302 |
| UTC002 | An existing sentence; the actor is an active Member (not the Secretary). | clipId; actorId: the Member; text: corrected wording. | Return: Forbidden (403, MSG295).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46a · BR-359 · MSG295 |
| UTC003 | An existing sentence; the actor is its meeting's Secretary. | clipId; actorId: the Secretary; text of 8,001 characters. | Return: Invalid (400, MSG303) with an error on text: “Nội dung từ 1 đến 8000 ký tự.”.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-46a · BR-359 · MSG303 |
| UTC004 | 1. An existing sentence; the actor is its meeting's Secretary.<br>2. The database fails while saving. | clipId; actorId: the Secretary; text: corrected wording. | Return: SaveFailed (503, MSG304).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to edit speech clip'. | Abnormal | UC-46a · BR-359 · MSG304 |
