# Transcription.Export — Unit (FE-10, TRANS)

Description: Transcript export: who may export and the file produced.

Precondition: Transcript seeded.

## TranscriptionService.ExportAsync

Layer: Application · `backend/TrustCabinet.Application/Transcription/TranscriptionService.cs:113`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Transcript is Completed with two segments; actor is the current chair.<br>2. SPEAKER_00 is assigned to a named member. | recordingId: a recording with a Completed transcript; actorId: the current chair. | Return: Success with a text file named ban-go-bang-<date>.txt; header says what it is and that a machine wrote it, then one [time] speaker: text line per segment.<br>Exception: N/A<br>Log Message: Saved: audit row records the export with the segment count. | Normal | UC-45b · BR-351 |
| UTC002 | Transcript is Completed; actor takes part in the meeting but is neither chair nor secretary. | recordingId: a recording with a Completed transcript; actorId: an ordinary member. | Return: Failure ExportForbidden (MSG355 "Chỉ Chủ toạ hoặc Thư ký được tải bản ghi và bản gỡ băng về."); no file.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-45b · BR-351 · MSG355 |
| UTC003 | Meeting sensitivity is Secret; transcript is Completed; actor is the current chair. | recordingId: a recording of a Secret meeting; actorId: the current chair. | Return: Failure SecretMeeting (MSG356 "Bản ghi và bản gỡ băng của cuộc họp Mật không được tải về."); no file.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-45b · BR-351 · MSG356 |
| UTC004 | Transcript is Completed with no segments; actor is the current chair. | recordingId: a recording with an empty Completed transcript; actorId: the current chair. | Return: Success; the file holds only the header and no lines.<br>Exception: N/A<br>Log Message: Saved: audit row records the export with segment count 0. | Boundary | UC-45b · BR-351 |
