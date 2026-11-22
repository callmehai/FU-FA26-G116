# Recording.Download — Unit (FE-09, REC)

Description: Recording playback, download and lookup: who may listen.

Precondition: Recording rows seeded.

## RecordingService.DownloadAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RecordingService.cs:131`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Available recording in a Normal meeting.<br>2. The actor is the current Chair. | recordingId; actorId: the Chair. | Return: Success; a signed download URL (signed apart from playback links).<br>Exception: N/A<br>Log Message: Audit row RecordingDownloaded for the recording with meetingId, file name and byte size, actor = the Chair. | Normal | UC-43c · BR-351 |
| UTC002 | Available recording; the actor is a Member. | actorId: a Member. | Return: ExportForbidden (403, MSG355).<br>Exception: N/A<br>Log Message: No audit row. | Abnormal | UC-43c · BR-351 · MSG355 |
| UTC003 | Available recording in a Secret meeting; the actor is the Chair. | actorId: the Chair. | Return: SecretMeeting (403, MSG356).<br>Exception: N/A<br>Log Message: No audit row. | Abnormal | UC-43c · BR-351 · MSG356 |
| UTC004 | Available recording in a Secret meeting; the actor is a Member. | actorId: a Member. | Return: ExportForbidden (403, MSG355).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-43c · BR-351 · MSG355 |
| UTC005 | The database fails while reading. | actorId: the Chair. | Return: ReadFailed (500, MSG297).<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to sign a download'. | Abnormal | UC-43c · MSG297 |
| UTC006 | 1. Available WAV recording in a Normal meeting; the actor is its current Chair.<br>2. The server cannot convert audio to MP3 (no converter). | recordingId: the WAV recording; actorId: the Chair. | Return: Mp3Unavailable (503, MSG482 "Chưa chuyển được bản ghi sang MP3. Vui lòng thử lại sau."); no link is signed.<br>Exception: N/A<br>Log Message: No audit row. | Abnormal | UC-43c · BR-351 · MSG482 · Deviation: downloads leave as MP3 since ADR-0069 (ea9b880a), after these UTCs were written |
