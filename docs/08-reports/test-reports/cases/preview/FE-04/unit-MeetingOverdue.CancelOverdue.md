# MeetingOverdue.CancelOverdue — Unit (FE-04, MEET)

Description: System auto-cancel sweep: overdue unstarted meetings are cancelled.

Precondition: Unstarted meetings past their planned start.

## MeetingOverdueService.CancelOverdueAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingOverdueService.cs:63`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A Draft meeting whose planned start was 30 hours ago. | AutoCancelAfterHours: 24. | Return: Returns 1; status Cancelled, CancelledById null, CancelledAt = now, CancelledFromStatus Draft; reason "Không diễn ra theo lịch: quá 24 giờ sau giờ bắt đầu dự kiến mà chưa bắt đầu."<br>Exception: N/A<br>Log Message: Saved: history row AutoCancel with no actor; vote drafts deleted in the same transaction (ADR-0067); one notification "Cuộc họp đã tự huỷ" for the organizer only. | Normal | UC-22 · BR-029 |
| UTC002 | Planned start exactly 24 hours ago. | PlannedStartTime = now-24h. | Return: Returns 1 (start is not later than now minus the delay).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-22 · BR-029 |
| UTC003 | Planned start 23 hours 59 minutes ago. | PlannedStartTime = now-23h59m. | Return: Returns 0; the meeting is not touched.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-22 · BR-029 |
| UTC004 | Meeting has moved to InProgress between the listing and the locked re-check. | status InProgress on the re-check. | Return: Not cancelled; not counted.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-22 · BR-028 |
| UTC005 | More than 50 overdue meetings. | 60 overdue meetings. | Return: At most 50 per sweep.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-22 · BR-029 |
