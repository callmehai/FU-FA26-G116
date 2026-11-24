# SpeechClip.Export — Unit (FE-09, REC)

Description: Speech-clip retention erasure and playback.

Precondition: Clips with ages.

## SpeechClipService.ExportAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/SpeechClipService.cs:62`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A non-secret meeting with kept sentences (one excluded); the actor is its Chair (active user). | meetingId; actorId: the Chair. | Return: Success with the transcript text file: excluded sentences left out, guest lines labelled “(Khách)”.<br>Exception: N/A<br>Log Message: Audit SpeechClipsExported (meeting, sentence count). | Normal | UC-45b · BR-351 · BR-358 |
| UTC002 | A non-secret meeting; the actor is an active Member (not Chair or Secretary). | meetingId; actorId: the Member. | Return: ExportForbidden (403, MSG355).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-45b · BR-351 · MSG355 |
| UTC003 | A secret meeting; the actor is its Chair. | meetingId of the secret meeting; actorId: the Chair. | Return: SecretMeeting (403, MSG356).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-45b · BR-351 · MSG356 |
| UTC004 | A non-secret meeting with no sentences; the actor is its Chair. | meetingId; actorId: the Chair. | Return: Success with an (empty) transcript text file.<br>Exception: N/A<br>Log Message: Audit SpeechClipsExported with sentence count 0. | Boundary | UC-45b · BR-351 |
