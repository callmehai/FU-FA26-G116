# Role.Update — Unit (FE-03, RBAC)

Description: Role update: name validation, built-in role protection and capability set changes.

Precondition: Roles seeded, one built-in and one custom.

## RoleService.UpdateAsync

Layer: Application · `backend/TrustCabinet.Application/Access/RoleService.cs:108`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role "Thư ký phó" held by 3 accounts. | id: that role; Name: "Thư ký chính"; Description: "Phụ trách biên bản". | Return: RoleResult Success (MSG22) with the new name and description and userCount 3; capabilities and sessions are untouched.<br>Exception: N/A<br>Log Message: Audit row RoleUpdated (details { before, after }); log "Role {RoleId} updated by {ActorId}." | Normal | UC-12b · BR-16 · MSG22 |
| UTC002 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role exists. | Name: ""; Description: null. | Return: RoleResult InvalidInformation (MSG20) with errors[name] = "Tên vai trò không được để trống.".<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12b · MSG20 |
| UTC003 | 1. Actor is an administrator holding AdminUsers.<br>2. Another role "Thành viên" exists. | id: custom role; Name: "thành viên". | Return: RoleResult Failure(DuplicateName) (MSG21); the role keeps its name.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12b · MSG21 |
| UTC004 | 1. Actor is an administrator holding AdminUsers.<br>2. Built-in role "Quản trị viên". | id: that role; Name: "Quản trị cấp cao". | Return: RoleResult Failure(BuiltInRole) (MSG24); the role is unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12b · MSG24 |
| UTC005 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role exists. | Name: 64 characters. | Return: RoleResult Success (MSG22).<br>Exception: N/A<br>Log Message: Audit row RoleUpdated. | Boundary | UC-12b · MSG22 |
