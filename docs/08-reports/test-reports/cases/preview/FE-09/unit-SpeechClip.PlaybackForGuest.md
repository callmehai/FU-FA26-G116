# SpeechClip.PlaybackForGuest — Unit (FE-09, REC)

Description: Speech clips for a guest session: list, capture, delete, playback.

Precondition: Guest session in a meeting in progress.

## SpeechClipService.PlaybackForGuestAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:380`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A current guest session; the sentence is the guest's own, with audio, still in the record. | guest session; clipId: the guest's own sentence with audio. | Return: Success with a signed playback link for the sentence.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-43b |
| UTC002 | A current guest session; the sentence is another speaker's. | guest session; clipId: someone else's sentence. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b |
| UTC003 | A current guest session; the guest's own sentence was taken out of the record. | guest session; clipId: the guest's own excluded sentence. | Return: NotFound; only the Chair and the Secretary hear an excluded sentence.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46c · BR-357 |
| UTC004 | A current guest session; the guest's own sentence was stored without audio (disk was full). | guest session; clipId: the guest's own sentence without audio. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b |
| UTC005 | A current guest session; the database fails while reading. | guest session; clipId. | Return: ReadFailed (500, MSG297).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to sign playback for speech clip for a guest'. | Abnormal | UC-43b · MSG297 |
