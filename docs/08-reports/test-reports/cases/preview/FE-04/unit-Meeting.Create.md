# Meeting.Create — Unit (FE-04, MEET)

Description: Meeting create and update validation: title, mode, timestamps and the Published reschedule lock.

Precondition: Organizer with meeting-creation capability; valid timestamps with offsets.

## MeetingService.CreateAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:22`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Clock is 2026-11-18T08:00:00Z.<br>2. Chair and Secretary users exist and are active. | input: title "Họp giao ban", start 2026-11-19T02:00:00Z, end 03:00:00Z, InPerson; participants: Chair user A, Secretary user B. | Return: MeetingResult Success; meeting Draft, CreatedById = actor, no approver; participants A (Chair) and B (Secretary) each with response Pending.<br>Exception: N/A<br>Log Message: Saved: Meeting row, 2 participant rows, history row Create (from none to Draft) by the organizer. | Normal | UC-14 · BR-24 · BR-26 · BR-162 · BR-320 · MSG29 |
| UTC002 | Input has an empty title. | input: title "", rest valid; participants: valid. | Return: MeetingResult Invalid; errors { title }; MSG31; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-24 · MSG31 |
| UTC003 | Planned start is not after the clock. | start = now. | Return: MeetingResult Invalid; errors { plannedStartTime: "Thời gian bắt đầu phải sau thời điểm hiện tại." }; MSG31.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-14 · BR-162 · MSG31 |
| UTC004 | Participants list is not supplied. | participants: null. | Return: MeetingResult InvalidParticipants; errors { participants: "Chọn từ 1 đến 100 thành phần tham dự, có đúng một Chủ toạ." }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-320 · MSG277 |
| UTC005 | Participants list is empty. | participants: []. | Return: InvalidParticipants; errors { participants } ; MSG277.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-14 · BR-320 · MSG277 |
| UTC006 | Participants list has 101 rows. | participants: 101 distinct active users, one Chair. | Return: InvalidParticipants; errors { participants }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-14 · BR-320 · MSG277 |
| UTC007 | Participants list has exactly 100 rows with one Chair. | participants: 100 distinct active users, one Chair, 99 Members. | Return: Success; Draft meeting with 100 participants; MSG29.<br>Exception: N/A<br>Log Message: Saved: Meeting, 100 participant rows and the Create history row. | Boundary | UC-14 · BR-24 · BR-26 · BR-162 · BR-320 · MSG29 |
| UTC008 | Only one participant, the Chair. | participants: [Chair user A]. | Return: Success; one participant; MSG29.<br>Exception: N/A<br>Log Message: Saved: Meeting, 1 participant row and the Create history row. | Boundary | UC-14 · BR-24 · BR-26 · BR-162 · BR-320 · MSG29 |
| UTC009 | Same user is listed twice. | participants: [A Chair, A Member]. | Return: InvalidParticipants; errors { participants[1].userId: "Người dùng không hợp lệ hoặc bị trùng." }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-320 · MSG277 |
| UTC010 | A participant row carries an empty user id. | participants: [Guid.Empty Chair]. | Return: InvalidParticipants; errors { participants[0].userId }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-320 · MSG277 |
| UTC011 | A participant row has an unknown role. | participants: [A role "Boss"]. | Return: InvalidParticipants; errors { participants[0].role: "Chọn Chủ toạ, Thư ký hoặc Thành viên." } and the Chair-count error; MSG277.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-320 · MSG277 |
| UTC012 | A participant row has the Guest role. | participants: [A Chair, B role "Guest"]. | Return: InvalidParticipants; errors { participants[1].role }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-150 · BR-320 · MSG277 |
| UTC013 | No participant is Chair. | participants: [A Member, B Secretary]. | Return: InvalidParticipants; errors { participants: "Cuộc họp phải có đúng một Chủ toạ." }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-320 · MSG277 |
| UTC014 | Two participants are Chair. | participants: [A Chair, B Chair]. | Return: InvalidParticipants; errors { participants: "Cuộc họp phải có đúng một Chủ toạ." }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-320 · MSG277 |
| UTC015 | One listed user does not exist. | participants: [A Chair, unknown id Member]. | Return: InvalidParticipants; errors { participants[1].userId: "Người dùng không tồn tại hoặc đã ngừng hoạt động." }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-320 · MSG277 |
| UTC016 | One listed user is deactivated. | participants: [A Chair, deactivated B Member]. | Return: InvalidParticipants; errors { participants[1].userId: "Người dùng không tồn tại hoặc đã ngừng hoạt động." }; MSG277; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-320 · MSG277 |
| UTC017 | The store fails while saving. | valid input and roster. | Return: MeetingResult SaveFailed; MSG35; the error is logged.<br>Exception: PersistenceException is caught inside the service.<br>Log Message: Error logged with the actor id; transaction rolled back. | Abnormal | UC-14 · MSG35 |
| UTC018 | The caller cancels the request. | cancellation token already cancelled. | Return: No result; the cancellation is rethrown.<br>Exception: OperationCanceledException is propagated.<br>Log Message: N/A | Abnormal | UC-14 |
