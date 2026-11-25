# Transcription.ProcessNext — Unit (FE-10, TRANS)

Description: Transcription worker step: claim the next job, transcribe, store segments.

Precondition: Transcription job queued; speech worker replaced.

## TranscriptionService.ProcessNextAsync

Layer: Application · `backend/TrustCabinet.Application/Transcription/TranscriptionService.cs:374`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. One transcript is Pending; its recording is available.<br>2. The speech worker returns two voices with lines. | No arguments; the worker calls it for the oldest queued transcript. | Return: true; transcript is Completed with segments, voices, provider "self-hosted:<model>", language and progress 100; a sure match names the voice at once.<br>Exception: N/A<br>Log Message: Saved: segments and voices replaced; audit rows for machine-named voices; listeners notified of the finished transcript. | Normal | UC-45c · BR-366 · BR-369 |
| UTC002 | No transcript is Pending. | No arguments; the queue is empty. | Return: false; nothing is claimed and nothing changes.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-45c |
| UTC003 | A Pending transcript whose recording was erased (status Deleted). | No arguments; the recording is gone but its transcript row waits. | Return: true; the transcript stays Processing with an empty error; nothing is saved.<br>Exception: N/A<br>Log Message: Warning logged with the recording id and the reason. | Abnormal | UC-45c · Deviation: defensive path only; recording erasure deletes the transcript in the same transaction, so production never reaches this state |
| UTC004 | A Pending transcript whose audio file is missing from the server. | No arguments; the file was removed after queueing. | Return: true; transcript is Failed with "Tệp âm thanh không còn trên máy chủ."; nothing is stored.<br>Exception: N/A<br>Log Message: Warning logged with the recording id and the reason. | Abnormal | UC-45c |
