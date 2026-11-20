# Recording.Playback — Unit (FE-09, REC)

Description: Recording playback, download and lookup: who may listen.

Precondition: Recording rows seeded.

## RecordingService.PlaybackAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RecordingService.cs:113`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Recording in status Available in a Published meeting.<br>2. The actor is a current Member; the playback link lasts 120 minutes. | recordingId: that recording; actorId: the Member. | Return: Success; a signed playback URL for the recording with ExpiresAt = now + 120 minutes (not a download link).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-43b · BR-350 |
| UTC002 | The same recording in a Secret meeting. | actorId: a Member of the meeting. | Return: Success; a signed playback URL. Listening is not restricted by sensitivity, only downloading is.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-43b · BR-350 · BR-351 |
| UTC003 | No recording with this id exists. | recordingId: a random id. | Return: NotFound (404 without body).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b · BR-350 |
| UTC004 | The actor's account is deactivated. | actorId: the deactivated user. | Return: Inactive (403).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b · BR-350 |
| UTC005 | The actor created the meeting but is not on the roster. | actorId: the creator. | Return: NotFound; the recording is hidden.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b · BR-350 |
| UTC006 | The actor is an approver, not on the roster. | actorId: the approver. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b · BR-350 |
| UTC007 | The actor's Member assignment was removed. | actorId: the former Member. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b · BR-350 |
| UTC008 | The recording belongs to a meeting that has been deleted or cannot be loaded. | actorId: any user. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-43b · BR-350 |
| UTC009 | The recording was erased: status Deleted. | actorId: the Secretary. | Return: NotFound; nothing is signed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43b · BR-350 · BR-348 |
| UTC010 | A room-mic take still streaming: status Pending. | actorId: the Secretary. | Return: NotFound; nobody hears a take before the mic is switched off.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-44a · UC-43b · BR-350 |
| UTC011 | A recording whose transcription failed: status Failed. | actorId: a Member. | Return: NotFound; only Available recordings are played.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-43b · BR-350 |
| UTC012 | The database fails while reading the recording. | actorId: a Member. | Return: ReadFailed (500, MSG297).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to sign playback'. | Abnormal | UC-43b · BR-350 · MSG297 |
