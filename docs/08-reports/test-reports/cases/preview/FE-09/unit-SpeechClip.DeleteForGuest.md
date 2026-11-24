# SpeechClip.DeleteForGuest — Unit (FE-09, REC)

Description: Speech clips for a guest session: list, capture, delete, playback.

Precondition: Guest session in a meeting in progress.

## SpeechClipService.DeleteForGuestAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:361`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A current guest session; the sentence is the guest's own and still in the record. | guest session; clipId: the guest's own sentence. | Return: Success (200, MSG322); the row is removed and its audio file is deleted after the commit.<br>Exception: N/A<br>Log Message: Audit SpeechClipDeleted (sentence, meeting, invitation, session, byOwner true); a ClipDeleted change is published. | Normal | UC-80 · BR-355 · MSG322 |
| UTC002 | A current guest session; the sentence is another speaker's. | guest session; clipId: someone else's sentence. | Return: NotFound; someone else's sentence does not exist for a guest.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-80 · BR-355 |
| UTC003 | A current guest session; the database fails while saving the removal. | guest session; clipId: the guest's own sentence. | Return: SaveFailed (503, MSG304); the row and its audio file are kept.<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to delete speech clip for a guest'. | Abnormal | UC-80 · BR-355 · MSG304 |
