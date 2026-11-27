# Attendance.Save — Unit (FE-06, PART)

Description: Attendance save: secretary-only write with optimistic concurrency and totals on read.

Precondition: InProgress meeting; actor is an active secretary; current version token known.

## AttendanceService.SaveAsync

Layer: Application · `backend/TrustCabinet.Application/Attendance/AttendanceService.cs:76`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting in status InProgress with an active Secretary S, a current Member A (attendance Unknown, version 0) and a current Guest invitation G (attendance Unknown, version 0).<br>2. Actor is S. | meetingId: the meeting; actor: user S; entries: [{Internal, A, Present, current token of A}]. | Return: Result Saved: changedCount 1; one entry for A with attendance "Present", a new version token, recorder S and recorded time; 200 / MSG147.<br>Exception: N/A<br>Log Message: Participant A: Attendance Present, AttendanceVersion 1, recorded by S. One audit row MeetingAttendanceRecorded (actor S, object Meeting) with before/after of A. Attendance change event published. | Normal | UC-33 · BR-056, BR-057 · MSG147 |
| UTC002 | 1. Meeting in status Published (not started).<br>2. Actor is its active Secretary. | entries: [{Internal, A, Present, token}]. | Return: Failure Locked; 409 / MSG149.<br>Exception: N/A<br>Log Message: Nothing saved. | Abnormal | UC-33 · BR-056, BR-057 · MSG149 |
| UTC003 | 1. Meeting in status InProgress with an active Secretary S, a current Member A (attendance Unknown, version 0) and a current Guest invitation G (attendance Unknown, version 0).<br>2. Another save has already moved A to version 1. | entries: [{Internal, A, Present, token of version 0}]. | Return: Failure Stale; 409 / MSG151; the client must reload.<br>Exception: N/A<br>Log Message: Nothing saved; no entry is partially applied even when other entries in the request are valid. | Abnormal | UC-33 · BR-056, BR-057 · MSG151 |
| UTC004 | 1. Meeting in status InProgress with an active Secretary S, a current Member A (attendance Unknown, version 0) and a current Guest invitation G (attendance Unknown, version 0).<br>2. A's attendance version is already at the maximum value of 64-bit integers. | entries: [{Internal, A, Present, token of that version}]. | Return: Failure SaveFailed; 503 / MSG150 (the version increment overflows).<br>Exception: N/A<br>Log Message: Pending changes discarded; error logged. | Boundary | UC-33 · BR-056, BR-057 · MSG150 |
| UTC005 | 1. Meeting in status InProgress with an active Secretary S, a current Member A (attendance Unknown, version 0) and a current Guest invitation G (attendance Unknown, version 0).<br>2. Request body exceeded 64 KiB. | input.ParseFailure: TooLarge. | Return: Failure TooLarge; 413 / MSG87.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-33 · BR-056, BR-057 · MSG87 |
