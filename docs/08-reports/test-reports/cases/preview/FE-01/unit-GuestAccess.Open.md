# GuestAccess.Open — Unit (FE-01, AUTH)

Description: Guest link opening: opening a guest invitation link returns the meeting view or a failure.

Precondition: Meeting and guest invitation rows seeded.

## GuestAccessService.OpenAsync

Layer: Application · `backend/TrustCabinet.Application/GuestAccess/GuestAccessService.cs:28`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Invitation I1 for nguyen@example.com is not revoked.<br>2. Its meeting "Họp giao ban" is Published, planned 09:00-10:00. | invitationId: I1; linkToken: <valid link token of I1>. | Return: GuestAccessResult success with GuestInvitationView { invitationId I1, meeting { id, title "Họp giao ban", status "Published", planned start/end }, maskedEmail "ng***@example.com" }.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-04 · BR-152 |
| UTC002 | Invitation whose meeting is InProgress and not revoked. | invitationId: I1; linkToken: <valid link token>. | Return: GuestAccessResult success; meeting.status = "InProgress".<br>Exception: N/A<br>Log Message: N/A | Normal | UC-04 · BR-155 |
| UTC003 | Invitation I1 exists. | invitationId: I1; linkToken: "wrong-token". | Return: GuestAccessResult Failure(InvalidInvitation) (MSG371); the invitation store is not read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-152 · MSG371 |
| UTC004 | Invitation I1 exists. | invitationId: I1; linkToken: null. | Return: GuestAccessResult Failure(InvalidInvitation) (MSG371).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-152 · MSG371 |
| UTC005 | Invitation I1 was revoked by the Secretary. | invitationId: I1; linkToken: <valid link token>. | Return: GuestAccessResult Failure(InvalidInvitation) (MSG371): the reason is not revealed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-157 · MSG371 |
| UTC006 | Invitation I1 is not revoked but its meeting is Ended. | invitationId: I1; linkToken: <valid link token>. | Return: GuestAccessResult Failure(InvalidInvitation) (MSG371): an invitation stops working when the meeting is no longer Published or InProgress.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-155 · MSG371 |
| UTC007 | No invitation with the supplied id exists. | invitationId: <random Guid>; linkToken: <token computed for that Guid>. | Return: GuestAccessResult Failure(InvalidInvitation) (MSG371).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · MSG371 |
| UTC008 | The invitation store fails with a persistence error while reading. | invitationId: I1; linkToken: <valid link token>. | Return: GuestAccessResult Failure(Unavailable) (MSG374).<br>Exception: N/A<br>Log Message: Error log "Could not read guest invitation {InvitationId}." | Abnormal | UC-04 · MSG374 |
| UTC009 | Invitation for the email abc@example.com (at-sign at index 3) is open. | invitationId: I1; linkToken: <valid link token>. | Return: maskedEmail = "a***@example.com" (one leading character when the local part is 3 characters or fewer).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-04 |
| UTC010 | Invitation for the email abcd@example.com (at-sign at index 4) is open. | invitationId: I1; linkToken: <valid link token>. | Return: maskedEmail = "ab***@example.com" (two leading characters from a 4-character local part).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-04 |
