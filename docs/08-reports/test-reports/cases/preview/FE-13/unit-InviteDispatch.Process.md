# InviteDispatch.Process — Unit (FE-13, NOTI)

Description: Invitation dispatch worker: pick the next staged event and send.

Precondition: Staged invitation events.

## MeetingInvitationDispatchProcessor.ProcessAsync

Layer: Application · `backend/TrustCabinet.Application/Email/MeetingInvitationDispatchProcessor.cs:37`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Email delivery is disabled. | id: any dispatch id. | Return: 0; nothing is read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-20 · BR-300 |
| UTC002 | A Pending dispatch whose NextAttemptAt equals the clock, 03:00Z. | id of that dispatch. | Return: 1 (an email is queued): a dispatch due exactly now is processed.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-20 · BR-300 |
| UTC003 | 1. An Invitation dispatch for an internal participant on a Published meeting.<br>2. Participant Bình, role Member; application base URL https://tc.example.vn/. | id of the dispatch. | Return: 1 email queued: subject "Lời mời tham dự cuộc họp: <title>"; text body with greeting, start, end, "Vai trò: Thành viên", place and "Xem cuộc họp: https://tc.example.vn/mgmt/meetings/<meetingId>"; the organizer's name and email as contact; an HTML body from the invitation template; a METHOD:REQUEST calendar attachment.<br>Exception: N/A<br>Log Message: The dispatch becomes Materialized, EmailMessageId set, LastError empty, NextAttemptAt cleared. | Normal | UC-20 · BR-300 |
| UTC004 | Same, DependencyAttemptCount 1, 2, 3. | id of the dispatch, three times. | Return: 0 each time.<br>Exception: N/A<br>Log Message: NextAttemptAt is now + 5 minutes, + 30 minutes and + 360 minutes; 360 minutes also for any higher count. | Boundary | UC-04 · BR-300 |
