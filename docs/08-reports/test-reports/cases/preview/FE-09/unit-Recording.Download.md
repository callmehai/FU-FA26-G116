# Recording.Download — Unit (FE-09, REC)

Description: Recording playback, download and lookup: who may listen.

Precondition: Recording rows seeded.

## RecordingService.DownloadAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RecordingService.cs:131`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Available recording in a Normal meeting.<br>2. The actor is the current Chair. | recordingId; actorId: the Chair. | Return: Success; a signed download URL (signed apart from playback links).<br>Exception: N/A<br>Log Message: Audit row RecordingDownloaded for the recording with meetingId, file name and byte size, actor = the Chair. | Normal | UC-43c · BR-351 |
| UTC002 | 1. Available recording in an Internal meeting.<br>2. The actor is the current Secretary. | recordingId; actorId: the Secretary. | Return: Success; a signed download URL.<br>Exception: N/A<br>Log Message: Audit row RecordingDownloaded written. | Normal | UC-43c · BR-351 |
| UTC003 | Available recording; the actor is a Member. | actorId: a Member. | Return: ExportForbidden (403, MSG355).<br>Exception: N/A<br>Log Message: No audit row. | Abnormal | UC-43c · BR-351 · MSG355 |
| UTC004 | Available recording in a Secret meeting; the actor is the Chair. | actorId: the Chair. | Return: SecretMeeting (403, MSG356).<br>Exception: N/A<br>Log Message: No audit row. | Abnormal | UC-43c · BR-351 · MSG356 |
| UTC005 | Available recording in a Secret meeting; the actor is a Member. | actorId: a Member. | Return: ExportForbidden (403, MSG355).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-43c · BR-351 · MSG355 |
| UTC006 | No recording with this id exists. | recordingId: a random id. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43c |
| UTC007 | The actor's account is deactivated. | actorId: the deactivated user. | Return: Inactive (403).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43c |
| UTC008 | The actor is not on the roster. | actorId: the meeting creator, not a participant. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43c · BR-350 |
| UTC009 | Recording is Deleted. | actorId: the Secretary. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43c · BR-348 |
| UTC010 | Recording is Pending (take streaming). | actorId: the Secretary. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-43c |
| UTC011 | The combined recording of a hybrid meeting, Available; the actor is the Secretary. | recordingId: the combined recording. | Return: Success; a signed download URL; the combined recording can be downloaded.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-43c · BR-458 |
| UTC012 | The database fails while reading. | actorId: the Chair. | Return: ReadFailed (500, MSG297).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to sign a download'. | Abnormal | UC-43c · MSG297 |
