# CallRoom.GetRoom — Unit (FE-08, ROOM)

Description: Room state read and closing the room at the end of a meeting.

Precondition: Meeting with a room.

## CallRoomService.GetRoomAsync

Layer: Application · `backend/TrustCabinet.Application/Calls/CallRoomService.cs:65`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting InProgress with mode Online; caller is a current participant with role Member.<br>2. LiveKit is configured and reports two people in the room. | meetingId: the meeting; caller: the participant. | Return: Success with the room view: myRole Member, status Open, available true, participantCount 2, retentionDays from configuration.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC002 | Meeting Published with mode Online; caller is a current participant. | meetingId: the meeting; caller: the participant. | Return: Success with the room view: status NotStarted, participantCount null; the provider is not asked while the room is not open.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC003 | 1. Meeting InProgress with mode Online; caller is a current participant.<br>2. LiveKit is configured but the participant count cannot be read. | meetingId: the meeting; caller: the participant. | Return: Success with the room view: status Open, participantCount null; the count is a nicety on the waiting screen and never fails the page.<br>Exception: N/A<br>Log Message: Could not count participants of meeting {MeetingId}. | Normal | UC-37 |
| UTC004 | Caller cannot read the meeting (stranger, or meeting does not exist). | meetingId: that meeting; caller: the stranger. | Return: NotFound; existence stays hidden.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 |
| UTC005 | 1. Meeting InProgress with mode Online; caller is a current participant.<br>2. The access read fails in storage. | meetingId: the meeting; caller: the participant. | Return: ReadFailed; 500 "Không thể tải trạng thái phòng họp. Vui lòng thử lại." (MSG283).<br>Exception: N/A<br>Log Message: Could not read call room access for meeting {MeetingId}. | Abnormal | UC-37 · MSG283 |
