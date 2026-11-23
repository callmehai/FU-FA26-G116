# SpeechClip.Add — Unit (FE-09, REC)

Description: Adding a speech clip: capture permission, duplicates by client id, limits.

Precondition: Meeting in progress; participant who may capture.

## SpeechClipService.AddAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:106`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Online meeting in status InProgress; the actor is an active participant.<br>2. The speech worker is configured and free; the sentence holds words. | meetingId; actorId: the participant; upload: clientClipId new, startedAt within 2 minutes of now, durationMs 5,000, audio/wav audio. | Return: Success (200, MSG318) with the saved sentence: the transcribed text, StartedAt from the browser clock, DurationMs 5,000, audio stored with waveform peaks.<br>Exception: N/A<br>Log Message: A ClipAdded change is published to the meeting. | Normal | UC-39a · BR-352 · MSG318 |
| UTC002 | Online meeting in status InProgress. | upload: durationMs 0. | Return: Invalid (400, MSG319) with an error on durationMs: “Một câu dài từ 1 ms đến 60 giây.”; nothing is stored.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-39a · MSG319 |
| UTC003 | 1. Online meeting in status InProgress; the actor is an active participant.<br>2. The speech worker is configured and free. | upload: durationMs 60,000 with matching audio. | Return: Success (200, MSG318); a sentence of exactly 60 seconds is accepted.<br>Exception: N/A<br>Log Message: A ClipAdded change is published to the meeting. | Boundary | UC-39a · MSG318 |
| UTC004 | Online meeting ended 8 days ago, past the 7-day late-upload window. | meetingId of an Online meeting ended 8 days ago; actorId: the participant; upload: valid. | Return: NotCapturing (409, MSG320); nothing is transcribed or stored.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-39a · BR-354 · MSG320 · Deviation: check() refuses before any audio is written, so no file is deleted |
