# Agenda.CanRecordConclusion — Unit (FE-05, AGENDA)

Description: Conclusion-permission rule: who may record a conclusion, and when.

Precondition: No database needed; pure rules.

## AgendaRules.CanRecordConclusion

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:52`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Running meeting, running item, no minutes, actor is Chair. | meeting InProgress; item InProgress; hasMinutes: false; actor: Chair. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-77 · BR-65 |
| UTC002 | Minutes exist for the meeting. | meeting Ended; item Done; hasMinutes: true; actor: Chair. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-77 · MSG273 |
| UTC003 | Meeting was cancelled. | meeting Cancelled; item Done; actor: Chair. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-77 · MSG273 |
