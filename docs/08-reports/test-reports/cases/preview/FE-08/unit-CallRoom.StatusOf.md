# CallRoom.StatusOf — Unit (FE-08, ROOM)

Description: Online room status: whether the call room is open for a meeting status and mode.

Precondition: Meeting with known status and mode.

## CallRoomRules.StatusOf

Layer: Application · `backend/TrustCabinet.Application/Calls/CallContracts.cs:22`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting InProgress with mode Online. | status: InProgress; mode: Online. | Return: Open; the room can be entered and the waiting screen offers the join button.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC002 | Meeting InProgress with mode Hybrid. | status: InProgress; mode: Hybrid. | Return: Open; a hybrid sitting has the same online room as an online one.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC003 | Meeting Published with mode Online (approved, not started yet). | status: Published; mode: Online. | Return: NotStarted; the waiting screen shows "Phòng họp chỉ mở khi cuộc họp đang diễn ra." (MSG117) and opens by itself when the sitting starts.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · MSG117 |
| UTC004 | Meeting Ended with mode Online. | status: Ended; mode: Online. | Return: Ended; the room is closed and no new ticket is issued.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 |
| UTC005 | Meeting Cancelled after it was published, with mode Online. | status: Cancelled; mode: Online. | Return: Cancelled; only a meeting cancelled after publishing reaches this (a meeting cancelled earlier is answered as if it did not exist).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-75 |
| UTC006 | Meeting InProgress with mode InPerson. | status: InProgress; mode: InPerson. | Return: InPersonOnly; the mode is decided first, so an in-person sitting never has a room whatever its status ("Cuộc họp này họp trực tiếp nên không có phòng họp trực tuyến." MSG280).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · MSG280 |
