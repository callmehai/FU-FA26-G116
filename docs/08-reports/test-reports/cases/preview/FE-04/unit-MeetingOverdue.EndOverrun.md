# MeetingOverdue.EndOverrun — Unit (FE-04, MEET)

Description: System auto-end sweep: running meetings past the cutoff are ended.

Precondition: InProgress meetings past the cutoff.

## MeetingOverdueService.EndOverrunAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingOverdueService.cs:51`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | No meeting is overrun. | AutoEndAfterHours: 24. | Return: Returns 0; the lifecycle service is not called.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-22 · BR-460 |
| UTC002 | Three overrun meetings; each end succeeds. | AutoEndAfterHours: 24. | Return: Returns 3.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-22 · BR-460 |
| UTC003 | Three overrun meetings; one has been ended by the Chair before the sweep reaches it. | AutoEndAfterHours: 24. | Return: Returns 2 (the skipped one is not counted).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-22 · BR-460 |
| UTC004 | Configured AutoEndAfterHours is 48. | AutoEndAfterHours: 48. | Return: Cutoff is now-48h and the reason text says 48 giờ.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-22 · BR-460 |
