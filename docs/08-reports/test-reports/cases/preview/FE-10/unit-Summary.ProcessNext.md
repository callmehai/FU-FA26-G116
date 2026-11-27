# Summary.ProcessNext — Unit (FE-10, TRANS)

Description: Summary worker: process the next job and requeue interrupted ones.

Precondition: Queued summary job; language model replaced.

## SummaryService.ProcessNextAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/SummaryService.cs:187`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. A draft is Queued; the model for the meeting sensitivity is ready.<br>2. The transcript, agenda and closed votes are readable. | No arguments; the worker calls it for the oldest queued draft. | Return: true; the draft is stored whole with the model name and the time it was read; the vote lines come straight from the data, never through the model.<br>Exception: N/A<br>Log Message: Saved: draft content replacing the old text whole; the earlier review and correction no longer apply. | Normal | UC-58 · UC-58, UC-59 · BR-128 |
| UTC002 | No draft is Queued. | No arguments; the queue is empty. | Return: false; nothing is claimed and nothing changes.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-58 |
| UTC003 | A draft is Queued; another worker claims it first. | No arguments; two workers race for the same draft. | Return: true; the other worker writes the draft, this one steps aside.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-58 · Claim lost to another worker: the fake store answers 0 changed rows and leaves the draft Running, the way a second worker that claimed first would. |
| UTC004 | A draft is Queued; the model refuses the request. | No arguments; the model answers with a failure message. | Return: true; the draft is Failed with the model message.<br>Exception: N/A<br>Log Message: Warning logged with the meeting id and the model message. | Abnormal | UC-58 |
