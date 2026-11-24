# RolePermission.Update — Unit (FE-03, RBAC)

Description: Capability matrix update: grant/revoke capabilities with the lock-out guard.

Precondition: Roles seeded; admin actor holds role management capability.

## RolePermissionService.UpdateAsync

Layer: Application · `backend/TrustCabinet.Application/Access/RolePermissionService.cs:33`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Actor is an administrator holding AdminUsers.<br>2. Roles: "Quản trị viên" [AdminUsers], "Thư ký" [MeetingCreator], 2 active accounts hold "Thư ký". | roleId: "Thư ký"; capabilities: [MeetingCreator, VoiceCollector]. | Return: RolePermissionResult Success (MSG16) with the matrix showing "Thư ký" = [MeetingCreator, VoiceCollector]; the 2 active holders are told their capabilities changed, their sessions stay.<br>Exception: N/A<br>Log Message: Capabilities replaced; audit row RolePermissionsUpdated (details { name, before, after }); log "Permissions for role {RoleId} changed by {ActorId}; 2 account(s) to notify."; capabilities-changed notice sent after commit | Normal | UC-11 · BR-16 · BR-23 · BR-313 · MSG16 |
| UTC002 | Actor is an administrator holding AdminUsers. | roleId: <random Guid with no role>; capabilities: [MeetingCreator]. | Return: RolePermissionResult InvalidConfiguration (MSG17) with errors[roleId] = "Vai trò không hợp lệ."; nothing saved.<br>Exception: N/A<br>Log Message: Warning log "Permission configuration for role {RoleId} rejected: ...". | Abnormal | UC-11 · BR-23 · MSG17 |
| UTC003 | 1. Actor is an administrator holding AdminUsers.<br>2. Role "Thư ký" exists. | roleId: "Thư ký"; capabilities: [(Capability)999] (a value outside the catalogue). | Return: RolePermissionResult InvalidConfiguration (MSG17) with errors[capabilities] = "Danh sách quyền chứa giá trị không hợp lệ."; nothing saved.<br>Exception: N/A<br>Log Message: Warning log "Permission configuration ... rejected". | Abnormal | UC-11 · BR-23 · MSG17 |
| UTC004 | 1. Actor is an administrator holding AdminUsers.<br>2. "Quản trị viên" is the only role holding AdminUsers and its only holder is the actor. | roleId: "Quản trị viên"; capabilities: []. | Return: RolePermissionResult InvalidConfiguration (MSG17) with errors[adminUsers] = "Không thể lưu cấu hình này: sau khi lưu sẽ không còn tài khoản đang hoạt động nào giữ quyền quản trị người dùng, nên không ai vào được màn hình phân quyền nữa."; the matrix is unchanged.<br>Exception: N/A<br>Log Message: Warning log ... | Abnormal | UC-11 · BR-23 · MSG17 |
| UTC005 | 1. Actor is an administrator holding AdminUsers.<br>2. AdminUsers is held by "Quản trị viên" and by "Thư ký", which has 1 active holder. | roleId: "Quản trị viên"; capabilities: [] (removes AdminUsers). | Return: RolePermissionResult Success (MSG16): "Thư ký" keeps an active account with AdminUsers, so someone can still administer.<br>Exception: N/A<br>Log Message: Audit row RolePermissionsUpdated; holders of "Quản trị viên" notified. | Boundary | UC-11 · BR-23 · MSG16 |
| UTC006 | 1. Actor is an administrator holding AdminUsers.<br>2. The change is saved but telling the role's accounts fails (hub unavailable). | roleId: "Thư ký"; capabilities: [MeetingCreator, VoiceCollector]. | Return: RolePermissionResult Success (MSG16): the failure is logged, not raised; accounts pick the change up at their next token refresh.<br>Exception: N/A<br>Log Message: Error log "Permissions for role {RoleId} changed, but telling its accounts failed." | Boundary | UC-11 · BR-313 · MSG16 |
