# Meeting.GetHistory — Unit (FE-04, MEET)

Description: Status history reading: paging bounds and who may read reasons and the full history.

Precondition: Meeting with status history rows.

## MeetingService.GetHistoryAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:739`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting created by the actor with 3 history rows. | id: meeting; actorId: creator; page: 1; pageSize: 20. | Return: Success with 3 items, page 1, page size 20, total 3; every field of each row visible including rejection reason and organizer hand-over ids.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-38 · BR-445 |
| UTC002 | Page is 0. | page: 0; pageSize: 20. | Return: Failure InvalidRequest; MSG86; the meeting is not read.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · MSG86 |
| UTC003 | Page size is 0. | page: 1; pageSize: 0. | Return: Failure InvalidRequest; MSG86.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · MSG86 |
| UTC004 | Page size is 101. | page: 1; pageSize: 101. | Return: Failure InvalidRequest; MSG86.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · MSG86 |
| UTC005 | Page size is exactly 100. | page: 1; pageSize: 100. | Return: Success.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-38 · BR-445 |
| UTC006 | Meeting does not exist. | id: unknown. | Return: Failure NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-38 · BR-445 |
| UTC007 | Actor has no relation to the meeting. | actorId: stranger. | Return: Failure NotFound.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 |
| UTC008 | Requested page lies beyond the available rows (3 rows exist). | page: 50; pageSize: 20. | Return: Success with an empty item list, page 50, page size 20, total 3.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-38 · BR-445 |
| UTC009 | Actor is the assigned approver of a WaitingApproval meeting. | actorId: approver. | Return: Success with the full history, organizer ids included.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-38 · BR-445 |
| UTC010 | Actor is the Chair; rows include a Reject with reason and a TransferOrganizer. | actorId: Chair. | Return: Success; the rejection reason is visible; PreviousOrganizerId and NewOrganizerId are null.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-38 · BR-445 |
| UTC011 | Actor is a Member of a Published meeting; rows include a Reject with reason. | actorId: Member. | Return: Success; rejection reason is null on every row; organizer ids are null.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-38 · BR-445 |
| UTC012 | The store fails while reading. | actorId: creator. | Return: Failure ReadFailed; MSG136.<br>Exception: PersistenceException is caught inside the service.<br>Log Message: Error logged with the meeting id. | Abnormal | UC-16 · MSG136 |
