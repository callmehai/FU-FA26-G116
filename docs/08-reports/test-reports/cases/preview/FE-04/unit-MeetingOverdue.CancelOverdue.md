# MeetingOverdue.CancelOverdue — Unit (FE-04, MEET)

Description: System auto-cancel sweep: overdue unstarted meetings are cancelled.

Precondition: Unstarted meetings past their planned start.

## MeetingOverdueService.CancelOverdueAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingOverdueService.cs:60`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | No meeting is overdue. | AutoCancelAfterHours: 24. | Return: Returns 0.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-75 · BR-301 |
| UTC002 | A Draft meeting whose planned start was 30 hours ago. | AutoCancelAfterHours: 24. | Return: Returns 1; status Cancelled, CancelledById null, CancelledAt = now, CancelledFromStatus Draft; reason "Không diễn ra theo lịch: quá 24 giờ sau giờ bắt đầu dự kiến mà chưa bắt đầu."<br>Exception: N/A<br>Log Message: Saved: history row AutoCancel with no actor; one notification "Cuộc họp đã tự huỷ" for the organizer only. | Normal | UC-75 · BR-301 |
| UTC003 | A Published meeting planned to start 25 hours ago. | AutoCancelAfterHours: 24. | Return: Returns 1; CancelledFromStatus Published.<br>Exception: N/A<br>Log Message: Saved: AutoCancel history row; only the organizer is notified (no invitee notice). | Normal | UC-75 · BR-300 · BR-301 |
| UTC004 | Planned start exactly 24 hours ago. | PlannedStartTime = now-24h. | Return: Returns 1 (start is not later than now minus the delay).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-75 · BR-301 |
| UTC005 | Planned start 23 hours 59 minutes ago. | PlannedStartTime = now-23h59m. | Return: Returns 0; the meeting is not touched.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-75 · BR-301 |
| UTC006 | Meeting has moved to InProgress between the listing and the locked re-check. | status InProgress on the re-check. | Return: Not cancelled; not counted.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-75 · BR-297 |
| UTC007 | Organizer rescheduled the meeting to a later start between the listing and the re-check. | PlannedStartTime later than now-24h on re-check. | Return: Not cancelled; not counted.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-75 · BR-301 |
| UTC008 | Meeting no longer exists on the re-check. | meeting missing. | Return: Not counted.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-75 · BR-301 |
| UTC009 | Statuses WaitingApproval, Rejected and Approved, each 30 hours past planned start. | three meetings. | Return: Returns 3; each records its own previous status as CancelledFromStatus.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-75 · BR-301 |
| UTC010 | Saving one of two overdue meetings fails. | two overdue meetings; first save fails. | Return: Returns 1; the failed meeting is left for the next sweep and the tracker is discarded.<br>Exception: N/A<br>Log Message: Error logged for the failed meeting; pending changes discarded. | Abnormal | UC-75 · BR-301 |
| UTC011 | More than 50 overdue meetings. | 60 overdue meetings. | Return: At most 50 per sweep.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-75 · BR-301 |
| UTC012 | Configured AutoCancelAfterHours is 12. | AutoCancelAfterHours: 12. | Return: Cutoff now-12h and reason text says 12 giờ.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-75 · BR-301 |
