# Role.Create — Unit (FE-03, RBAC)

Description: Custom role creation: name and capability validation, duplicate guard, built-in protection.

Precondition: Capability catalogue available; no role with the test name exists.

## RoleService.CreateAsync

Layer: Application · `backend/TrustCabinet.Application/Access/RoleService.cs:38`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Actor is an administrator holding AdminUsers.<br>2. No role is named "Thư ký phó". | Name: "Thư ký phó"; Description: "Hỗ trợ thư ký"; Capabilities: ["MeetingCreator", "VoiceCollector"]. | Return: RoleResult Success (MSG19): custom role (not built-in) with 0 holders and capabilities MeetingCreator and VoiceCollector.<br>Exception: N/A<br>Log Message: Role and its capabilities saved in one transaction; audit row RoleCreated (details { name, description, capabilities }); log "Role {RoleId} created by {ActorId}." | Normal | UC-12a · BR-16 · MSG19 |
| UTC002 | Actor is an administrator holding AdminUsers. | Name: ""; Description: null; Capabilities: []. | Return: RoleResult InvalidInformation (MSG20) with errors[name] = "Tên vai trò không được để trống."; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12a · MSG20 |
| UTC003 | 1. Actor is an administrator holding AdminUsers.<br>2. A custom role "Thư ký phó" exists. | Name: "THƯ KÝ PHÓ". | Return: RoleResult Failure(DuplicateName) (MSG21): names are unique regardless of case; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-12a · MSG21 |
| UTC004 | Actor is an administrator holding AdminUsers. | Name: 64 characters. | Return: RoleResult Success (MSG19): 64 characters is the maximum.<br>Exception: N/A<br>Log Message: Role saved; audit row RoleCreated. | Boundary | UC-12a · MSG19 |
| UTC005 | Actor is an administrator holding AdminUsers. | Name: 65 characters. | Return: RoleResult InvalidInformation (MSG20) with errors[name] = "Tên vai trò không được vượt quá 64 ký tự.".<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-12a · MSG20 |
