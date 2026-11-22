# Agenda.Start — Unit (FE-05, AGENDA)

Description: Starting an agenda item: the chair runs a pending item when no other item runs.

Precondition: Meeting in a planning or running status; actor with the needed role.

## AgendaService.StartAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaService.cs:111`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Pending; no item is InProgress. | actor: Chair C; itemId: I. | Return: Success; 200 / MSG64.<br>Exception: N/A<br>Log Message: I.Status InProgress; ActualStartedAt and UpdatedAt set to the current time; change "Started" published. | Normal | UC-24 · BR-69 · BR-70 · BR-72 · MSG64 |
| UTC002 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item J is already InProgress; item I is Pending. | actor: Chair C; itemId: I. | Return: Failure AlreadyCurrent; 409 / MSG72.<br>Exception: N/A<br>Log Message: Nothing changed. | Abnormal | UC-24 · BR-71 · MSG72 |
| UTC003 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress. | actor: Chair C; itemId: I. | Return: Failure CannotStart; 409 / MSG73.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-70 · MSG73 |
| UTC004 | Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster. | actor: Chair C; itemId: unknown. | Return: Failure NotFound; 404.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 |
| UTC005 | One candidate of exactly 10 MB with a valid PDF body. | candidates: [{FileName "lon.pdf", Length 10,485,760}]. | Return: No failure; the file size limit is inclusive.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · MSG75 |
