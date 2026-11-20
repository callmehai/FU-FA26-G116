# Agenda.PlanningAllowed — Unit (FE-05, AGENDA)

Description: Planning-allowed rule: the meeting statuses in which the agenda may change.

Precondition: No database needed; pure rules.

## AgendaRules.PlanningAllowed

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:12`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting is being drafted. | status: Draft. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-65 · BR-74 |
| UTC002 | Meeting awaits approval; agenda is still open. | status: WaitingApproval. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-65 · BR-74 |
| UTC003 | Meeting was rejected. | status: Rejected. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-74 |
| UTC004 | Meeting is approved. | status: Approved. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-74 |
| UTC005 | Meeting is published. | status: Published. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-74 |
| UTC006 | Meeting is running. | status: InProgress. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-65 · BR-74 |
| UTC007 | Meeting has ended. | status: Ended. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-74 · MSG67 |
| UTC008 | Meeting was cancelled. | status: Cancelled. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-74 · MSG67 |
