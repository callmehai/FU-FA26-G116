# Authentication.Refresh — Unit (FE-01, AUTH)

Description: Refresh token rotation: a valid token is exchanged once; unknown, expired, reused or deactivated-owner tokens are refused.

Precondition: Refresh token store and user rows seeded.

## AuthenticationService.RefreshAsync

Layer: Application · `backend/TrustCabinet.Application/Authentication/AuthenticationService.cs:179`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Refresh token T1 is stored for active account organizer@example.com and is not expired or spent.<br>2. The account role currently holds capabilities [MeetingCreator, UserManager]. | refreshToken: "T1". | Return: SignInResult Success with a new access token carrying the current capabilities [MeetingCreator, UserManager] and a new refresh token T2; T1 is spent (consumed) and T2 is stored for the account.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · BR-09 · Deviation: precondition names capability UserManager, which is not a Capability in code; executed with real capabilities [MeetingCreator, AdminUsers] and the re-read behaviour held. |
| UTC002 | 1. Refresh token T1 is stored for an active account.<br>2. An administrator removed a capability from the role after the last sign-in. | refreshToken: "T1". | Return: SignInResult Success whose new access token carries only the capabilities the role holds now (re-read, not copied from the old token).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · BR-09 |
| UTC003 | No repository or token store is touched for a blank token. | refreshToken: "". | Return: SignInResult Failure(InvalidCredentials) (MSG01).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 · MSG01 |
| UTC004 | No repository or token store is touched for a whitespace token. | refreshToken: "   ". | Return: SignInResult Failure(InvalidCredentials) (MSG01).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 · MSG01 |
| UTC005 | Token store has no live record of the presented token (unknown, expired or already spent). | refreshToken: "unknown-token". | Return: SignInResult Failure(InvalidCredentials) (MSG01); the token is still presented for consumption so a replay of a spent token is detected by the store.<br>Exception: N/A<br>Log Message: Refresh rejected: token unknown, expired or already used. | Abnormal | UC-01 · BR-09 · MSG01 |
| UTC006 | Token store maps the token to an account id that no longer exists in the account store. | refreshToken: "T-orphan". | Return: SignInResult Failure(InvalidCredentials) (MSG01); the token is consumed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 · MSG01 |
| UTC007 | 1. Token T1 belongs to an account that was deactivated after sign-in.<br>2. The account holds a second live refresh token T9. | refreshToken: "T1". | Return: SignInResult Failure(AccountDeactivated) (MSG02); every refresh token of the account (T1, T9) is revoked and no token is issued.<br>Exception: N/A<br>Log Message: Refresh rejected: account {UserId} is deactivated. | Abnormal | UC-01 · BR-03 · BR-18 · MSG02 |
| UTC008 | 1. Token T1 is live when read, but another request spends it before this call consumes it.<br>2. Account is active. | refreshToken: "T1" (concurrent duplicate request). | Return: SignInResult Failure(InvalidCredentials) (MSG01): the consume does not return the account id, so no new token is issued.<br>Exception: N/A<br>Log Message: Refresh rejected: token of account {UserId} was spent concurrently. | Abnormal | UC-01 · BR-09 · MSG01 |
