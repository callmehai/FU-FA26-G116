# Recording.Erase — Unit (FE-09, REC)

Description: Recording erasure on request and when retention expires.

Precondition: Recordings with ages; clock replaced.

## RecordingService.EraseAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RecordingService.cs:165`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Available recording with transcript and file, in a meeting where the actor is the current Secretary. | recordingId; actorId: the Secretary. | Return: Success (200, MSG296) with the recording id: status Deleted, DeletedAt = now, UpdatedAt = now; the transcript with its segments is removed.<br>Exception: N/A<br>Log Message: Audit RecordingErased (meeting, file name, reason 'request', actor = the Secretary); the audio file is deleted after the commit; RecordingsChanged published. | Normal | UC-52e · BR-110 · BR-108 · MSG296 |
| UTC002 | Recording already Deleted. | recordingId; actorId: the Secretary. | Return: Success (200, MSG296) with the same recording id; nothing is changed.<br>Exception: N/A<br>Log Message: No new audit row, no file deletion, no event. | Boundary | UC-52e · BR-110 · MSG296 |
| UTC003 | Combined recording of a hybrid meeting, Available. | actorId: the Secretary. | Return: Success; status Deleted, so the combined recording is not built again.<br>Exception: N/A<br>Log Message: Audit RecordingErased. | Normal | UC-52e · BR-110 · UC-51a · MSG296 |
| UTC004 | The actor is the Chair, not the Secretary. | actorId: the Chair. | Return: Forbidden (403, MSG295).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-52e · BR-110 · BR-108 · MSG295 |
| UTC005 | A room-mic take still streaming: status Pending. | actorId: the Secretary. | Return: TakeOpen (409, MSG364); the take is not touched.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-52e · BR-110 · MSG364 |
