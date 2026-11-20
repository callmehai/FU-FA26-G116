# Transcription.ProcessNext — Unit (FE-10, TRANS)

Description: Transcription worker step: claim the next job, transcribe, store segments.

Precondition: Transcription job queued; speech worker replaced.

## TranscriptionService.ProcessNextAsync

Layer: Application · `backend/TrustCabinet.Application/Transcription/TranscriptionService.cs:334`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. One transcript is Pending; its recording is available.<br>2. The speech worker returns two voices with lines. | No arguments; the worker calls it for the oldest queued transcript. | Return: true; transcript is Completed with segments, voices, provider "self-hosted:<model>", language and progress 100; a sure match names the voice at once.<br>Exception: N/A<br>Log Message: Saved: segments and voices replaced; audit rows for machine-named voices; listeners notified of the finished transcript. | Normal | UC-45c · BR-366 · BR-369 · Pending: needs the live speech worker; transcript content comes from the worker. |
| UTC002 | No transcript is Pending. | No arguments; the queue is empty. | Return: false; nothing is claimed and nothing changes.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-45c |
| UTC003 | A Pending transcript whose recording was erased (status Deleted). | No arguments; the recording is gone but its transcript row waits. | Return: true; transcript is Failed with "Bản ghi đã bị xoá."; nothing is stored.<br>Exception: N/A<br>Log Message: Warning logged with the recording id and the reason. | Abnormal | UC-45c · Deviation: real run leaves the transcript Processing with an empty error (0 saves, warning logged) — an erased recording is discarded without marking Failed. |
| UTC004 | A Pending transcript whose audio file is missing from the server. | No arguments; the file was removed after queueing. | Return: true; transcript is Failed with "Tệp âm thanh không còn trên máy chủ."; nothing is stored.<br>Exception: N/A<br>Log Message: Warning logged with the recording id and the reason. | Abnormal | UC-45c |
| UTC005 | A Pending transcript; writing the result fails unexpectedly. | No arguments; the store fails while writing the finished transcript. | Return: true; transcript is Failed with "Không chuyển được bản ghi thành chữ do hệ thống. Hãy chạy lại."; the staged write is discarded so a restart can requeue it.<br>Exception: N/A<br>Log Message: Error logged with the transcript id; staged changes discarded. | Abnormal | UC-45c · Pending: needs the live speech worker plus a real store failure while writing the result. |
| UTC006 | A Pending transcript; the worker answers with no usable lines. | No arguments; every returned line is empty. | Return: true; transcript is Completed with no segments; provider and progress are still stored.<br>Exception: N/A<br>Log Message: Saved: empty segments and voices; listeners notified of the finished transcript. | Boundary | UC-45c · BR-369 · Pending: needs the live speech worker; the empty output comes from the worker. |
| UTC007 | A Pending transcript; the store fails while claiming it. | No arguments; the claim itself fails. | Return: No value; no transcript is claimed.<br>Exception: The storage failure is not caught here; the caller answers it.<br>Log Message: No log line here; the failure reaches the caller as is. | Abnormal | UC-45c |
