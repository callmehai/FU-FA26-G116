# Agenda.Reorder — Unit (FE-05, AGENDA)

Description: Reordering agenda items: a planner submits a new order; the order version changes.

Precondition: Meeting in a planning or running status; actor with the needed role.

## AgendaService.ReorderAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaService.cs:109`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Items P1 (position 1), P2 (position 2), P3 (position 3), all Pending; the client holds the current order version. | actor: S; input {OrderVersion: current; OrderedItemIds: [P3, P1, P2]}. | Return: Success; 200 / MSG63.<br>Exception: N/A<br>Log Message: P3 position 1, P1 position 2, P2 position 3; UpdatedAt set on each moved item; agenda change "Reordered" published. | Normal | UC-23d · BR-64 · MSG63 |
| UTC002 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Items P1 (position 1), P2 (position 2), P3 (position 3), all Pending; the client holds the current order version.<br>3. Another user has since started P1. | actor: S; input {OrderVersion: the older version; OrderedItemIds: [P3, P1, P2]}. | Return: Failure StaleOrder; 409 / MSG143.<br>Exception: N/A<br>Log Message: Nothing changed. | Abnormal | UC-23d · MSG143 |
| UTC003 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Items P1 (position 1), P2 (position 2), P3 (position 3), all Pending; the client holds the current order version. | actor: Member M. | Return: Failure NotFound; 404, empty body.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23d · BR-65 · MSG67 · Deviation: a Member cannot read a Draft meeting, so the call answers NotFound (404, existence hidden) before the planner check; Forbidden applies once the meeting is readable (for example Published or InProgress). |
| UTC004 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Items P1 (position 1), P2 (position 2), P3 (position 3), all Pending; the client holds the current order version. | actor: S; valid input; the database fails while saving. | Return: Failure SaveFailed; 503 / MSG71.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23d · MSG71 |
