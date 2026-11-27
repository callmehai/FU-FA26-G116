# SpeechClip.Playback — Unit (FE-09, REC)

Description: Speech-clip retention erasure and playback.

Precondition: Clips with ages.

## SpeechClipService.PlaybackAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:439`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | An existing sentence with audio in a meeting the actor (active Member) may read. | clipId; actorId: the Member. | Return: Success with a signed playback link for the sentence.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-51b · BR-111, BR-112 |
| UTC002 | An excluded sentence with audio; the actor is an active Member (not Chair or Secretary). | clipId: the excluded sentence; actorId: the Member. | Return: NotFound; to everyone else an excluded sentence does not exist.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-55c · BR-114, BR-115 |
| UTC003 | An existing sentence with audio; the database fails while reading. | clipId; actorId: a Member. | Return: ReadFailed (500, MSG297).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to sign playback for speech clip'. | Abnormal | UC-51b · BR-111, BR-112 · MSG297 |
