# User.Update — Unit (FE-02, USER)

Description: User update rules: field validation, role change and last-administrator protection.

Precondition: Admin actor with user-management capability; target user and roles seeded.

## UserService.UpdateAsync

Layer: Application · `backend/TrustCabinet.Application/Users/UserService.cs:153`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Actor is an administrator holding AdminUsers.<br>2. Active account "Lê Văn A" <a@x.vn> with role "Thành viên". | id: that account; FullName: "Lê Văn Anh"; Email: "a@x.vn" (unchanged); RoleId: "Thành viên" (unchanged). | Return: UserResult Success (MSG10) with FullName "Lê Văn Anh"; no session is ended and nobody is notified.<br>Exception: N/A<br>Log Message: Audit row UserUpdated (details { before, after }); log "Account {UserId} updated by {ActorId}." | Normal | UC-06b · BR-009 · MSG10 |
| UTC002 | 1. Actor is an administrator holding AdminUsers.<br>2. Account exists. | FullName: ""; Email: "a@x.vn"; RoleId valid. | Return: UserResult InvalidInformation (MSG09) with errors[fullName] = "Họ tên không được để trống."; the account is unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-06b · UC-06a, UC-06b · MSG09 |
| UTC003 | 1. Actor is an administrator holding AdminUsers.<br>2. Account exists. | FullName: 201 characters; other fields valid. | Return: UserResult InvalidInformation (MSG09) with errors[fullName] = "Họ tên không được vượt quá 200 ký tự.".<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-06b · UC-06a, UC-06b · MSG09 |
| UTC004 | 1. Actor is an administrator holding AdminUsers.<br>2. Another account already uses b@x.vn. | id: account a@x.vn; Email: "b@x.vn". | Return: UserResult Failure(DuplicateEmail) (MSG08); the account is unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-06b · BR-005 · MSG08 |
| UTC005 | 1. Actor is an administrator holding AdminUsers.<br>2. Account is the only active holder of AdminUsers (role "Quản trị viên").<br>3. Target role "Thành viên" does not hold AdminUsers. | RoleId: "Thành viên". | Return: UserResult Failure(StatusChangeNotAllowed) (MSG12); the account keeps its role.<br>Exception: N/A<br>Log Message: Warning log "Update rejected: changing the role of {UserId} would leave no active administrator." | Abnormal | UC-08 · MSG12 |
