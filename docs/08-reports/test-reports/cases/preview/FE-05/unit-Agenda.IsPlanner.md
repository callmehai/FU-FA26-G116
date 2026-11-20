# Agenda.IsPlanner — Unit (FE-05, AGENDA)

Description: Planner rule: the chair and the secretary plan the agenda.

Precondition: No database needed; pure rules.

## AgendaRules.IsPlanner

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:23`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Actor is the current Chair with an active account. | meeting with participant {actor; Chair; Current; active}. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-65 |
| UTC002 | Actor is a current Secretary with an active account. | participant {actor; Secretary; Current; active}. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-65 |
| UTC003 | Actor is a plain Member. | participant {actor; Member; Current; active}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-65 · MSG67 |
| UTC004 | Chair's account was deactivated. | participant {actor; Chair; Current; User.IsActive: false}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-65 |
| UTC005 | Secretary was removed from the roster. | participant {actor; Secretary; AssignmentStatus: Removed}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-28c · BR-65 |
| UTC006 | Actor is only the meeting organizer. | Meeting.CreatedById: actor; no roster row for actor. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-65 |
