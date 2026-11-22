# CallRoom.CloseMeetingRoom — Unit (FE-08, ROOM)

Description: Room state read and closing the room at the end of a meeting.

Precondition: Meeting with a room.

## CallRoomService.CloseMeetingRoomAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:51`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Online meeting just ended; its room session is still open.<br>2. LiveKit is configured. | meetingId: the meeting. | Return: The provider room "meeting-{id}" is deleted; the open session is ended at the time the provider confirms closure (never earlier than any recorded join or leave). Open visits are closed with it, and open speaking requests are closed too (a Left change is published when hands were up).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 · Deviation: at 7286680a the session ended without touching the speaking queue; the current code closes open speaking requests via SpeakingLeftAsync — SCRUM-213 |
| UTC002 | 1. Online meeting just ended.<br>2. LiveKit is not configured. | meetingId: the meeting. | Return: Returns at once; nothing is deleted and no session is touched.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC003 | Online meeting just ended; the provider room exists but the database never heard anyone in (no active session). | meetingId: the meeting. | Return: The provider room "meeting-{id}" is still deleted; with no session there is nothing to end and no session is created.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
