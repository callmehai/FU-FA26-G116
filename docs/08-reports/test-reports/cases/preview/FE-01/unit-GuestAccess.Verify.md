# GuestAccess.Verify — Unit (FE-01, AUTH)

Description: Guest code verification: correct code opens a session; wrong, expired and exhausted codes are refused.

Precondition: Guest invitation with a pending verification code.

## GuestAccessService.VerifyAsync

Layer: Application · `backend/TrustCabinet.Application/GuestAccess/GuestAccessService.cs:122`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Open invitation I1 (meeting Published) with latest challenge K1, code "123456", created 2 minutes ago, not consumed, 0 failed attempts.<br>2. Session lifetime is 12 hours. | invitationId: I1; linkToken: <valid link token>; code: "123456". | Return: GuestAccessResult success with GuestVerified { accessToken, expiresAt = now + 12 hours, meetingId, profileCompleted: false }; K1 is marked consumed and one guest session row is created.<br>Exception: N/A<br>Log Message: Audit row GuestVerified (no actor; object GuestSession; details { meetingId, guestInvitationId }); session row added. | Normal | UC-04 · BR-053, BR-054 · BR-009 |
| UTC002 | Invitation I1 exists. | invitationId: I1; linkToken: "wrong-token"; code: "123456". | Return: GuestAccessResult Failure(InvalidInvitation) (MSG371); no challenge is read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-052 · MSG371 |
| UTC003 | No challenge was ever created for I1. | invitationId: I1; linkToken: <valid link token>; code: "123456". | Return: GuestAccessResult Failure(InvalidCode) (MSG126) without remainingAttempts.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-053, BR-054 · MSG126 |
| UTC004 | The latest challenge expires at exactly the current time (15 minutes after creation). | invitationId: I1; linkToken: <valid link token>; code: <correct code>. | Return: GuestAccessResult Failure(InvalidCode) (MSG126): a code whose expiry is not later than now is refused.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-04 · BR-053, BR-054 · MSG126 |
| UTC005 | The latest challenge already has 5 failed attempts. | invitationId: I1; linkToken: <valid link token>; code: <even the correct code>. | Return: GuestAccessResult Failure(CodeLocked) (MSG372); no session.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-053, BR-054 · MSG372 |
