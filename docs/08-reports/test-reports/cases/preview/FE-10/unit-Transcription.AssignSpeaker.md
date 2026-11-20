# Transcription.AssignSpeaker — Unit (FE-10, TRANS)

Description: Editing a segment and assigning speakers.

Precondition: Transcript with segments; editor actor.

## TranscriptionService.AssignSpeakerAsync

Layer: Application · `backend/TrustCabinet.Application/Transcription/TranscriptionService.cs:188`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Voice SPEAKER_00 exists on the recording; actor is the current secretary.<br>2. A member of the meeting matches the user id. | recordingId: a transcribed recording; label: "SPEAKER_00"; userId: a meeting member; guestId: null; actorId: the current secretary. | Return: Success with the voice and the assigned name (MSG316 "Đã gán người nói.").<br>Exception: N/A<br>Log Message: Saved: assigned user, assigner and time; audit row records the naming and whether it accepts the machine suggestion. | Normal | UC-46b · BR-368 · MSG316 |
| UTC002 | Voice SPEAKER_00 is named as a member; actor is the current secretary. | recordingId: a transcribed recording; label: "SPEAKER_00"; userId: null; guestId: null; actorId: the current secretary. | Return: Success (MSG316); the name is cleared from the voice.<br>Exception: N/A<br>Log Message: Saved: assigned user and guest cleared; audit row records the clearing. | Normal | UC-46b · BR-368 · MSG316 |
| UTC003 | 1. Voice SPEAKER_01 exists; actor is the current secretary.<br>2. An invited guest of the meeting matches the guest id. | recordingId: a transcribed recording; label: "SPEAKER_01"; userId: null; guestId: an invited guest; actorId: the current secretary. | Return: Success with the voice and the guest name (MSG316).<br>Exception: N/A<br>Log Message: Saved: assigned guest, assigner and time; audit row records the naming. | Normal | UC-46b · BR-368 · MSG316 |
| UTC004 | No voice with the label exists on the recording; actor is the current secretary. | recordingId: a transcribed recording; label: "SPEAKER_09"; userId: a meeting member; guestId: null; actorId: the current secretary. | Return: Failure NotFound; answered as if the voice does not exist.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b |
| UTC005 | Voice SPEAKER_00 exists; actor is the current secretary. | recordingId: a transcribed recording; label: "SPEAKER_00"; userId: a meeting member; guestId: an invited guest; actorId: the current secretary. | Return: Failure InvalidSpeaker (MSG317 "Người được gán phải có trong danh sách cuộc họp."); a voice is named as one user or one guest, never both.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b · BR-368 · MSG317 |
| UTC006 | 1. Voice SPEAKER_00 exists; actor is the current secretary.<br>2. The user id belongs to nobody on the meeting roster. | recordingId: a transcribed recording; label: "SPEAKER_00"; userId: an outsider; guestId: null; actorId: the current secretary. | Return: Failure InvalidSpeaker (MSG317); nothing is assigned.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b · BR-368 · MSG317 |
| UTC007 | Voice SPEAKER_00 exists; actor takes part in the meeting but is not the secretary. | recordingId: a transcribed recording; label: "SPEAKER_00"; userId: a meeting member; guestId: null; actorId: a member who is not the secretary. | Return: Failure Forbidden (MSG295); nothing is assigned.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b · MSG295 |
