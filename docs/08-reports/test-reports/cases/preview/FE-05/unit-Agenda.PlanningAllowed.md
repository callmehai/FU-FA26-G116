# Agenda.PlanningAllowed — Unit (FE-05, AGENDA)

Description: Planning-allowed rule: the meeting statuses in which the agenda may change.

Precondition: No database needed; pure rules.

## AgendaRules.PlanningAllowed

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:12`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting is being drafted. | status: Draft. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-65 · BR-74 |
| UTC002 | Ten files whose lengths add to more than 100 MB. | candidates: 10 files of 11 MB each (115,343,360 bytes; each also exceeds the 10 MB per-file limit, but the submission-total gate fires first). | Return: Failure TooMany (HTTP 400, MSG288).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-74 · MSG67 |
