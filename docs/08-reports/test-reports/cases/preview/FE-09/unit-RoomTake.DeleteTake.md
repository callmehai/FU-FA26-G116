# RoomTake.DeleteTake — Unit (FE-09, REC)

Description: Room take lifecycle: begin, append, complete, complete idle, delete.

Precondition: Meeting in progress; storage recorder.

## RoomTakeService.DeleteTakeAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RoomTakeService.cs:264`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Room recording Available with 3 takes: take 1 at 0 ms for 10,000 ms, take 2 at 10,000 ms for 5,000 ms, take 3 at 15,000 ms for 8,000 ms; file of 736,044 bytes (23,000 ms).<br>2. The actor is its current Secretary.<br>3. Transcript Completed: line A 1,000-4,000 ms (speaker S1); line B 11,000-14,000 ms (S2, only voice in take 2); line C 16,000-20,000 ms (S1). | recordingId; take 2; actor: the Secretary. | Return: Success (200, MSG366): 160,000 bytes cut out from byte 320,044; file 576,044 bytes, DurationMs 18,000; takes become take 1 (0, 10,000) and former take 3 now at offset 10,000 ms. Line B is removed, line C moves to 11,000-15,000 ms, speaker S2 is removed, S1's speech time is recomputed.<br>Exception: N/A<br>Log Message: Audit RecordingTakeDeleted with take 2, its start time, 5,000 ms, 1 line removed, total 18,000 ms; the previous file is deleted after commit; RecordingsChanged published. | Normal | UC-44d · BR-341 · BR-349 · MSG366 |
| UTC002 | Transcript Completed; a line runs from 8,000 to 12,000 ms and take 2 covers 10,000-15,000 ms. | take 2. | Return: The line's end moves back to 10,000 ms, where the take began.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-44d · BR-341 · BR-349 · MSG366 |
| UTC003 | Transcript in status Processing. | take 2. | Return: Success; the transcript is reset to Pending with progress 0 so it is made again from the shorter file, and the last finished run's lines are cut and moved up as for a completed transcript (ADR-0077).<br>Exception: N/A<br>Log Message: Audit RecordingTakeDeleted with the number of lines cut. | Boundary | UC-44d · BR-341 · BR-349 · BR-360 · MSG366 |
| UTC004 | The recording has one take only. | take 1. | Return: LastTake (409, MSG367); the Secretary erases the whole recording instead.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-44d · BR-341 · BR-349 · MSG367 |
