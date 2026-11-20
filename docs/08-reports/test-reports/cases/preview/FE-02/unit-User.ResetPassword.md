# User.ResetPassword — Unit (FE-02, USER)

Description: Password reset completion: single-use expiring token, new-password validation and session revocation.

Precondition: Active and deactivated users seeded; reset email sender replaced by a recorder.

## UserService.ResetPasswordAsync

Layer: Application · `backend/TrustCabinet.Application/Users/UserService.cs:471`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Live reset link with token "T-abc" for active account lan@x.vn that has a password; every session of the account is open. | token: "T-abc"; newPassword: "MatKhauMoi@2026". | Return: UserResult Success (MSG92); the password hash is replaced, the link is spent, every refresh token of the account is revoked before and again after the save.<br>Exception: N/A<br>Log Message: Audit row UserPasswordReset (actor = the account itself, no details); log "Password of account {UserId} reset through its link." | Normal | UC-07 · BR-01 · BR-16 · BR-20 · MSG92 |
| UTC002 | No state needed. | token: null; newPassword: "MatKhauMoi@2026". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-07 · BR-20 · MSG93 |
| UTC003 | No state needed. | token: "   "; newPassword: "MatKhauMoi@2026". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-07 · BR-20 · MSG93 |
| UTC004 | The token store has no live record of the token (unknown, expired after 60 minutes, spent or replaced by a newer link). | token: "T-old"; newPassword: "MatKhauMoi@2026". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93); no password changed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-07 · BR-20 · MSG93 |
| UTC005 | Token is dead and the new password is also too short. | token: "T-old"; newPassword: "ngan". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93): the link is checked before the password.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-07 · MSG93 |
| UTC006 | Live reset link T-abc. | token: "T-abc"; newPassword: "MatKhau@12" (11 characters). | Return: UserResult InvalidInformation (MSG94) with errors[newPassword] = "Mật khẩu phải có ít nhất 12 ký tự."; the link is not spent and can be reused for a corrected password.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-07 · BR-01 · MSG94 |
| UTC007 | Live reset link T-abc. | token: "T-abc"; newPassword: null. | Return: UserResult InvalidInformation (MSG94) with errors[newPassword]; link still usable.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-07 · MSG94 |
| UTC008 | Live reset link T-abc for an active account with a password. | token: "T-abc"; newPassword: "MatKhau@123!" (12 characters). | Return: UserResult Success (MSG92): 12 characters is the minimum.<br>Exception: N/A<br>Log Message: Audit row UserPasswordReset. | Boundary | UC-07 · MSG92 |
| UTC009 | The link is live when read, but a simultaneous submission spends it first. | token: "T-abc"; newPassword: "MatKhauMoi@2026". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93): the consume does not return the account id; no password changed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-07 · BR-20 · MSG93 |
| UTC010 | Link T-abc belongs to an account deactivated after the link was sent. | token: "T-abc"; newPassword: "MatKhauMoi@2026". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93); the link is spent and the password is unchanged.<br>Exception: N/A<br>Log Message: Log "Password reset refused: account {UserId} is gone, deactivated or has no password." | Abnormal | UC-07 · BR-03 · BR-22 · MSG93 |
| UTC011 | Link T-abc belongs to an account that no longer exists. | token: "T-abc"; newPassword: "MatKhauMoi@2026". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93).<br>Exception: N/A<br>Log Message: Log "Password reset refused: account {UserId} is gone, deactivated or has no password." | Abnormal | UC-07 · MSG93 |
| UTC012 | Link T-abc belongs to a Google-only account (no password hash). | token: "T-abc"; newPassword: "MatKhauMoi@2026". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93): a reset does not add password sign-in.<br>Exception: N/A<br>Log Message: Log "Password reset refused: ..." | Abnormal | UC-07 · BR-22 · MSG93 |
| UTC013 | Live link; the second session revoke (after the save) fails. | token: "T-abc"; newPassword: "MatKhauMoi@2026". | Return: UserResult Success (MSG92): the new password is saved; the failure is logged, not raised.<br>Exception: N/A<br>Log Message: Error log "Password of account {UserId} was reset, but the second session revoke failed..."; audit row UserPasswordReset. | Boundary | UC-07 · MSG92 |
| UTC014 | The same link was used successfully a moment ago. | token: "T-abc"; newPassword: "KhacNua@2026!!". | Return: UserResult Failure(PasswordResetLinkInvalid) (MSG93): a link cannot be reused after a successful reset.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-07 · BR-20 · MSG93 |
