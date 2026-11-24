# CombinedRecording.BuildNext — Unit (FE-09, REC)

Description: Building the combined recording of a finished meeting.

Precondition: Meeting with clips; storage recorder.

## CombinedRecordingService.BuildNextAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/CombinedRecordingService.cs:53`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Hybrid, non-Secret meeting ended 2026-11-14 10:00 UTC.<br>2. One available room-mic recording with a single take of 60,000 ms started 2026-11-14 03:05 UTC; no take is streaming.<br>3. Three online sentences of 5,000 ms each with audio files in 16 kHz mono 16-bit WAV, started inside the room take.<br>4. The latest change to any source was more than 1 minute ago; no combined recording exists yet. | No arguments. | Return: true; a new recording is stored: source Combined, status Available, content type audio/wav, file name ban-ghi-tong-hop-14-11-2026-1005.wav (Vietnam time of the earliest source), duration 60,000 ms, size 44 + 1,920,000 bytes, consent time, purpose and maker copied from the room recording, retention 180 days from now, UpdatedAt equal to the last source change.<br>Exception: N/A<br>Log Message: Saved recording row; Information log 'Built the combined recording' with 1 room take, 3 online sentences; a RecordingsChanged event is published to the meeting. | Normal | UC-43a · BR-456 |
| UTC002 | Same hybrid meeting, but the room recording's take is still streaming (status Pending). | No arguments. | Return: false; the meeting is skipped until the take is saved; no mixing is done.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43a · BR-456 |
| UTC003 | Newest source change was exactly 1 minute ago (equal to the settle limit). | No arguments. | Return: true; the combined recording is built because the change is not later than the settle limit.<br>Exception: N/A<br>Log Message: Combined recording row saved; RecordingsChanged published. | Boundary | UC-43a · BR-456 |
| UTC004 | A combined recording exists, status Deleted (the Secretary erased it). | No arguments. | Return: false; it is never built again.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43a · UC-44e · BR-456 · BR-457 |
| UTC005 | The room take and the online sentences together would make a track longer than 12 hours (sources spread over 13 hours of sound). | No arguments. | Return: true; no recording is created and the 12-hour limit is reported as a failed build for this meeting.<br>Exception: InvalidOperationException 'The combined recording would last ... ms, past the limit.' raised inside the build and caught; logged as an error.<br>Log Message: Error log 'Could not build the combined recording of meeting'; the meeting is remembered as failed at its current change time. | Boundary | UC-43a · BR-456 |
