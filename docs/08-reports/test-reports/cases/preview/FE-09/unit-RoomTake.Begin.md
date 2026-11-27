# RoomTake.Begin — Unit (FE-09, REC)

Description: Room take lifecycle: begin, append, complete, complete idle, delete.

Precondition: Meeting in progress; storage recorder.

## RoomTakeService.BeginAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RoomTakeService.cs:32`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. InPerson meeting in status InProgress; the actor is its current Secretary and an active user.<br>2. The clock reads 2026-11-14 10:00 UTC; retention is 180 days; part size 320,000 bytes; upload limit 300 MB.<br>3. The meeting has no room recording yet. | start: consentConfirmed true, consentPurpose 'Họp giao ban'. | Return: Success: a take opens with its recording id, part size 320,000 and max size 314,572,800 bytes. The new recording is Pending, source BrowserCapture, audio/wav, file name ghi-am-phong-14-11-2026-1700.wav (Vietnam time), consent time now, purpose 'Họp giao ban', RetainUntil 2027-05-13 10:00 UTC, an empty take list, maker = the Secretary.<br>Exception: N/A<br>Log Message: An empty WAV file is created in storage; audit RecordingTakeStarted with firstTake true; RecordingsChanged published. | Normal | UC-52a · BR-108 · BR-109 · BR-110 |
| UTC002 | 1. InPerson meeting in status InProgress; the actor is its current Secretary and an active user.<br>2. The clock reads 2026-11-14 10:00 UTC; retention is 180 days; part size 320,000 bytes; upload limit 300 MB.<br>3. No room recording yet. | start: consentConfirmed false. | Return: Invalid (400, MSG291) with an error on consentConfirmed; the first take needs the Secretary's confirmation.<br>Exception: N/A<br>Log Message: No file is created, no row saved. | Abnormal | UC-52a · BR-108 · BR-109 · MSG291 |
| UTC003 | Online meeting in status InProgress; the actor is the Secretary. | start: consentConfirmed true. | Return: NoRoom (409, MSG358).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-52a · BR-108 · BR-109 · BR-107 · MSG358 |
| UTC004 | InPerson meeting in status Published; the actor is the Secretary. | start: consentConfirmed true. | Return: NotRunning (409, MSG359).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-52a · BR-108 · BR-109 · BR-107 · MSG359 |
