# Authentication.SignIn — Unit (FE-01, AUTH)

Description: Password sign-in decision: valid credentials vs unknown email vs wrong password vs deactivated account.

Precondition: Seeded users exist (active, deactivated); password hasher configured.

## AuthenticationService.SignInAsync

Layer: Application · `backend/TrustCabinet.Application/Authentication/AuthenticationService.cs:52`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Active account organizer@example.com exists with role Meeting Organizer holding capabilities [MeetingCreator].<br>2. Password hasher verifies the stored hash for the supplied password. | email: "organizer@example.com"; password: "<correct password>". | Return: SignInResult Success with the user, capabilities [MeetingCreator], an access token, a refresh token (stored for the user) and access-token expiry = now + configured lifetime.<br>Exception: N/A<br>Log Message: Sign-in succeeded for account {UserId}.; audit row SignedIn (actor = the user, object = User, details { method: "password" }). | Normal | UC-01 · NFR · BR-009 |
| UTC002 | No account exists for nobody@example.com. | email: "nobody@example.com"; password: "<any password>". | Return: SignInResult Failure(InvalidCredentials) (MSG01); the password is still verified against a dummy hash so timing does not reveal the missing account.<br>Exception: N/A<br>Log Message: Sign-in rejected: invalid credentials.; no audit row (an unknown email is not recorded). | Abnormal | UC-01 · BR-004 · BR-009 · MSG01 |
| UTC003 | Account former@example.com exists and is deactivated. | email: "former@example.com"; password: "<correct password>". | Return: SignInResult Failure(AccountDeactivated) (MSG02); no token issued.<br>Exception: N/A<br>Log Message: Sign-in rejected: account {UserId} is deactivated.; audit row SignInFailed (details { method: "password", reason: "account_deactivated" }). | Abnormal | UC-01 · BR-001 · BR-009 · MSG02 |
| UTC004 | No repository access is needed for a blank email. | email: ""; password: "<any password>". | Return: SignInResult Failure(InvalidCredentials) (MSG01); the account store is not read and nothing is recorded.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 · MSG01 |
| UTC005 | Active account exists for organizer@example.com. | email: "organizer@example.com"; password: "<wrong password>". | Return: SignInResult Failure(InvalidCredentials) (MSG01); no token issued.<br>Exception: N/A<br>Log Message: Sign-in rejected: invalid credentials.; audit row SignInFailed (details { method: "password", reason: "invalid_password" }). | Abnormal | UC-01 · BR-004 · BR-009 · MSG01 |
| UTC006 | Account former@example.com exists and is deactivated. | email: "former@example.com"; password: "<wrong password>". | Return: SignInResult Failure(InvalidCredentials) (MSG01); the caller cannot tell the account is deactivated (not MSG02).<br>Exception: N/A<br>Log Message: Sign-in rejected: invalid credentials.; audit row SignInFailed (details { method: "password", reason: "invalid_password" }). | Abnormal | UC-01 · BR-001 · BR-009 · MSG01 |
