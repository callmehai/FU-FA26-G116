# SpeechClip.Playback — Unit (FE-09, REC)

Description: Speech-clip retention erasure and playback.

Precondition: Clips with ages.

## SpeechClipService.PlaybackAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:439`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | An existing sentence with audio in a meeting the actor (active Member) may read. | clipId; actorId: the Member. | Return: Success with a signed playback link for the sentence.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-43b · BR-350 |
| UTC002 | An excluded sentence with audio; the actor is an active Member (not Chair or Secretary). | clipId: the excluded sentence; actorId: the Member. | Return: NotFound; to everyone else an excluded sentence does not exist.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46c · BR-357 |
| UTC003 | The actor's account is deactivated. | clipId; actorId: the deactivated user. | Return: Inactive (403).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b · BR-350 |
| UTC004 | An existing sentence stored without audio (disk was full). | clipId: the sentence without audio; actorId: a Member. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b |
| UTC005 | An existing sentence with audio; the database fails while reading. | clipId; actorId: a Member. | Return: ReadFailed (500, MSG297).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to sign playback for speech clip'. | Abnormal | UC-43b · BR-350 · MSG297 |
