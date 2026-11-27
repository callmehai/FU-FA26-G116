# RoomTake.Complete — Unit (FE-09, REC)

Description: Room take lifecycle: begin, append, complete, complete idle, delete.

Precondition: Meeting in progress; storage recorder.

## RoomTakeService.CompleteAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RoomTakeService.cs:136`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. A Pending room take made by the Secretary in an InProgress InPerson meeting.<br>2. Clock reads 2026-11-14 12:00 UTC.<br>3. The take holds 320,044 bytes (10,000 ms); the meeting has no other room recording. | recordingId: the take; actor: its maker. | Return: Success (200, MSG363): the take becomes the room recording, status Available, one take (offset 0, 10,000 ms, started at the take's creation time), DurationMs 10,000; result says it was not appended.<br>Exception: N/A<br>Log Message: File cut to its whole-millisecond size and its WAV lengths sealed; a Transcript in status Pending is created; audit RecordingTakeSaved kept true with takeMs; RecordingsChanged published. | Normal | UC-52a · BR-108 · MSG363 |
| UTC002 | 1. A Pending room take made by the Secretary in an InProgress InPerson meeting.<br>2. Clock reads 2026-11-14 12:00 UTC.<br>3. The take has 5,000 ms of sound (160,044 bytes). The room recording has 2 takes and 30,000 ms (960,044 bytes) with a Completed transcript. | recordingId: the take; actor: its maker. | Return: Success (200, MSG363): the take is appended: the room recording gets a third take at offset 30,000 ms of 5,000 ms, its size 1,120,044 bytes, DurationMs 35,000; the transcript becomes Pending (error cleared, progress 0); the take row is removed. Result: room recording id, appended true.<br>Exception: N/A<br>Log Message: Joined file written; audit RecordingTakeSaved on the room recording with appended true, takeMs 5000, totalMs 35000; the previous room file and the take's file are deleted after the commit; RecordingsChanged published. | Normal | UC-52a · BR-108 · UC-54a · MSG363 |
| UTC003 | 1. A Pending room take made by the Secretary in an InProgress InPerson meeting.<br>2. Clock reads 2026-11-14 12:00 UTC.<br>3. The room recording is 314,000,064 bytes; the take is 572,812 bytes (data 572,768), one 32-byte step over the limit. | recordingId; actor: maker. | Return: Success: the take does not fit, so it becomes a new recording of its own (Available, its own take list, own Pending transcript); result appended false.<br>Exception: N/A<br>Log Message: Audit RecordingTakeSaved with kept true; no files deleted. | Boundary | UC-52a · BR-108 · MSG363 |
| UTC004 | The take is Pending; the actor is the Chair. | actor: the Chair. | Return: Forbidden (403, MSG295).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-52a · BR-108 · MSG295 |
