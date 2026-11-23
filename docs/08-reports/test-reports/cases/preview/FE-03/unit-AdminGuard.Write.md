# AdminGuard.Write — Unit (FE-03, RBAC)

Description: Administrator invariant: a write that would leave no active administrator is refused.

Precondition: At least one administrator seeded.

## AdministratorInvariantGuard.WriteAsync

Layer: Application · `backend/TrustCabinet.Application/Access/AdministratorInvariantGuard.cs:12`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | No other administrative write is running. | write: a function returning "ok". | Return: Returns "ok" after the administrator lock was taken and the write ran inside one Read Committed transaction that is then committed.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-11 · UC-06c · UC-08 |
| UTC002 | Another guarded write (for example deactivating the last administrator) holds the lock. | write: removing AdminUsers from a role. | Return: The call waits for the first write to commit, then runs against the committed state, so two legal-looking changes cannot combine into a system with no administrator.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-11 · UC-06c |
| UTC003 | The write returns a refusal result (for example MSG12 or MSG17). | write: a function returning a failure result without saving. | Return: The refusal is returned unchanged to the caller; nothing was written, so the commit leaves the database as it was.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-06c · UC-11 · MSG12 · MSG17 |
| UTC004 | No other write is running. | write: a function that throws after changing rows. | Return: The exception reaches the caller and the transaction is rolled back, so no partial change stays and the lock is released.<br>Exception: The thrown exception propagates (translated when it is a persistence error).<br>Log Message: N/A | Abnormal | UC-11 |
| UTC005 | The caller already runs inside a guarded transaction (for example creating a role inside an administrator write). | write: a function returning a value. | Return: The write runs inside the existing transaction and is not committed early; the outer call commits it. Taking the lock again is harmless.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-12a |
