# CallRoom.HandleWebhook — Unit (FE-08, ROOM)

Description: Room provider webhook handling, reconciliation and access enforcement.

Precondition: Room provider events as input.

## CallRoomService.HandleWebhookAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:227`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting InProgress with mode Online and no active room session yet.<br>2. A participant_joined event arrives with a valid member identity. | event: participant_joined; roomName: "meeting-{id}"; participantIdentity: the user id; participantJoinedAt: now. | Return: An active session is ensured for the meeting and one open visit is recorded for the member.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-43 |
| UTC002 | Meeting InProgress with an active session and an open visit; the leaver holds the floor of the speaking queue. | event: participant_left; roomName: "meeting-{id}"; participantIdentity: the leaver. | Return: The open visit is closed at the event time; the closed hand is published as a Left change so the queue panels refetch (no invited target).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-44 · UC-50 · BR-104–BR-106 |
| UTC003 | Meeting InProgress; two members share their screen at once (one presenter at a time). | event: track_published with trackSource ScreenShare; roomName: "meeting-{id}"; participantIdentity: the second sharer. | Return: Every other unmuted screen-share track in the room is muted; the second sharer keeps theirs.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-45c |
| UTC004 | A participant_joined event arrives with an identity that is neither a user id nor a guest identity. | event: participant_joined; roomName: "meeting-{id}"; participantIdentity: "unknown-device". | Return: Ignored; no session or visit is touched.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43 |
