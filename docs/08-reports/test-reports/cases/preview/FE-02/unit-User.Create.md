# User.Create — Unit (FE-02, USER)

Description: User creation rules: valid input creates an active account; validation, duplicate email and unknown role are refused.

Precondition: Admin actor with user-management capability; roles seeded.

## UserService.CreateAsync

Layer: Application · `backend/TrustCabinet.Application/Users/UserService.cs:61`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Actor is an administrator holding AdminUsers; roles "Quản trị viên" and "Thành viên" exist.<br>2. No account uses the email thuky@trustcabinet.local. | FullName: "Nguyễn Thư Ký"; Email: "thuky@trustcabinet.local"; Password: "MatKhau@2026!"; RoleId: role "Thành viên". | Return: UserResult Success (MSG07) with the new account: active, role "Thành viên"; the password is stored only as a hash.<br>Exception: N/A<br>Log Message: Account row added; audit row UserCreated (actor = admin, details { email, fullName, roleId, role } and no password); log "Account {UserId} created by {ActorId}." | Normal | UC-06a · BR-01 · BR-13 · BR-14 · BR-15 · BR-16 · MSG07 |
| UTC002 | 1. Actor is an administrator holding AdminUsers; roles "Quản trị viên" and "Thành viên" exist.<br>2. No account uses the email thuky@trustcabinet.local. | FullName: "  Nguyễn Thư Ký  "; Email: "  thuky@trustcabinet.local  "; Password: "MatKhau@2026!"; RoleId: valid. | Return: UserResult Success (MSG07); name and email are saved trimmed.<br>Exception: N/A<br>Log Message: Account row added; audit row UserCreated. | Boundary | UC-06a · BR-14 · MSG07 |
| UTC003 | Actor is an administrator holding AdminUsers; roles "Quản trị viên" and "Thành viên" exist. | FullName: ""; Email: "thuky@trustcabinet.local"; Password: "MatKhau@2026!"; RoleId: valid. | Return: UserResult InvalidInformation (MSG09) with errors[fullName] = "Họ tên không được để trống."; nothing is saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-06a · BR-14 · MSG09 |
| UTC004 | Actor is an administrator holding AdminUsers; roles "Quản trị viên" and "Thành viên" exist. | FullName: 200 characters; Email valid; Password valid; RoleId valid. | Return: UserResult Success (MSG07): 200 characters is within the limit.<br>Exception: N/A<br>Log Message: Account row added; audit row UserCreated. | Boundary | UC-06a · BR-14 · MSG07 |
| UTC005 | Actor is an administrator holding AdminUsers; roles "Quản trị viên" and "Thành viên" exist. | FullName: 201 characters; Email valid; Password valid; RoleId valid. | Return: UserResult InvalidInformation (MSG09) with errors[fullName] = "Họ tên không được vượt quá 200 ký tự.".<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-06a · BR-14 · MSG09 |
| UTC006 | 1. Actor is an administrator holding AdminUsers; roles "Quản trị viên" and "Thành viên" exist.<br>2. An account with email thuky@trustcabinet.local already exists. | Email: "THUKY@trustcabinet.local" (different case); other fields valid. | Return: UserResult Failure(DuplicateEmail) (MSG08): the email is unique regardless of case; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-06a · BR-13 · MSG08 |
