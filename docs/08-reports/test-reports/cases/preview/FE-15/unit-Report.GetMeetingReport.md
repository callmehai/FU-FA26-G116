# Report.GetMeetingReport — Unit (FE-15, RPT)

Description: Meeting report: who may read, period handling and totals.

Precondition: Meetings in a period; reader with report capability.

## ReportService.GetMeetingReportAsync

Layer: Application · `backend/TrustCabinet.Application/Reports/ReportService.cs:19`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. The reader may read meetings spread over January to March 2026 with several statuses and sensitivities.<br>2. Ended meetings exist with attendance taken, known start and end times, and issued minutes. | actorId: <id of an active internal reader>; from: "2026-01"; to: "2026-03". | Return: A report From "2026-01" To "2026-03" with GeneratedAt in Vietnam time (+07:00); Total equals the sum of the groups; ByMonth holds every month of the period (months with no meeting show 0); ByStatus holds all 8 statuses in enum order; BySensitivity holds Normal, Internal and Secret; attendance average is a fraction rounded to 4 decimals; duration average is in minutes rounded to 1 decimal; turnaround average is in hours rounded to 2 decimals. No id, title, participant or content of any meeting appears.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-70a · BR-432 · BR-434 · BR-435 · BR-436 · BR-437 |
| UTC002 | Only the start month is supplied. | actorId: <id of an active internal reader>; from: "2026-01"; to: null. | Return: InvalidPeriod; nothing is read and no audit row is written. The endpoint answers 400 MSG463: "Khoảng thời gian không hợp lệ. Chọn tối đa 24 tháng, tháng bắt đầu không sau tháng kết thúc."<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-70a EX-1 · BR-433 · MSG463 |
| UTC003 | 1. The period is valid.<br>2. The database fails while reading the groups. | actorId: <id of an active internal reader>; from: "2026-01"; to: "2026-12". | Return: ReadFailed; no report. The endpoint answers 500 MSG464: "Không tải được báo cáo. Vui lòng thử lại."<br>Exception: N/A<br>Log Message: Error: "Could not read the meeting report 2026-01–2026-12" with the database error as cause. | Abnormal | UC-70a EX-2 · MSG464 |
| UTC004 | Ended meetings exist in the period, but none has attendance taken, none has both start and end times, and none has issued minutes. | actorId: <id of an active internal reader>; from: "2026-01"; to: "2026-12". | Return: Total counts the meetings; attendance average is null with meetings 0 and meetingsWithoutAttendance equal to the Ended count; duration average is null with meetings 0 and meetingsWithoutTimes equal to the Ended count; turnaround average is null with meetings 0 and meetingsWithoutIssuedMinutes equal to the Ended count.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-70a · BR-435 · BR-436 |
