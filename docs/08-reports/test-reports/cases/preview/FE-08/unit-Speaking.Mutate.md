# Speaking.Mutate — Unit (FE-08, ROOM)

Description: Register to speak: raise, lower, invite, dismiss.

Precondition: Meeting in progress; participants seeded.

## SpeakingService.MutateAsync

Layer: Application · `backend/TrustCabinet.Application/Speaking/SpeakingService.cs:57`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting InProgress; actor is a current participant connected to the room.<br>2. The actor holds no open request. | operation: Raise; actor: the participant. | Return: Success with Created true; the queue holds one Queued request at the next free position, stamped with the current agenda item. A Raise change is published to every reader (no invited target).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-79 · BR-339 |
| UTC002 | Meeting InProgress; actor can read the meeting but is not connected to its room. | operation: Raise; actor: that reader. | Return: NotInRoom; 409 "Bạn cần đang ở trong phòng họp để đăng ký phát biểu." (MSG458); nothing is saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-79 · MSG458 |
| UTC003 | 1. Meeting InProgress; caller is an ordinary Member.<br>2. A Queued request exists. | operation: Invite; requestId: that request; actor: the Member. | Return: NotChair; 403 "Chỉ Chủ toạ hiện hành được điều hành lượt phát biểu." (MSG457); the former Chair after a handover is refused the same way.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-79 · MSG457 · BR-338 |
| UTC004 | 1. Meeting InProgress; caller is the current Chair; someone is already Invited.<br>2. Another Queued request exists. | operation: Invite; requestId: the Queued request; actor: the Chair. | Return: FloorTaken; 409 "Đang có người được mời hoặc đang phát biểu. Hãy kết thúc lượt đó trước." (MSG459); nothing is saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-79 · MSG459 · BR-338 |
