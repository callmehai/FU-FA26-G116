# Meeting.GetHistory — Unit (FE-04, MEET)

Description: Status history reading: paging bounds and who may read reasons and the full history.

Precondition: Meeting with status history rows.

## MeetingService.GetHistoryAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:744`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting created by the actor with 3 history rows. | id: meeting; actorId: creator; page: 1; pageSize: 20. | Return: Success with 3 items, page 1, page size 20, total 3; every field of each row visible including rejection reason and organizer hand-over ids.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-15 · BR-022, BR-023 |
| UTC002 | Page size is 101. | page: 1; pageSize: 101. | Return: Failure InvalidRequest; MSG86.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-15 · MSG86 |
| UTC003 | Meeting does not exist. | id: unknown. | Return: Failure NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-022, BR-023 |
| UTC004 | Requested page lies beyond the available rows (3 rows exist). | page: 50; pageSize: 20. | Return: Success with an empty item list, page 50, page size 20, total 3.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-15 · BR-022, BR-023 |
| UTC005 | Actor is a Member of a Published meeting; rows include a Reject with reason. | actorId: Member. | Return: Success; rejection reason is null on every row; organizer ids are null.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-15 · BR-022, BR-023 |
