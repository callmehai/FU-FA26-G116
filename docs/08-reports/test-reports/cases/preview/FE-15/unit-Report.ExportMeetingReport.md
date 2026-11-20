# Report.ExportMeetingReport — Unit (FE-15, RPT)

Description: Meeting report export: format and permission.

Precondition: Meetings in a period.

## ReportService.ExportMeetingReportAsync

Layer: Application · `backend/TrustCabinet.Application/Reports/ReportService.cs:35`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | The reader may read meetings in January to December 2026. | actorId: <id of an active internal reader>; from: "2026-01"; to: "2026-12". | Return: A file named "bao-cao-cuoc-hop_2026-01_2026-12.xlsx" with content type "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", holding the figures computed again at export time; the file carries only counts, rates and averages, never a meeting id, title, participant or content. An audit row (event report.exported, objectType "Report", no object id) records the actor, the period and the time.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-70b · BR-437 · BR-438 |
| UTC002 | The reader may read no meeting in the period. | actorId: <id of an active internal reader>; from: "2026-01"; to: "2026-03". | Return: A file is still built with zero counts and no averages, and the export is recorded in the audit log.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-70b AF-1 · BR-438 |
| UTC003 | The period is a single month. | actorId: <id of an active internal reader>; from: "2026-11"; to: "2026-11". | Return: A file named "bao-cao-cuoc-hop_2026-11_2026-11.xlsx" for that month, and the export is recorded in the audit log.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-70b · BR-433 · BR-438 |
| UTC004 | The start month is after the end month. | actorId: <id of an active internal reader>; from: "2026-12"; to: "2026-01". | Return: InvalidPeriod; no file is built and nothing is recorded. The endpoint answers 400 MSG463: "Khoảng thời gian không hợp lệ. Chọn tối đa 24 tháng, tháng bắt đầu không sau tháng kết thúc."<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-70b EX-1 · BR-433 · MSG463 |
| UTC005 | 1. The period is valid.<br>2. The database fails while reading the figures. | actorId: <id of an active internal reader>; from: "2026-01"; to: "2026-12". | Return: ExportFailed; no file is returned. The endpoint answers 500 MSG465: "Không xuất được báo cáo. Vui lòng thử lại."<br>Exception: N/A<br>Log Message: Error: "Could not export the meeting report 2026-01–2026-12" with the database error as cause. | Abnormal | UC-70b EX-2 · MSG465 |
| UTC006 | 1. The period is valid and the figures are read.<br>2. Building the Excel file fails. | actorId: <id of an active internal reader>; from: "2026-01"; to: "2026-12". | Return: ExportFailed; no file is returned and no export is recorded. The endpoint answers 500 MSG465: "Không xuất được báo cáo. Vui lòng thử lại."<br>Exception: N/A<br>Log Message: Error: "Could not export the meeting report 2026-01–2026-12" with the build error as cause. | Abnormal | UC-70b EX-2 · MSG465 |
| UTC007 | 1. The period is valid and the file is built.<br>2. Writing the audit row fails. | actorId: <id of an active internal reader>; from: "2026-01"; to: "2026-12". | Return: ExportFailed; no file is returned even though the figures were computed. The endpoint answers 500 MSG465: "Không xuất được báo cáo. Vui lòng thử lại."<br>Exception: N/A<br>Log Message: Error: "Could not export the meeting report 2026-01–2026-12" with the audit error as cause. | Abnormal | UC-70b EX-2 · BR-438 · MSG465 |
| UTC008 | 1. The period is valid.<br>2. The caller stops waiting while the export is running. | actorId: <id of an active internal reader>; from: "2026-01"; to: "2026-12"; cancellation requested. | Return: N/A; no file is returned and no export is recorded.<br>Exception: The cancellation escapes to the caller; it is not converted into ExportFailed.<br>Log Message: N/A | Abnormal | UC-70b |
