# User.ResetPassword — Unit (FE-02, USER)

Description: Password reset completion: single-use expiring token, new-password validation and session revocation.

Precondition: Active and deactivated users seeded; reset email sender replaced by a recorder.

## UserService.ResetPasswordAsync

Layer: Application · `backend/TrustCabinet.Application/Users/UserService.cs:471`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Live reset link with token "T-abc" for active account lan@x.vn that has a password; every session of the account is open. | token: "T-abc"; newPassword: "MatKhauMoi@2026". | Return: UserResult Success (MSG92); the password hash is replaced, the link is spent, every refresh token of the account is revoked before and again after the save.<br>Exception: N/A<br>Log Message: Audit row UserPasswordReset (actor = the account itself, no details); log "Password of account {UserId} reset through its link." | Normal | UC-07 · BR-01 · BR-16 · BR-20 · MSG92 |
| UTC002 | No state needed. | token: null; newPassword: "MatKhauMoi@2026". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-07 · BR-20 · MSG93 |
| UTC003 | Token is dead and the new password is also too short. | token: "T-old"; newPassword: "ngan". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93): the link is checked before the password.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-07 · MSG93 |
| UTC004 | Live reset link T-abc. | token: "T-abc"; newPassword: "MatKhau@12" (11 characters). | Return: UserResult InvalidInformation (MSG94) with errors[newPassword] = "Mật khẩu phải có ít nhất 12 ký tự."; the link is not spent and can be reused for a corrected password.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-07 · BR-01 · MSG94 |
| UTC005 | Live reset link T-abc for an active account with a password. | token: "T-abc"; newPassword: "MatKhau@123!" (12 characters). | Return: UserResult Success (MSG92): 12 characters is the minimum.<br>Exception: N/A<br>Log Message: Audit row UserPasswordReset. | Boundary | UC-07 · MSG92 |
