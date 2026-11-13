# Auth.SignIn — Unit (FE-01, AUTH)

Description: Password sign-in decision: valid credentials vs unknown email vs wrong password vs deactivated account.

Precondition: Seeded users exist (active, deactivated); password hasher configured.

## AuthenticationService.SignInAsync

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Active account exists for organizer@example.com.<br>2. Password hasher configured. | email: "organizer@example.com"; password: "<correct password>" | Return: Success with access token and refresh token; capabilities read from the database.<br>Exception: N/A<br>Log Message: Sign-in succeeded for account {UserId}. | Normal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/AuthenticationServiceTests.cs (An_active_user_with_the_right_password_signs_in) |
| UTC002 | No account exists for nobody@example.com. | email: "nobody@example.com"; password: "<any password>" | Return: Failure(InvalidCredentials); no sign-in attempt recorded.<br>Exception: N/A<br>Log Message: Sign-in rejected: invalid credentials. | Abnormal | UC-01 · MSG01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/AuthenticationServiceTests.cs (An_unknown_email_is_rejected) |
| UTC003 | Active account exists for organizer@example.com. | email: "organizer@example.com"; password: "<wrong password>" | Return: Failure(InvalidCredentials); sign-in attempt recorded with reason invalid_password.<br>Exception: N/A<br>Log Message: Sign-in rejected: invalid credentials. | Abnormal | UC-01 · MSG01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/AuthenticationServiceTests.cs (A_wrong_password_is_rejected) |
| UTC004 | Account former@example.com exists and is deactivated. | email: "former@example.com"; password: "<correct password>" | Return: Failure(AccountDeactivated); sign-in attempt recorded.<br>Exception: N/A<br>Log Message: Sign-in rejected: account {UserId} is deactivated. | Abnormal | UC-01 · MSG02 · Automated: tests/TrustCabinet.Web.Tests/Authentication/AuthenticationServiceTests.cs (A_deactivated_account_is_told_so_only_after_the_password_matched) |
| UTC005 | No database access needed. | email: ""; password: "<any password>" | Return: Failure(InvalidCredentials); the database is not touched.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 · MSG01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/AuthenticationServiceTests.cs (Blank_input_is_rejected_without_touching_the_database) |
| UTC006 | Account former@example.com exists and is deactivated. | email: "former@example.com"; password: "<wrong password>" | Return: Failure(InvalidCredentials); the caller cannot tell the account is deactivated.<br>Exception: N/A<br>Log Message: Sign-in rejected: invalid credentials. | Abnormal | UC-01 · MSG01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/AuthenticationServiceTests.cs (A_deactivated_account_with_a_wrong_password_reveals_nothing) |
