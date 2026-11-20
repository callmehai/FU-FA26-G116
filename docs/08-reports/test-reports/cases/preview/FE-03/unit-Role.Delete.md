# Role.Delete — Unit (FE-03, RBAC)

Description: Role deletion: refused while users hold it and for built-in roles.

Precondition: Roles seeded, one in use.

## RoleService.DeleteAsync

Layer: Application · `backend/TrustCabinet.Application/Access/RoleService.cs:184`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role "Thư ký phó" with capabilities [MeetingCreator]; no account holds it, active or deactivated. | id: that role. | Return: RoleResult Success (MSG23); the role and its capability grants are removed.<br>Exception: N/A<br>Log Message: Audit row RoleDeleted (details { name, capabilities: ["MeetingCreator"] }); log "Role {RoleId} deleted by {ActorId}." | Normal | UC-12c · BR-16 · MSG23 |
| UTC002 | Actor is an administrator holding AdminUsers. | id: <random Guid with no role>. | Return: RoleResult Failure(NotFound) (HTTP 404, no message code).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12c |
| UTC003 | 1. Actor is an administrator holding AdminUsers.<br>2. Built-in role "Thành viên". | id: that role. | Return: RoleResult Failure(BuiltInRole) (MSG24); the role stays.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12c · MSG24 |
| UTC004 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role held by 2 active accounts. | id: that role. | Return: RoleResult InUse with userCount 2 (MSG25); nothing deleted.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12c · MSG25 |
| UTC005 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role held by exactly 1 account, which is deactivated. | id: that role. | Return: RoleResult InUse with userCount 1 (MSG25): deactivated accounts still count.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-12c · MSG25 |
| UTC006 | 1. Actor is an administrator holding AdminUsers.<br>2. The role has no holder when counted, but an account is assigned to it before the delete executes (the foreign key refuses the delete). | id: that role. | Return: RoleResult InUse (MSG25) with the fresh count, at least 1; no audit row.<br>Exception: N/A<br>Log Message: Log "Deleting role {RoleId} refused by the foreign key: {Count} account(s) hold it." | Abnormal | UC-12c · MSG25 |
| UTC007 | 1. Actor is an administrator holding AdminUsers.<br>2. The database fails while deleting (connection lost or timeout). | id: unused custom role. | Return: RoleResult Failure(SaveFailed) (MSG28); the role and its grants remain.<br>Exception: N/A<br>Log Message: Error log "Deleting role {RoleId} failed." | Abnormal | UC-12c · MSG28 |
| UTC008 | 1. Actor is an administrator holding AdminUsers.<br>2. Custom role with no capabilities and no holders. | id: that role. | Return: RoleResult Success (MSG23); audit details list capabilities as an empty array.<br>Exception: N/A<br>Log Message: Audit row RoleDeleted. | Boundary | UC-12c · BR-16 · MSG23 |
