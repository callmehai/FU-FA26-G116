# CallRoom.Reconcile — Unit (FE-08, ROOM)

Description: Room provider webhook handling, reconciliation and access enforcement.

Precondition: Room provider events as input.

## CallRoomService.ReconcileAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:294`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting with an active session whose status left InProgress (the sitting ended).<br>2. LiveKit is configured and the provider room still exists. | No caller arguments (cancellation token only); seeded: the active session and the provider room. | Return: The provider room is deleted and the session ended; every person in it receives the room-closed notice.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 · Deviation: Deviation: provider room deleted and session ended, but the service publishes no room-closed notice to anyone (nothing in this path notifies the people in the room; only a speaking Left change is published when raised hands were closed). |
| UTC002 | 1. Meeting InProgress with an active session; roster enforcement is on.<br>2. A person with no current access is still connected to the room. | No caller arguments (cancellation token only); seeded: the session, the allowed-user list and the connected people. | Return: That person is taken out of the room; everyone still allowed keeps their recorded visits untouched.<br>Exception: N/A<br>Log Message: Removing {Identity} from {Room}: no longer allowed in. | Normal | UC-37 |
| UTC003 | Meeting InProgress; the provider room has allowed members connected but the database knows no session (every join notice was lost). | No caller arguments (cancellation token only); seeded: the provider room with two allowed members, no session. | Return: One active session is created from the earliest join, and the present members get their visits recorded.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC004 | 1. Meeting InProgress; roster enforcement is on.<br>2. A provider room exists for the meeting but nobody allowed is in it and no session is known. | No caller arguments (cancellation token only); seeded: the orphan provider room. | Return: The orphan room is deleted.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC005 | 1. Roster enforcement is off (synthetic connections under check).<br>2. A provider room holds only synthetic people and no session is known. | No caller arguments (cancellation token only); seeded: the provider room with synthetic connections. | Return: The room is kept; with enforcement off the synthetic people are exactly what is being checked.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC006 | LiveKit is not configured. | No caller arguments (cancellation token only). | Return: Returns at once; the provider is never called.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
