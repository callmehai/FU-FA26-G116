# User.RequestPasswordReset — Unit (FE-02, USER)

Description: Password reset request: link creation, email delivery and revocation rules; accounts are not revealed.

Precondition: Active and deactivated users seeded; reset email sender replaced by a recorder.

## UserService.RequestPasswordResetAsync

Layer: Application · `backend/TrustCabinet.Application/Users/UserService.cs:375`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Actor is an administrator holding AdminUsers.<br>2. Active account lan@x.vn with a password hash; email transport accepts the message. | id: that account; actorId: administrator. | Return: UserResult Success (MSG13); a reset link valid for 60 minutes is created, emailed to lan@x.vn and then activated (earlier links stop working); the current password is unchanged.<br>Exception: N/A<br>Log Message: Audit row UserPasswordResetRequested (details { email }, never the token); log "Password reset for account {UserId} requested by {ActorId}." | Normal | UC-07 · BR-16 · BR-20 · BR-21 · MSG13 |
| UTC002 | 1. Actor is an administrator holding AdminUsers.<br>2. Account is deactivated. | id: that account. | Return: UserResult Failure(PasswordResetNotAvailable) (MSG14); no link created and no email sent.<br>Exception: N/A<br>Log Message: Log "Password reset rejected: account {UserId} is deactivated." | Abnormal | UC-07 · BR-22 · MSG14 |
| UTC003 | 1. Actor is an administrator holding AdminUsers.<br>2. Email transport cannot deliver the message. | id: active account with a password. | Return: UserResult Failure(PasswordResetEmailFailed) (MSG15); the new link is never activated, so a link sent earlier keeps working; no audit row.<br>Exception: N/A<br>Log Message: Warning log "Password reset for account {UserId} could not be emailed." | Abnormal | UC-07 · MSG15 |
| UTC004 | 1. Actor is an administrator holding AdminUsers.<br>2. While the email is being sent another administrator changes the account email from lan@x.vn to moi@x.vn. | id: that account. | Return: UserResult Success (MSG13): the email did go out, but the just-activated link is revoked because the address it went to is no longer the account's.<br>Exception: N/A<br>Log Message: Link revoked (log "Password reset link for account {UserId} revoked: ..."); audit row UserPasswordResetRequested with the old email. | Boundary | UC-07 · BR-20 · MSG13 |
| UTC005 | 1. Actor is an administrator holding AdminUsers.<br>2. While the email is being sent another administrator deactivates the account. | id: that account. | Return: UserResult Success (MSG13); the just-activated link is revoked because the account is no longer active.<br>Exception: N/A<br>Log Message: Link revoked; audit row UserPasswordResetRequested. | Boundary | UC-07 · BR-20 · MSG13 |
