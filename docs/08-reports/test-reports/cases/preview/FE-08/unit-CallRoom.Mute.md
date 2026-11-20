# CallRoom.Mute — Unit (FE-08, ROOM)

Description: Chair muting a participant.

Precondition: Meeting in progress; chair actor.

## CallRoomService.MuteAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:167`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting InProgress with mode Online; caller is the current Chair.<br>2. The target member is in the room with an unmuted microphone; LiveKit is configured. | meetingId: the meeting; actorId: the Chair; target: the member identity. | Return: Success; that microphone track is muted. Only muting, never unmuting: nobody gets their microphone switched on by someone else.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-41 |
| UTC002 | 1. Meeting InProgress with mode Online; caller is the current Chair.<br>2. The target member is in the room and already muted. | meetingId: the meeting; actorId: the Chair; target: the member identity. | Return: Success; the provider is not called again for an already-muted microphone.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-41 |
| UTC003 | 1. Meeting InProgress with mode Online; caller is a current Member (not the Chair).<br>2. The target is in the room with a microphone. | meetingId: the meeting; actorId: the Member; target: another member identity. | Return: NotChair; 403 "Chỉ chủ toạ mới tắt được micro của người khác." (MSG123).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41 · MSG123 |
| UTC004 | Caller has no room access for the meeting (unknown user). | meetingId: the meeting; actorId: the stranger; target: a member identity. | Return: NotFound; existence stays hidden.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41 |
| UTC005 | Meeting Published with mode Online; caller is the current Chair. | meetingId: the meeting; actorId: the Chair; target: a member identity. | Return: RoomNotOpen; 409 "Phòng họp chỉ mở khi cuộc họp đang diễn ra." (MSG117).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41 · MSG117 |
| UTC006 | Meeting InProgress with mode InPerson; caller is the current Chair. | meetingId: the meeting; actorId: the Chair; target: a member identity. | Return: RoomNotOpen (MSG117); an in-person sitting has no room to mute in.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41 · MSG117 |
| UTC007 | 1. Meeting InProgress with mode Online; caller is the current Chair.<br>2. LiveKit is not configured. | meetingId: the meeting; actorId: the Chair; target: a member identity. | Return: NotConfigured; 503 "Phòng họp trực tuyến chưa sẵn sàng. Vui lòng liên hệ quản trị viên." (MSG281).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41 · MSG281 |
| UTC008 | 1. Meeting InProgress with mode Online; caller is the current Chair.<br>2. LiveKit is configured but the target is not in the room (or has no microphone track). | meetingId: the meeting; actorId: the Chair; target: that identity. | Return: TargetNotInRoom; 409 "Người này không còn trong phòng hoặc chưa bật micro." (MSG282).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41 · MSG282 |
| UTC009 | 1. Meeting InProgress with mode Online; caller is the current Chair; the target has a microphone.<br>2. The provider refuses the mute call. | meetingId: the meeting; actorId: the Chair; target: the member identity. | Return: ProviderFailed; 502 "Máy chủ phòng họp không phản hồi. Vui lòng thử lại." (MSG124).<br>Exception: N/A<br>Log Message: Could not mute {Identity} in meeting {MeetingId}. | Abnormal | UC-41 · MSG124 |
| UTC010 | 1. Meeting InProgress with mode Online; caller is the current Chair.<br>2. The access read fails in storage. | meetingId: the meeting; actorId: the Chair; target: a member identity. | Return: ReadFailed; 500 "Không thể tắt micro. Vui lòng thử lại." (MSG284).<br>Exception: N/A<br>Log Message: Could not read call room access for meeting {MeetingId}. | Abnormal | UC-41 · MSG284 |
