# Recording.Playback — Unit (FE-09, REC)

Description: Recording playback, download and lookup: who may listen.

Precondition: Recording rows seeded.

## RecordingService.PlaybackAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RecordingService.cs:115`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Recording in status Available in a Published meeting.<br>2. The actor is a current Member; the playback link lasts 120 minutes. | recordingId: that recording; actorId: the Member. | Return: Success; a signed playback URL for the recording with ExpiresAt = now + 120 minutes (not a download link).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-51b · BR-111, BR-112 |
| UTC002 | The recording was erased: status Deleted. | actorId: the Secretary. | Return: NotFound; nothing is signed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-51b · BR-111, BR-112 · BR-110 |
| UTC003 | A room-mic take still streaming: status Pending. | actorId: the Secretary. | Return: NotFound; nobody hears a take before the mic is switched off.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-52a · UC-51b · BR-111, BR-112 |
| UTC004 | The database fails while reading the recording. | actorId: a Member. | Return: ReadFailed (500, MSG297).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to sign playback'. | Abnormal | UC-51b · BR-111, BR-112 · MSG297 |
