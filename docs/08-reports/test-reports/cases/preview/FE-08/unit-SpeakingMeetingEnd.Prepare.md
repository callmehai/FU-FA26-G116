# SpeakingMeetingEnd.Prepare — Unit (FE-08, ROOM)

Description: Ending a meeting closes open speaking requests.

Precondition: Meeting with open requests.

## SpeakingMeetingEnd.PrepareAsync

Layer: Application · `backend/TrustCabinet.Application/Speaking/SpeakingService.cs:265`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting with one Queued, one Invited and one Speaking request.<br>2. Caller holds the meeting transaction and lock. | meetingId: the meeting; endedAt: 2026-11-10T09:00:00Z. | Return: The Speaking turn becomes Finished and the other two become Cancelled, all with EndedAt = endedAt; the ending itself publishes no speaking change (readers reload on the meeting-ended notice).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-79 · BR-337 |
| UTC002 | 1. Meeting with no open requests (only Finished and Cancelled ones).<br>2. Caller holds the meeting transaction and lock. | meetingId: the meeting; endedAt: any. | Return: Nothing is closed and no row changes.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-79 · BR-337 |
| UTC003 | 1. Meeting with an open request raised under a guest invitation.<br>2. Caller holds the meeting transaction and lock. | meetingId: the meeting; endedAt: 2026-11-10T09:00:00Z. | Return: The guest request is closed like any member request (Cancelled while Queued or Invited, Finished while Speaking).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-79 · BR-337 |
