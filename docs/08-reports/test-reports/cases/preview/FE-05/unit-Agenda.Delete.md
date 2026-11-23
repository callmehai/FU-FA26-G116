# Agenda.Delete — Unit (FE-05, AGENDA)

Description: Deleting an agenda item: a planner removes an item and the order closes up.

Precondition: Meeting in a planning or running status; actor with the needed role.

## AgendaService.DeleteAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaService.cs:107`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Pending. | actor: S; itemId: I. | Return: Success; 200 / MSG62.<br>Exception: N/A<br>Log Message: Item removed; agenda change "Deleted" published with the item id and title. | Normal | UC-23c · BR-65 · MSG62 |
| UTC002 | 1. Meeting in status InProgress; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is InProgress. | actor: Chair C; itemId: I. | Return: Failure DeleteLocked; 409 / MSG142.<br>Exception: N/A<br>Log Message: Item unchanged. | Abnormal | UC-23c · MSG142 |
| UTC003 | Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster. | actor: S; itemId: unknown. | Return: Failure NotFound; 404.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23c |
| UTC004 | Ten files whose lengths add to more than 100 MB. | candidates: 10 files of 11 MB each (115,343,360 bytes; each also exceeds the 10 MB per-file limit, but the submission-total gate fires first). | Return: Failure TooMany (HTTP 400, MSG288).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23c · BR-74 · MSG67 |
| UTC005 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I is Pending. | actor: S; the database fails while saving. | Return: Failure SaveFailed; 503 / MSG70.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23c · MSG70 |
