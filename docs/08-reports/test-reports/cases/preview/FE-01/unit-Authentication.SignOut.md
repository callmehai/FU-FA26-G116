# Authentication.SignOut — Unit (FE-01, AUTH)

Description: Sign-out revokes the presented refresh token without failing for an unknown token.

Precondition: Refresh token store seeded.

## AuthenticationService.SignOutAsync

Layer: Application · `backend/TrustCabinet.Application/Authentication/AuthenticationService.cs:233`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Refresh token T1 is stored for account organizer@example.com. | refreshToken: "T1". | Return: Completes without error; T1 is revoked, so no new access token can be issued with it (an access token already issued stays valid until expiry).<br>Exception: N/A<br>Log Message: Sign-out: session of account {UserId} ended. | Normal | UC-03 · BR-09 |
| UTC002 | Token store has no live record of the token (already revoked, expired or never issued). | refreshToken: "dead-token". | Return: Completes without error and without a failure result; sign-out is treated as successful.<br>Exception: N/A<br>Log Message: Sign-out with no live refresh token; nothing to revoke. | Boundary | UC-03 · BR-09 |
| UTC003 | No token store call is needed for a blank token. | refreshToken: "". | Return: Completes without error; the token store is not called.<br>Exception: N/A<br>Log Message: Sign-out with no live refresh token; nothing to revoke. | Boundary | UC-03 |
