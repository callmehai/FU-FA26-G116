# Agenda.RecordConclusion — Unit (FE-05, AGENDA)

Description: Recording a conclusion: a planner saves the outcome of a run item before minutes exist.

Precondition: Meeting in a planning or running status; actor with the needed role.

## AgendaService.RecordConclusionAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaService.cs:116`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress; no minutes exist. | actor: Chair C; itemId: I; input {Conclusion: "  Approved the budget.  "}. | Return: Success; 200 / MSG271.<br>Exception: N/A<br>Log Message: Conclusion saved trimmed as "Approved the budget."; ConclusionUpdatedById C and ConclusionUpdatedAt/UpdatedAt set; change "ConclusionUpdated" published. | Normal | UC-77 · BR-65 · MSG271 |
| UTC002 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress. | actor: Chair C; input {Conclusion: 2001 characters}. | Return: Failure Invalid on conclusion; 400 / MSG272.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-77 · MSG272 |
| UTC003 | 1. Meeting in status Ended; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Done; minutes exist for the meeting. | actor: Chair C; input {Conclusion: "x"}. | Return: Failure Locked; 409 / MSG273.<br>Exception: N/A<br>Log Message: Conclusion unchanged. | Abnormal | UC-77 · MSG273 |
| UTC004 | One candidate of 10 MB plus 1 byte. | candidates: [{FileName "lon.pdf", Length 10,485,761}]. | Return: Failure Invalid (MSG164); errors["files[0]"] = ["lon.pdf: tệp lớn hơn 10 MB."]; the bytes are not read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-77 · MSG174 |
| UTC005 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress. | actor: Chair C; input.ParseFailure: UnsupportedMedia. | Return: Failure UnsupportedMedia; 415 / MSG34.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-77 · MSG34 |
| UTC006 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress. | actor: Chair C; the database fails while saving. | Return: Failure SaveFailed; 503 / MSG274.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-77 · MSG274 |
