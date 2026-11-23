# Agenda.Complete — Unit (FE-05, AGENDA)

Description: Completing an agenda item: the chair closes the running item.

Precondition: Meeting in a planning or running status; actor with the needed role.

## AgendaService.CompleteAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaService.cs:113`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress, started 10 minutes ago. | actor: Chair C; itemId: I. | Return: Success; 200 / MSG65.<br>Exception: N/A<br>Log Message: I.Status Done; ActualEndedAt and UpdatedAt set to now; change "Completed" published; no other item is started automatically. | Normal | UC-25 · BR-69 · BR-72 · MSG65 |
| UTC002 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress, started in the same microsecond as now. | actor: Chair C. | Return: Success; 200 / MSG65; ActualEndedAt equals ActualStartedAt.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-25 · BR-72 · MSG65 |
| UTC003 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress but its stored start time is later than the current time. | actor: Chair C; itemId: I. | Return: Failure SaveFailed; 503 / MSG76.<br>Exception: N/A<br>Log Message: Item unchanged; an error about invalid actual timestamps is logged. | Abnormal | UC-25 · BR-72 · MSG76 |
| UTC004 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Pending. | actor: Chair C; itemId: I. | Return: Failure CannotComplete; 409 / MSG74.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-25 · BR-70 · MSG74 |
