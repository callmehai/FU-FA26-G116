# Transcript.AssignSegmentSpeaker — Unit (FE-10, TRANS)

Description: Editing a segment and assigning speakers.

Precondition: Transcript with segments; editor actor.

## TranscriptionService.AssignSegmentSpeakerAsync

Layer: Application · `backend/TrustCabinet.Application/Transcription/TranscriptionService.cs:230`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A line is said in SPEAKER_01; SPEAKER_00 is already named as the member; actor is the current secretary. | segmentId: a line in SPEAKER_01; userId: the member named on SPEAKER_00; guestId: null; label: null; actorId: the current secretary. | Return: Success with the line, the previous and new voice and newVoice false (MSG407 "Đã đổi người nói của câu này."); the line length moves with it.<br>Exception: N/A<br>Log Message: Saved: line voice and both voices speech time; audit row records the move. | Normal | UC-46b · BR-368 · MSG407 |
| UTC002 | A line exists; actor is the current secretary. | segmentId: an existing line; userId: a meeting member; guestId: null; label: "SPEAKER_00"; actorId: the current secretary. | Return: Failure InvalidSpeaker (MSG317); a line is said by one voice, user or guest.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b · MSG317 |
| UTC003 | No line or voice on the recording uses the label; actor is the current secretary. | segmentId: an existing line; userId: null; guestId: null; label: "SPEAKER_09"; actorId: the current secretary. | Return: Failure NotFound; another recording label is not one of this recording voices.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b |
| UTC004 | All 100 voice labels are taken by lines or voices; the member has no voice; actor is the current secretary. | segmentId: a line; userId: a meeting member with no voice; guestId: null; label: null; actorId: the current secretary. | Return: Failure InvalidSpeaker (MSG317); no free voice label remains.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-46b · MSG317 |
| UTC005 | A line exists; actor takes part in the meeting but is not the secretary. | segmentId: an existing line; userId: a meeting member; guestId: null; label: null; actorId: a member who is not the secretary. | Return: Failure Forbidden (MSG295); nothing moves.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b · MSG295 |
