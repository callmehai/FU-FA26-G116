# Email.ProcessNext — Unit (FE-13, NOTI)

Description: Email queue worker: claim the oldest message, send it, retry with backoff or fail permanently.

Precondition: Queued email rows with attempts below the maximum; provider reachable.

## EmailQueueProcessor.ProcessNextAsync

*No UTC yet.*
