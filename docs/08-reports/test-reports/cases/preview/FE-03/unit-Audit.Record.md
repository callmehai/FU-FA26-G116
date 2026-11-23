# Audit.Record — Unit (FE-03, RBAC)

Description: Audit entry recording: caller context, action catalogue and details.

Precondition: Caller context available.

## AuditService.RecordAsync

Layer: Application · `backend/TrustCabinet.Application/Audit/AuditService.cs:34`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Caller context reports IP address 203.0.113.7. | action: "user.created"; objectType: "User"; objectId: U1; actorId: A1; details: { email: "lan@x.vn", fullName: "Lê Văn Lan" }. | Return: Completes; one audit row is added with the actor A1, the action, object type and id, IP 203.0.113.7 and details JSON {"email":"lan@x.vn","fullName":"Lê Văn Lan"}, and saved at once in its own unit of work.<br>Exception: N/A<br>Log Message: Audit row written. | Normal | BR-16 · BR-444 |
| UTC002 | Caller context available. | details: null. | Return: Completes; the row is saved with details "{}" (the column is NOT NULL, so nothing to add is an empty object).<br>Exception: N/A<br>Log Message: Audit row written with empty details. | Boundary | BR-16 |
| UTC003 | Caller context available. | details: { name: "Thư ký phó" } containing Vietnamese letters. | Return: Details JSON is stored with the letters as themselves ("Thư ký phó"), not as \u escapes.<br>Exception: N/A<br>Log Message: Audit row written. | Boundary | BR-16 |
| UTC004 | A Guest verifies an invitation (no account acts). | actorId: null; objectType: "GuestSession"; objectId: S1; details: { meetingId, guestInvitationId }. | Return: Completes; the row is saved with a null actor and the invitation in the details.<br>Exception: N/A<br>Log Message: Audit row written without actor. | Boundary | BR-444 |
| UTC005 | The audited action has no single object. | objectId: null; actorId: A1. | Return: Completes; the row is saved with a null object id.<br>Exception: N/A<br>Log Message: Audit row written. | Boundary | BR-16 |
| UTC006 | The database refuses the save. | Valid arguments. | Return: The persistence error reaches the caller; no row is stored.<br>Exception: Persistence exception from saving the audit row.<br>Log Message: N/A | Abnormal | BR-16 |
