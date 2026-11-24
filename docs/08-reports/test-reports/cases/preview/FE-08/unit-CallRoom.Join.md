# CallRoom.Join — Unit (FE-08, ROOM)

Description: Joining the room: who may join and in which meeting status; token issuing.

Precondition: Meeting and participants seeded; LiveKit replaced by a recorder.

## CallRoomService.JoinAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:103`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting InProgress with mode Online; caller is an active current participant.<br>2. LiveKit is configured with TokenTtlMinutes 10. | meetingId: the meeting; caller: the participant user; recordingNoticeAcknowledged: default false. | Return: Success with a join ticket: serverUrl from configuration, roomName "meeting-{id}", identity the bare user id, metadata {"meetingRole":"Member"}, expiry = now + 10 minutes. Nothing is written; the visit is recorded only when the provider reports the person actually in.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC002 | Meeting InProgress with mode InPerson; caller is a current participant. | meetingId: the meeting; caller: the participant. | Return: InPersonMeeting; 409 "Cuộc họp này họp trực tiếp nên không có phòng họp trực tuyến." (MSG280); no ticket.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · MSG280 |
| UTC003 | Meeting Published with mode Online; caller is a current participant. | meetingId: the meeting; caller: the participant. | Return: RoomNotOpen; 409 "Phòng họp chỉ mở khi cuộc họp đang diễn ra." (MSG117); no ticket.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · MSG117 |
| UTC004 | 1. Meeting InProgress with mode Online; caller is a current participant.<br>2. LiveKit is not configured. | meetingId: the meeting; caller: the participant. | Return: NotConfigured; 503 "Phòng họp trực tuyến chưa sẵn sàng. Vui lòng liên hệ quản trị viên." (MSG281); no ticket.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · MSG281 |
