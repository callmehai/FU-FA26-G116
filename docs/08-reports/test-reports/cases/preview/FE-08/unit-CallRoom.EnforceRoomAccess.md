# CallRoom.EnforceRoomAccess — Unit (FE-08, ROOM)

Description: Room provider webhook handling, reconciliation and access enforcement.

Precondition: Room provider events as input.

## CallRoomService.EnforceRoomAccessAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:320`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting with an active room session; LiveKit is configured.<br>2. A participant was just removed from the roster (or a guest invitation just revoked). | meetingId: the meeting. | Return: That meeting's room is reconciled at once: whoever lost access is taken out now instead of at the next 30-second run.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC002 | 1. Meeting with no active room session (a Published meeting, or an empty room).<br>2. LiveKit is configured. | meetingId: the meeting. | Return: Nothing happens; with nobody known to be in the room the provider is not called.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
