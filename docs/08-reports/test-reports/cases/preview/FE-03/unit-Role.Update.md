# Role.Update — Unit (FE-03, RBAC)

Description: Role update: name validation, built-in role protection and capability set changes.

Precondition: Roles seeded, one built-in and one custom.

## RoleService.UpdateAsync

Layer: Application · `backend/TrustCabinet.Application/Access/RoleService.cs:108`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role "Thư ký phó" held by 3 accounts. | id: that role; Name: "Thư ký chính"; Description: "Phụ trách biên bản". | Return: RoleResult Success (MSG22) with the new name and description and userCount 3; capabilities and sessions are untouched.<br>Exception: N/A<br>Log Message: Audit row RoleUpdated (details { before, after }); log "Role {RoleId} updated by {ActorId}." | Normal | UC-12b · BR-16 · MSG22 |
| UTC002 | Actor is an administrator holding AdminUsers. | id: <random Guid with no role>; valid request. | Return: RoleResult Failure(NotFound) (HTTP 404, no message code).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12b |
| UTC003 | 1. Actor is an administrator holding AdminUsers.<br>2. Built-in role "Quản trị viên". | id: that role; Name: "Quản trị cấp cao". | Return: RoleResult Failure(BuiltInRole) (MSG24); the role is unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12b · MSG24 |
| UTC004 | 1. Actor is an administrator holding AdminUsers.<br>2. Built-in role and a blank name submitted. | id: built-in role; Name: "". | Return: RoleResult Failure(BuiltInRole) (MSG24): the built-in check comes before field validation.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-12b · MSG24 |
| UTC005 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role exists. | Name: ""; Description: null. | Return: RoleResult InvalidInformation (MSG20) with errors[name] = "Tên vai trò không được để trống.".<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12b · MSG20 |
| UTC006 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role exists. | Name: 65 characters. | Return: RoleResult InvalidInformation (MSG20) with errors[name] = "Tên vai trò không được vượt quá 64 ký tự.".<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-12b · MSG20 |
| UTC007 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role exists. | Name: 64 characters. | Return: RoleResult Success (MSG22).<br>Exception: N/A<br>Log Message: Audit row RoleUpdated. | Boundary | UC-12b · MSG22 |
| UTC008 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role exists. | Name valid; Description: 257 characters. | Return: RoleResult InvalidInformation (MSG20) with errors[description] = "Mô tả không được vượt quá 256 ký tự.".<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-12b · MSG20 |
| UTC009 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role "Thư ký phó" with description "Hỗ trợ". | Name: "Thư ký phó"; Description: "Hỗ trợ" (identical). | Return: RoleResult Success (MSG22) with the current data; nothing is written and no audit row is added.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-12b · MSG22 |
| UTC010 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role "Thư ký phó". | Name: "THƯ KÝ PHÓ" (only the letter case changes). | Return: RoleResult Success (MSG22): re-casing the role's own name is a change, not a duplicate.<br>Exception: N/A<br>Log Message: Audit row RoleUpdated. | Boundary | UC-12b · MSG22 |
| UTC011 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role "Thư ký phó" with description "Hỗ trợ". | Name: "Thư ký phó"; Description: "   " (blank). | Return: RoleResult Success (MSG22); the description is cleared to null and the change is audited.<br>Exception: N/A<br>Log Message: Audit row RoleUpdated. | Boundary | UC-12b · MSG22 |
| UTC012 | 1. Actor is an administrator holding AdminUsers.<br>2. Another role "Thành viên" exists. | id: custom role; Name: "thành viên". | Return: RoleResult Failure(DuplicateName) (MSG21); the role keeps its name.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12b · MSG21 |
| UTC013 | 1. Actor is an administrator holding AdminUsers.<br>2. The name is free when checked, but a concurrent request takes it before the save. | Valid rename. | Return: RoleResult Failure(DuplicateName) (MSG21); no audit row.<br>Exception: N/A<br>Log Message: Log "Role update rejected: name already taken (unique index)." | Abnormal | UC-12b · MSG21 |
| UTC014 | 1. Actor is an administrator holding AdminUsers.<br>2. The role is deleted by another administrator between reading and saving. | Valid rename. | Return: RoleResult Failure(NotFound) (HTTP 404): the save matches no row; the client refreshes its list.<br>Exception: N/A<br>Log Message: Log "Role update rejected: the role no longer exists." | Abnormal | UC-12b |
| UTC015 | 1. Actor is an administrator holding AdminUsers.<br>2. The database refuses the save (connection lost or timeout). | Valid rename. | Return: RoleResult Failure(SaveFailed) (MSG27); the role keeps its previous data.<br>Exception: N/A<br>Log Message: Error log "Role update failed to save." | Abnormal | UC-12b · MSG27 |
