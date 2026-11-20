# AgendaExecution.TryComplete — Unit (FE-05, AGENDA)

Description: Completing a running item: wrong state or time refuses; success stamps the end.

Precondition: No database needed; pure rules.

## AgendaExecution.TryComplete

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:74`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Item is InProgress, started at 09:00. | item {Status: InProgress; ActualStartedAt: 2026-12-01T09:00:00Z}; endedAt: 2026-12-01T10:00:00Z. | Return: true; item Status Done, ActualEndedAt and UpdatedAt 10:00:00Z.<br>Exception: N/A<br>Log Message: Item fields Status, ActualEndedAt, UpdatedAt set. | Normal | UC-25 · BR-72 |
| UTC002 | Item ends exactly when it started. | item InProgress, started 09:00:00Z; endedAt: 09:00:00Z. | Return: true; ActualEndedAt equals ActualStartedAt.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-25 · BR-72 |
| UTC003 | End is one microsecond before the start. | item InProgress, started 09:00:00.000001Z; endedAt: 09:00:00.000000Z. | Return: false; item unchanged.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-25 · BR-72 · MSG76 |
| UTC004 | End precedes the start. | item InProgress, started 09:00Z; endedAt: 08:59Z. | Return: false; item stays InProgress, ActualEndedAt unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-25 · BR-72 · MSG76 |
| UTC005 | Item is still Pending. | item {Status: Pending; ActualStartedAt: null}; endedAt: now. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-25 · BR-70 · MSG74 |
| UTC006 | Item is already Done. | item {Status: Done; ActualStartedAt: 09:00Z}; endedAt: 11:00Z. | Return: false; ActualEndedAt unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-25 · BR-70 · MSG74 |
| UTC007 | Item is InProgress but has no start time. | item {Status: InProgress; ActualStartedAt: null}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-25 · BR-72 · MSG76 |
| UTC008 | End time carries a +07:00 offset with sub-microsecond ticks. | item InProgress, started 2026-12-01T02:00:00Z; endedAt: 2026-12-01T10:00:00.1234567+07:00. | Return: true; ActualEndedAt 2026-12-01T03:00:00.123456Z (UTC, truncated to microseconds).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-25 · BR-72 |
