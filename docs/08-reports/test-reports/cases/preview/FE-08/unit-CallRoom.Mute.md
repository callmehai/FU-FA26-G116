# CallRoom.Mute — Unit (FE-08, ROOM)

Description: Chair muting a participant.

Precondition: Meeting in progress; chair actor.

## CallRoomService.MuteAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:168`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting InProgress with mode Online; caller is the current Chair.<br>2. The target member is in the room with an unmuted microphone; LiveKit is configured. | meetingId: the meeting; actorId: the Chair; target: the member identity. | Return: Success; that microphone track is muted. Only muting, never unmuting: nobody gets their microphone switched on by someone else.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-41 |
| UTC002 | 1. Meeting InProgress with mode Online; caller is a current Member (not the Chair).<br>2. The target is in the room with a microphone. | meetingId: the meeting; actorId: the Member; target: another member identity. | Return: NotChair; 403 "Chỉ chủ toạ mới tắt được micro của người khác." (MSG123).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41 · MSG123 |
| UTC003 | Meeting Published with mode Online; caller is the current Chair. | meetingId: the meeting; actorId: the Chair; target: a member identity. | Return: RoomNotOpen; 409 "Phòng họp chỉ mở khi cuộc họp đang diễn ra." (MSG117).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41 · MSG117 |
| UTC004 | 1. Meeting InProgress with mode Online; caller is the current Chair.<br>2. LiveKit is not configured. | meetingId: the meeting; actorId: the Chair; target: a member identity. | Return: NotConfigured; 503 "Phòng họp trực tuyến chưa sẵn sàng. Vui lòng liên hệ quản trị viên." (MSG281).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41 · MSG281 |
