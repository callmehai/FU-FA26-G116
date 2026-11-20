# Transcript.AssignSegmentSpeaker — Unit (FE-10, TRANS)

Description: Editing a segment and assigning speakers.

Precondition: Transcript with segments; editor actor.

## TranscriptionService.AssignSegmentSpeakerAsync

Layer: Application · `backend/TrustCabinet.Application/Transcription/TranscriptionService.cs:230`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A line is said in SPEAKER_01; SPEAKER_00 is already named as the member; actor is the current secretary. | segmentId: a line in SPEAKER_01; userId: the member named on SPEAKER_00; guestId: null; label: null; actorId: the current secretary. | Return: Success with the line, the previous and new voice and newVoice false (MSG407 "Đã đổi người nói của câu này."); the line length moves with it.<br>Exception: N/A<br>Log Message: Saved: line voice and both voices speech time; audit row records the move. | Normal | UC-46b · BR-368 · MSG407 |
| UTC002 | 1. No voice is named as the member; actor is the current secretary.<br>2. Free voice labels remain on the recording. | segmentId: a line in SPEAKER_01; userId: a meeting member with no voice; guestId: null; label: null; actorId: the current secretary. | Return: Success with newVoice true (MSG407); a new voice carrying the name is created and the line moves to it.<br>Exception: N/A<br>Log Message: Saved: new voice with the assigned name; line voice and speech times; audit row records the move with newVoice true. | Normal | UC-46b · BR-368 · MSG407 |
| UTC003 | A line was wrongly moved away from SPEAKER_00; that voice still exists; actor is the current secretary. | segmentId: the moved line; userId: null; guestId: null; label: "SPEAKER_00"; actorId: the current secretary. | Return: Success (MSG407); the line moves back to SPEAKER_00 and its time moves back with it.<br>Exception: N/A<br>Log Message: Saved: line voice and both voices speech time; audit row records the move. | Normal | UC-46b · BR-368 · MSG407 |
| UTC004 | A line is already said in the member own voice; actor is the current secretary. | segmentId: a line in the member own voice; userId: that member; guestId: null; label: null; actorId: the current secretary. | Return: Success with newVoice false (MSG407); a change that changes nothing writes nothing.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-46b · MSG407 |
| UTC005 | A line exists; actor is the current secretary. | segmentId: an existing line; userId: a meeting member; guestId: null; label: "SPEAKER_00"; actorId: the current secretary. | Return: Failure InvalidSpeaker (MSG317); a line is said by one voice, user or guest.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b · MSG317 |
| UTC006 | No line or voice on the recording uses the label; actor is the current secretary. | segmentId: an existing line; userId: null; guestId: null; label: "SPEAKER_09"; actorId: the current secretary. | Return: Failure NotFound; another recording label is not one of this recording voices.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b |
| UTC007 | All 100 voice labels are taken by lines or voices; the member has no voice; actor is the current secretary. | segmentId: a line; userId: a meeting member with no voice; guestId: null; label: null; actorId: the current secretary. | Return: Failure InvalidSpeaker (MSG317); no free voice label remains.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-46b · MSG317 |
| UTC008 | A line exists; actor takes part in the meeting but is not the secretary. | segmentId: an existing line; userId: a meeting member; guestId: null; label: null; actorId: a member who is not the secretary. | Return: Failure Forbidden (MSG295); nothing moves.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46b · MSG295 |
