# GuestAccess.SendCode — Unit (FE-01, AUTH)

Description: Guest verification code: sending, throttling and unknown-invitation answers.

Precondition: Guest invitation seeded; code sender replaced by a recorder.

## GuestAccessService.SendCodeAsync

Layer: Application · `backend/TrustCabinet.Application/GuestAccess/GuestAccessService.cs:45`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Open invitation I1 for guest.a@example.com, name "Trần Khách", meeting "Họp giao ban".<br>2. No verification code was created for I1 in the last 24 hours.<br>3. Email transport accepts the message. | invitationId: I1; linkToken: <valid link token>. | Return: GuestAccessResult success with GuestCodeSent { expiresInSeconds: 900, resendAfterSeconds: 60 } (MSG375); one challenge row saved with a 6-digit code stored only as a hash, expiring in 15 minutes; the code is sent to guest.a@example.com.<br>Exception: N/A<br>Log Message: Challenge row added; verification email sent to the invited address. | Normal | UC-04 · BR-153 · MSG375 |
| UTC002 | Invitation I1 exists. | invitationId: I1; linkToken: "wrong-token". | Return: GuestAccessResult Failure(InvalidInvitation) (MSG371); no code is created or sent.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-152 · MSG371 |
| UTC003 | The last code of I1 was created 20 seconds ago; resend interval is 60 seconds. | invitationId: I1; linkToken: <valid link token>. | Return: GuestAccessResult Failure(TooSoon) with retryAfterSeconds 40 (MSG373); no new code, nothing sent.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-153 · MSG373 |
| UTC004 | The last code of I1 was created exactly 60 seconds ago. | invitationId: I1; linkToken: <valid link token>. | Return: GuestAccessResult success (MSG375): the wait has just elapsed, so a new code is created and sent.<br>Exception: N/A<br>Log Message: Challenge row added; verification email sent. | Boundary | UC-04 · BR-153 · MSG375 |
| UTC005 | 20 codes were created for I1 in the last 24 hours (the oldest 23 hours ago); the newest was 2 minutes ago. | invitationId: I1; linkToken: <valid link token>. | Return: GuestAccessResult Failure(DailyLimit) (MSG379) with retryAfterSeconds = 3600 (until the oldest counted code is 24 hours old); nothing sent.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-153 · MSG379 |
