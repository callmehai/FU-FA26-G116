# Recording.Add — Unit (FE-09, REC)

Description: Recording upload: permission, meeting status, validation and storage.

Precondition: Meeting with participants; file storage recorder.

## RecordingService.AddAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RecordingService.cs:63`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. InPerson meeting in status InProgress.<br>2. The actor is its current Secretary and an active user.<br>3. The audio file is already stored by the endpoint at 'recordings/x.mp3'.<br>4. Retention is 180 days; the clock reads 2026-11-14 10:00 UTC. | upload: file 'hop.mp3', audio/mpeg, 5,000,000 bytes, source Upload, consent confirmed, purpose '  Họp HĐQT  ', duration 3,600,000 ms. | Return: Success with the new recording id (201, MSG290): status Available, consent time 2026-11-14 10:00 UTC, purpose 'Họp HĐQT' (trimmed), RetainUntil 2027-05-13 10:00 UTC, maker = the Secretary, an empty transcript in status Pending is created for the worker.<br>Exception: N/A<br>Log Message: Audit RecordingUploaded (meeting, file name, size, source, purpose, retainUntil); RecordingsChanged published to the meeting; the stored file is kept. | Normal | UC-44c · BR-343 · BR-347 · MSG290 |
| UTC002 | 1. InPerson meeting in status InProgress.<br>2. The actor is its current Secretary and an active user.<br>3. The audio file is already stored by the endpoint at 'recordings/x.mp3'. | upload: consentConfirmed false. | Return: Invalid (400, MSG291) with an error on consentConfirmed.<br>Exception: N/A<br>Log Message: The stored file is deleted. | Abnormal | UC-44c · BR-343 · MSG291 |
| UTC003 | The actor is the Chair, not the Secretary. | A valid upload. | Return: Forbidden (403, MSG295).<br>Exception: N/A<br>Log Message: The stored file is deleted. | Abnormal | UC-44c · BR-341 · MSG295 |
| UTC004 | Online meeting; the actor is the Secretary. | A valid upload. | Return: NoRoom (409, MSG358).<br>Exception: N/A<br>Log Message: The stored file is deleted. | Abnormal | UC-44c · BR-340 · MSG358 |
| UTC005 | Hybrid meeting; the actor is the Secretary. | A valid upload. | Return: HybridLiveOnly (409, MSG478).<br>Exception: N/A<br>Log Message: The stored file is deleted. | Abnormal | UC-44c · BR-340 · MSG478 |
