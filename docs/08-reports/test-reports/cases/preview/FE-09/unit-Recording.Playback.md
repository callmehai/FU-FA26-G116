# Recording.Playback — Unit (FE-09, REC)

Description: Recording playback, download and lookup: who may listen.

Precondition: Recording rows seeded.

## RecordingService.PlaybackAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RecordingService.cs:113`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Recording in status Available in a Published meeting.<br>2. The actor is a current Member; the playback link lasts 120 minutes. | recordingId: that recording; actorId: the Member. | Return: Success; a signed playback URL for the recording with ExpiresAt = now + 120 minutes (not a download link).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-43b · BR-350 |
| UTC002 | The actor's account is deactivated. | actorId: the deactivated user. | Return: Inactive (403).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b · BR-350 |
| UTC003 | The recording was erased: status Deleted. | actorId: the Secretary. | Return: NotFound; nothing is signed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b · BR-350 · BR-348 |
| UTC004 | A room-mic take still streaming: status Pending. | actorId: the Secretary. | Return: NotFound; nobody hears a take before the mic is switched off.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-44a · UC-43b · BR-350 |
| UTC005 | A recording whose transcription failed: status Failed. | actorId: a Member. | Return: NotFound; only Available recordings are played.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-43b · BR-350 |
| UTC006 | The database fails while reading the recording. | actorId: a Member. | Return: ReadFailed (500, MSG297).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to sign playback'. | Abnormal | UC-43b · BR-350 · MSG297 |
