# CallRoom.HandleWebhook — Unit (FE-08, ROOM)

Description: Room provider webhook handling, reconciliation and access enforcement.

Precondition: Room provider events as input.

## CallRoomService.HandleWebhookAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:226`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting InProgress with mode Online and no active room session yet.<br>2. A participant_joined event arrives with a valid member identity. | event: participant_joined; roomName: "meeting-{id}"; participantIdentity: the user id; participantJoinedAt: now. | Return: An active session is ensured for the meeting and one open visit is recorded for the member.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC002 | 1. Meeting InProgress with mode Online and an active room session.<br>2. A participant_joined event arrives with a valid guest identity. | event: participant_joined; roomName: "meeting-{id}"; participantIdentity: "guest-{sessionId}". | Return: One open visit is recorded under that guest session (no user id); the session stays active.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 · UC-04 |
| UTC003 | 1. Meeting already Ended; its room session was closed.<br>2. A participant_joined event arrives from a ticket that outlived the meeting. | event: participant_joined; roomName: "meeting-{id}"; participantIdentity: a current participant. | Return: The visit is still recorded as evidence, then the recreated room is deleted again and the session ended at once instead of at the next reconciliation.<br>Exception: N/A<br>Log Message: Closing {Room} again: {Identity} joined after it closed. | Normal | UC-37 · BR-143 |
| UTC004 | 1. Roster enforcement is on; the joiner was just removed from the participants.<br>2. A participant_joined event arrives with that member identity. | event: participant_joined; roomName: "meeting-{id}"; participantIdentity: the removed member. | Return: The visit is recorded, then that person is taken out of the room at once instead of at the next reconciliation.<br>Exception: N/A<br>Log Message: Removing {Identity} from {Room}: room access ended. | Normal | UC-37 |
| UTC005 | Meeting InProgress with an active session and an open visit; the leaver holds the floor of the speaking queue. | event: participant_left; roomName: "meeting-{id}"; participantIdentity: the leaver. | Return: The open visit is closed at the event time; the closed hand is published as a Left change so the queue panels refetch (no invited target).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-38 · UC-79 · BR-337 |
| UTC006 | Meeting InProgress with an active session and two open visits. | event: room_finished; roomName: "meeting-{id}". | Return: The session is ended at the event time and every open visit is closed with it.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC007 | Meeting InProgress; two members share their screen at once (one presenter at a time). | event: track_published with trackSource ScreenShare; roomName: "meeting-{id}"; participantIdentity: the second sharer. | Return: Every other unmuted screen-share track in the room is muted; the second sharer keeps theirs.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-39c · BR-326 |
| UTC008 | Any event arrives for a room that is not a meeting room. | event: participant_joined; roomName: "lobby-general"; participantIdentity: any. | Return: Ignored; no session or visit is touched.<br>Exception: N/A<br>Log Message: Ignoring webhook {Event} for room {Room}. | Normal | UC-37 |
| UTC009 | A participant_joined event arrives with an identity that is neither a user id nor a guest identity. | event: participant_joined; roomName: "meeting-{id}"; participantIdentity: "unknown-device". | Return: Ignored; no session or visit is touched.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 |
