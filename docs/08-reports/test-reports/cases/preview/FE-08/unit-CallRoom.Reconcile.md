# CallRoom.Reconcile — Unit (FE-08, ROOM)

Description: Room provider webhook handling, reconciliation and access enforcement.

Precondition: Room provider events as input.

## CallRoomService.ReconcileAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:295`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting with an active session whose status left InProgress (the sitting ended).<br>2. LiveKit is configured and the provider room still exists. | No caller arguments (cancellation token only); seeded: the active session and the provider room. | Return: The provider room is deleted and the session ended; open speaking requests are closed with it via SpeakingLeftAsync (a Left change is published when hands were up).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 · Deviation: at 7286680a closing the room here notified nobody and dropped the closed-hands count with no speaking-queue update; the current code closes the hands instead — SCRUM-213 |
| UTC002 | 1. Meeting InProgress with an active session; roster enforcement is on.<br>2. A person with no current access is still connected to the room. | No caller arguments (cancellation token only); seeded: the session, the allowed-user list and the connected people. | Return: That person is taken out of the room; everyone still allowed keeps their recorded visits untouched.<br>Exception: N/A<br>Log Message: Removing {Identity} from {Room}: no longer allowed in. | Normal | UC-37 |
| UTC003 | Meeting InProgress; the provider room has allowed members connected but the database knows no session (every join notice was lost). | No caller arguments (cancellation token only); seeded: the provider room with two allowed members, no session. | Return: One active session is created from the earliest join, and the present members get their visits recorded.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC004 | 1. Meeting InProgress; roster enforcement is on.<br>2. A provider room exists for the meeting but nobody allowed is in it and no session is known. | No caller arguments (cancellation token only); seeded: the orphan provider room. | Return: The orphan room is deleted.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
