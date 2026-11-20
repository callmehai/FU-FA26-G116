# AgendaExecution.UtcMicroseconds — Unit (FE-05, AGENDA)

Description: Microsecond truncation: timestamps are normalized to whole microseconds.

Precondition: No database needed; pure rules.

## AgendaExecution.UtcMicroseconds

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:71`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Time already has microsecond precision in UTC. | time: 2026-12-01T03:00:00.123456Z. | Return: 2026-12-01T03:00:00.123456Z.<br>Exception: N/A<br>Log Message: N/A | Normal | BR-72 |
| UTC002 | Time has a 7th fractional digit. | time: 2026-12-01T03:00:00.1234567Z. | Return: 2026-12-01T03:00:00.123456Z; the extra tick is dropped, not rounded.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-72 |
| UTC003 | Time has a local offset. | time: 2026-12-01T10:00:00.5+07:00. | Return: 2026-12-01T03:00:00.5Z, offset zero.<br>Exception: N/A<br>Log Message: N/A | Normal | BR-72 |
| UTC004 | Time ends in 9 ticks (0.9 microsecond). | time: 2026-12-01T03:00:00.1234569Z. | Return: 2026-12-01T03:00:00.123456Z (truncation, not rounding up).<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-72 |
