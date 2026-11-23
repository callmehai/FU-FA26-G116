# User.ChangeStatus — Unit (FE-02, USER)

Description: Deactivate and reactivate rules: status change, last-admin guard, session and Voice ID side effects.

Precondition: At least two active administrators exist for the guard cases.

## UserService.ChangeStatusAsync

Layer: Application · `backend/TrustCabinet.Application/Users/UserService.cs:275`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Actor is an administrator holding AdminUsers.<br>2. Two active administrators exist; target is an active non-administrator with an enrolled Voice ID and Effective calendar shares. | id: target; isActive: false. | Return: UserResult Success (MSG11) with IsActive false; the row is kept; every refresh token and reset link of the account are revoked after the commit.<br>Exception: N/A<br>Log Message: Audit row UserDeactivated (details { email, isActive: false }); Voice ID ended (reason AccountDeactivated); calendar shares stopped; log "Account {UserId} deactivated by {ActorId}." | Normal | UC-06c · BR-18 · BR-19 · BR-372 · BR-424 · MSG11 |
| UTC002 | Actor is an administrator holding AdminUsers. | id: the actor's own id; isActive: false. | Return: UserResult Failure(StatusChangeNotAllowed) (MSG12); the administrator stays active.<br>Exception: N/A<br>Log Message: Log "Status change rejected: administrator {ActorId} aimed at their own account." | Abnormal | UC-06c · MSG12 |
| UTC003 | 1. Actor is an administrator holding AdminUsers.<br>2. Exactly two active administrators A and B; A has already been deactivated by B. | id: B; isActive: false (requested with A's still-valid access token). | Return: UserResult Failure(StatusChangeNotAllowed) (MSG12): the last-administrator rule blocks mutual deactivation.<br>Exception: N/A<br>Log Message: Warning log "Status change rejected: ... would leave no active administrator." | Boundary | UC-06c · MSG12 |
| UTC004 | 1. Actor is an administrator holding AdminUsers.<br>2. Target is an inactive account holding the AdminUsers role; the only active administrator is the actor. | id: target; isActive: true. | Return: UserResult Success (MSG11): reactivation is never blocked by the last-administrator rule.<br>Exception: N/A<br>Log Message: Audit row UserReactivated. | Boundary | UC-06c · MSG11 |
| UTC005 | 1. Actor is an administrator holding AdminUsers.<br>2. Ending the sessions fails (Redis unavailable) after the commit. | id: active target; isActive: false. | Return: UserResult Success (MSG11): the failure is logged, not raised.<br>Exception: N/A<br>Log Message: Error log "Account {UserId} was changed, but ending its sessions failed."; audit row UserDeactivated. | Boundary | UC-06c · BR-18 · MSG11 |
