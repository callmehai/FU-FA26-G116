# Authentication.Refresh — Unit (FE-01, AUTH)

Description: Refresh token rotation: a valid token is exchanged once; unknown, expired, reused or deactivated-owner tokens are refused.

Precondition: Refresh token store and user rows seeded.

## AuthenticationService.RefreshAsync

Layer: Application · `backend/TrustCabinet.Application/Authentication/AuthenticationService.cs:179`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Refresh token T1 is stored for active account organizer@example.com and is not expired or spent.<br>2. The account role currently holds capabilities [MeetingCreator, UserManager]. | refreshToken: "T1". | Return: SignInResult Success with a new access token carrying the current capabilities [MeetingCreator, UserManager] and a new refresh token T2; T1 is spent (consumed) and T2 is stored for the account.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · UC-03, NFR |
| UTC002 | No repository or token store is touched for a whitespace token. | refreshToken: "   ". | Return: SignInResult Failure(InvalidCredentials) (MSG01).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 · MSG01 |
| UTC003 | 1. Token T1 belongs to an account that was deactivated after sign-in.<br>2. The account holds a second live refresh token T9. | refreshToken: "T1". | Return: SignInResult Failure(AccountDeactivated) (MSG02); every refresh token of the account (T1, T9) is revoked and no token is issued.<br>Exception: N/A<br>Log Message: Refresh rejected: account {UserId} is deactivated. | Abnormal | UC-01 · BR-001 · MSG02 |
