# EmailQueueProcessor.ProcessNext — Unit (FE-13, NOTI)

Description: Email queue worker: claim the oldest message, send it, retry with backoff or fail permanently.

Precondition: Queued email rows with attempts below the maximum; provider reachable.

## EmailQueueProcessor.ProcessNextAsync

Layer: Application · `backend/TrustCabinet.Application/Email/EmailQueueProcessor.cs:33`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. No email in the queue.<br>2. Defaults: MaxAttempts 8, RetryDelaySeconds 60, ProcessingLeaseSeconds 120. | No input; clock 2026-11-18T03:00Z. | Return: NoWork, AttemptCount 0. The provider is not called.<br>Exception: N/A<br>Log Message: N/A | Normal | BR-38 · BR-76 |
| UTC002 | A Queued email is due: NextAttemptAt in the past, ToAddress demo@demo.invalid; the domain demo.invalid is in Email:UndeliverableDomains. | Clock 2026-11-18T03:00Z. | Return: PermanentlyFailed, AttemptCount 1. The provider is not called.<br>Exception: N/A<br>Log Message: The message is marked Failed at once with the error "Not sent: the recipient's domain never receives mail (Email:UndeliverableDomains)." and no retry; an information line is logged. | Abnormal | BR-38 · BR-76 |
| UTC003 | A Queued email is due: NextAttemptAt in the past, ToAddress demo@DEMO.invalid. (upper case, trailing dot); the blocked domain is demo.invalid. | Clock 2026-11-18T03:00Z. | Return: PermanentlyFailed: the domain match ignores case and a trailing dot.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-38 · BR-76 |
| UTC004 | 1. A Queued email with AttemptCount 8 (the maximum) is still waiting.<br>2. No other due email. | Clock 2026-11-18T03:00Z. | Return: NoWork.<br>Exception: N/A<br>Log Message: Before the claim, rows that reached MaxAttempts are marked Failed; nothing is sent. | Boundary | BR-38 · BR-76 |
