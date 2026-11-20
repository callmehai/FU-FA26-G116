# Agenda.Start — Unit (FE-05, AGENDA)

Description: Starting an agenda item: the chair runs a pending item when no other item runs.

Precondition: Meeting in a planning or running status; actor with the needed role.

## AgendaService.StartAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaService.cs:111`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Pending; no item is InProgress. | actor: Chair C; itemId: I. | Return: Success; 200 / MSG64.<br>Exception: N/A<br>Log Message: I.Status InProgress; ActualStartedAt and UpdatedAt set to the current time; change "Started" published. | Normal | UC-24 · BR-69 · BR-70 · BR-72 · MSG64 |
| UTC002 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Items 1 (Done), 2 (Pending), 3 (Pending). | actor: Chair C; itemId: item 3 (not the next in order). | Return: Success; 200 / MSG64.<br>Exception: N/A<br>Log Message: Any Pending item may be started regardless of position. | Boundary | UC-24 · BR-70 · MSG64 |
| UTC003 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item J is already InProgress; item I is Pending. | actor: Chair C; itemId: I. | Return: Failure AlreadyCurrent; 409 / MSG72.<br>Exception: N/A<br>Log Message: Nothing changed. | Abnormal | UC-24 · BR-71 · MSG72 |
| UTC004 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress. | actor: Chair C; itemId: I. | Return: Failure CannotStart; 409 / MSG73.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-70 · MSG73 |
| UTC005 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Done. | actor: Chair C; itemId: I. | Return: Failure CannotStart; 409 / MSG73.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-70 · MSG73 |
| UTC006 | Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster. | actor: Secretary S; itemId: Pending item. | Return: Failure Forbidden; 403 / MSG73 (only the Chair starts an item).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-69 · MSG73 |
| UTC007 | Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster. | actor: Member M. | Return: Failure Forbidden; 403 / MSG73.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-69 · MSG73 |
| UTC008 | 1. Meeting in status Published; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Pending. | actor: Chair C; itemId: I. | Return: Failure Locked; 409 / MSG73 (meeting not running).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-69 · MSG73 |
| UTC009 | 1. Meeting in status Ended; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Pending. | actor: Chair C; itemId: I. | Return: Failure Locked; 409 / MSG73.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-24 · BR-69 · MSG73 |
| UTC010 | Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster. | actor: Chair C; itemId: unknown. | Return: Failure NotFound; 404.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 |
| UTC011 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Pending. | actor: Chair C; the database fails while saving. | Return: Failure SaveFailed; 503 / MSG75.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · MSG75 |
