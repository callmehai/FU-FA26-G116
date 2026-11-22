# Agenda.IsPlanner — Unit (FE-05, AGENDA)

Description: Planner rule: the chair and the secretary plan the agenda.

Precondition: No database needed; pure rules.

## AgendaRules.IsPlanner

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:23`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Actor is the current Chair with an active account. | meeting with participant {actor; Chair; Current; active}. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-65 |
| UTC002 | Actor is a plain Member. | participant {actor; Member; Current; active}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-65 · MSG67 |
| UTC003 | Secretary was removed from the roster. | participant {actor; Secretary; AssignmentStatus: Removed}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-28c · BR-65 |
