# Summary.ProcessNext — Unit (FE-10, TRANS)

Description: Summary worker: process the next job and requeue interrupted ones.

Precondition: Queued summary job; language model replaced.

## SummaryService.ProcessNextAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/SummaryService.cs:170`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. A draft is Queued; the model for the meeting sensitivity is ready.<br>2. The transcript, agenda and closed votes are readable. | No arguments; the worker calls it for the oldest queued draft. | Return: true; the draft is stored whole with the model name and the time it was read; the vote lines come straight from the data, never through the model.<br>Exception: N/A<br>Log Message: Saved: draft content replacing the old text whole; the earlier review and correction no longer apply. | Normal | UC-47 · BR-376 · BR-378 · BR-379 |
| UTC002 | No draft is Queued. | No arguments; the queue is empty. | Return: false; nothing is claimed and nothing changes.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-47 |
| UTC003 | A draft is Queued; another worker claims it first. | No arguments; two workers race for the same draft. | Return: true; the other worker writes the draft, this one steps aside.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-47 · Pending: needs two live workers racing one claim; not observable with an in-memory store. |
| UTC004 | A draft is Queued; the model refuses the request. | No arguments; the model answers with a failure message. | Return: true; the draft is Failed with the model message.<br>Exception: N/A<br>Log Message: Warning logged with the meeting id and the model message. | Abnormal | UC-47 |
| UTC005 | A draft is Queued; writing it fails unexpectedly. | No arguments; the model call throws. | Return: true; the draft is Failed with "Không viết được dự thảo. Vui lòng thử lại.".<br>Exception: N/A<br>Log Message: Error logged with the meeting id. | Abnormal | UC-47 |
| UTC006 | A draft is Queued and written; storing it fails. | No arguments; the store rejects the finished draft. | Return: true; the draft is Failed with "Không lưu được dự thảo. Vui lòng thử lại."; it is left out of Running so it never blocks the next request.<br>Exception: N/A<br>Log Message: Error logged with the meeting id. | Abnormal | UC-47 |
