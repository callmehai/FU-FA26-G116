# SpeechClip.AddForGuest — Unit (FE-09, REC)

Description: Speech clips for a guest session: list, capture, delete, playback.

Precondition: Guest session in a meeting in progress.

## SpeechClipService.AddForGuestAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:352`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. A current, named guest session of a standing invitation for an Online meeting in status InProgress.<br>2. The guest confirmed the recording notice; the speech worker is configured and free. | guest session; meetingId; upload: clientClipId new, durationMs 5,000, audio/wav audio. | Return: Success (200, MSG318) with the saved sentence owned by the invitation (not a user).<br>Exception: N/A<br>Log Message: A ClipAdded change is published to the meeting. | Normal | UC-04 · BR-113 · MSG318 |
| UTC002 | A current guest session with a confirmed notice. | guest session; meetingId; upload: audio of 0 bytes. | Return: Invalid (400, MSG319) with an error on audio.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-04 · MSG319 |
| UTC003 | A current guest session, but the guest never confirmed the recording notice. | guest session without a confirmed notice; meetingId; upload: valid. | Return: NoticeRequired (400, MSG380).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-109 · MSG380 |
