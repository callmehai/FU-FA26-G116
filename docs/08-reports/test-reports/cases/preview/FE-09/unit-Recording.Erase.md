# Recording.Erase — Unit (FE-09, REC)

Description: Recording erasure on request and when retention expires.

Precondition: Recordings with ages; clock replaced.

## RecordingService.EraseAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RecordingService.cs:160`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Available recording with transcript and file, in a meeting where the actor is the current Secretary. | recordingId; actorId: the Secretary. | Return: Success (200, MSG296) with the recording id: status Deleted, DeletedAt = now, UpdatedAt = now; the transcript with its segments is removed.<br>Exception: N/A<br>Log Message: Audit RecordingErased (meeting, file name, reason 'request', actor = the Secretary); the audio file is deleted after the commit; RecordingsChanged published. | Normal | UC-44e · BR-348 · BR-341 · MSG296 |
| UTC002 | Recording already Deleted. | recordingId; actorId: the Secretary. | Return: Success (200, MSG296) with the same recording id; nothing is changed.<br>Exception: N/A<br>Log Message: No new audit row, no file deletion, no event. | Boundary | UC-44e · BR-348 · MSG296 |
| UTC003 | Recording in an Ended meeting. | actorId: the Secretary. | Return: Success; erasing is allowed at any meeting status.<br>Exception: N/A<br>Log Message: Audit RecordingErased; file deleted. | Boundary | UC-44e · BR-348 · MSG296 |
| UTC004 | Combined recording of a hybrid meeting, Available. | actorId: the Secretary. | Return: Success; status Deleted, so the combined recording is not built again.<br>Exception: N/A<br>Log Message: Audit RecordingErased. | Normal | UC-44e · BR-348 · BR-457 · MSG296 |
| UTC005 | No recording with this id. | recordingId: a random id. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-44e · BR-348 |
| UTC006 | The actor's account is deactivated. | actorId: the deactivated user. | Return: Inactive (403).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-44e · BR-348 |
| UTC007 | The actor is not on the roster. | actorId: the creator, not a participant. | Return: NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-44e · BR-348 · BR-350 |
| UTC008 | The actor is the Chair, not the Secretary. | actorId: the Chair. | Return: Forbidden (403, MSG295).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-44e · BR-348 · BR-341 · MSG295 |
| UTC009 | A room-mic take still streaming: status Pending. | actorId: the Secretary. | Return: TakeOpen (409, MSG364); the take is not touched.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-44e · BR-348 · MSG364 |
| UTC010 | Recording whose file cannot be deleted from the disk (access denied). | actorId: the Secretary. | Return: Success (200, MSG296); the row stays Deleted.<br>Exception: N/A<br>Log Message: Error log 'Could not delete recording file'; audit RecordingErased written. | Boundary | UC-44e · BR-348 · MSG296 |
| UTC011 | The database fails while saving. | actorId: the Secretary. | Return: SaveFailed (503, MSG298); the file is not deleted.<br>Exception: PersistenceException caught; logged.<br>Log Message: Error log 'Failed to erase recording'. | Abnormal | UC-44e · BR-348 · MSG298 |
