# Participant.Mutate — Unit (FE-06, PART)

Description: Roster changes: add, change role, remove, respond, guests, with chair/secretary invariants.

Precondition: Meeting with a roster; organizer or manager actor.

## ParticipantService.MutateAsync

Layer: Application · `backend/TrustCabinet.Application/Participants/ParticipantService.cs:348`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting in status Draft with roster revision 5: an active Chair C, an active Secretary S and an active Member A (all Current). | operation: AddInternal; actor: Secretary S; revision: "5"; userIds: [U1, U2] (two active users not on the roster); role: "Member". | Return: Success Created; 201 / MSG96; createdIds holds two new participant ids; revision "6".<br>Exception: N/A<br>Log Message: Two participants (Pending response) added; ParticipantsRevision 5 to 6; audit MeetingParticipants(AddInternal) with before/after; no invitation staged because the meeting is not published; participants-changed event published to readers. | Normal | UC-30a · MSG96 |
| UTC002 | Meeting in status Published with roster revision 5: an active Chair C, an active Secretary S and an active Member A (all Current). | operation: AddInternal; actor: Member A. | Return: Failure Forbidden; 403 / MSG85.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-30a · BR-036, BR-037 · MSG85 |
| UTC003 | Meeting in status WaitingApproval with roster revision 5: an active Chair C, an active Secretary S and an active Member A (all Current). | operation: AddInternal; actor: Secretary S. | Return: Failure Locked; 409 / MSG98.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-30a · BR-036, BR-037 · MSG98 |
| UTC004 | Meeting in status Draft with roster revision 5: an active Chair C, an active Secretary S and an active Member A (all Current). | operation: AddInternal; userIds: 100 distinct active users; role: "Member". | Return: Success Created; 201 / MSG96 with 100 ids.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-30a · MSG96 |
| UTC005 | Meeting in status Draft with roster revision 5: an active Chair C, an active Secretary S and an active Member A (all Current). | operation: AddInternal; userIds: 101 distinct users. | Return: Failure Invalid on userIds; 400 / MSG97.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-30a · MSG97 |
