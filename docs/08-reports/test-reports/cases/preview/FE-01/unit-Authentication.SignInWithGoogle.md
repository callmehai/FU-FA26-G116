# Authentication.SignInWithGoogle — Unit (FE-01, AUTH)

Description: Google sign-in: identity verification, linking an administrator-created account by email, deactivated and unlinked accounts.

Precondition: Google token verifier returns an identity or null; account rows seeded.

## AuthenticationService.SignInWithGoogleAsync

Layer: Application · `backend/TrustCabinet.Application/Authentication/AuthenticationService.cs:98`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Verifier returns identity { subject: g-100, email: organizer@example.com }.<br>2. Active account already linked to subject g-100. | idToken: "<valid token>". | Return: GoogleSignInResult Success with the user, capabilities of the role, access token, refresh token (stored) and expiry.<br>Exception: N/A<br>Log Message: Google sign-in succeeded for account {UserId}.; audit row SignedIn (details { method: "google" }). | Normal | UC-02 · BR-003 · BR-009 |
| UTC002 | 1. Verifier returns identity { subject: g-300, email: former@example.com }.<br>2. Account former@example.com is deactivated and was never linked to a Google subject. | idToken: "<valid token>". | Return: GoogleSignInResult Failure(AccountDeactivated) (MSG02); the subject is not linked and no token is issued.<br>Exception: N/A<br>Log Message: Google sign-in rejected: account {UserId} is deactivated and was never linked.; audit row SignInFailed (details { method: "google", reason: "account_deactivated" }). | Abnormal | UC-02 · BR-001 · BR-009 · MSG02 |
| UTC003 | 1. Verifier returns identity { subject: g-400, email: stranger@gmail.com }.<br>2. No internal account has that subject or email. | idToken: "<valid token>". | Return: GoogleSignInResult Failure(NotLinked) (MSG05); no account is created.<br>Exception: N/A<br>Log Message: Google sign-in rejected: no internal account is linked to that Google identity.; no audit row. | Abnormal | UC-02 · BR-003 · MSG05 |
| UTC004 | 1. Verifier returns identity of a linked active account.<br>2. The account role holds no capabilities (BR-002). | idToken: "<valid token>". | Return: GoogleSignInResult Success with an empty capability list and a token pair.<br>Exception: N/A<br>Log Message: Google sign-in succeeded for account {UserId}.; audit row SignedIn (method "google"). | Boundary | UC-02 · BR-002 · BR-009 |
