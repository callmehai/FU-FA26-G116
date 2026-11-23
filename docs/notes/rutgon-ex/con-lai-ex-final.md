# EX cuối cùng — nhóm con-lai

Sinh tự động từ các `.txt` sequence (script `scripts/rutgon_ex_final.py`). Đây là bản chuẩn: UC Spec phải khớp.

## UC02_SignInWithGoogle
- Không có EX.

## UC03_SignOut
- Không có EX.

## UC04_AccessMeetingViaGuestInvitation
- EX-1 (MSG371): Invitation is missing or revoked, or the Meeting is no longer open
- EX-2 (MSG379): Code requested too soon or too many codes in 24 hours
- EX-3 (MSG377): Display information is invalid or already declared with different values

## UC06a_CreateUserAccount
- EX-1 (không MSG): Invalid user information
- EX-2 (MSG08): Username or Email already exists

## UC06b_UpdateUserAccount
- EX-1 (không MSG): Invalid user information

## UC06c_DeactivateReactivateUserAccount
- EX-1 (không MSG): Status change is not allowed

## UC07_ResetUserPassword
- EX-1 (không MSG): User account no longer exists
- EX-2 (MSG14): Account is deactivated or has no password

## UC08_AssignSystemRoleToUser
- EX-1 (MSG09): Selected role is not available
- EX-2 (MSG12): Assignment would remove the last active administrator

## UC11_ConfigureRolePermissions
- EX-1 (MSG17): Invalid permission configuration

## UC12a_CreateRole
- EX-1 (MSG20): Invalid role information
- EX-2 (MSG21): Role name already exists

## UC12b_UpdateRole
- EX-1 (MSG24): Role is a predefined role
- EX-2 (MSG20): Invalid role information
- EX-3 (MSG21): Role name already exists

## UC12c_DeleteRole
- EX-1 (MSG24): Role is a predefined role
- EX-2 (MSG25): Role is still held by user accounts

## UC13_ViewAuditLog
- EX-1 (không MSG): The caller's role does not hold the User administration permission
- EX-2 (MSG469): A parameter is unknown, repeated or out of range

## UC66_ViewNotifications
- Không có EX.

## UC67a_MarkNotificationAsRead
- EX-1 (không MSG): Notification does not exist or belongs to another user

## UC67b_MarkAllNotificationsAsRead
- Không có EX.

## UC68_OpenNotificationTarget
- EX-1 (không MSG): Notification does not exist or belongs to another user
- EX-2 (không MSG): Target no longer exists or user no longer has access

## UC70a_ViewReportsAndStatistics
- EX-1 (MSG463): The period is not valid
- EX-2 (MSG484): The query parameter is not valid

## UC70b_ExportReport
- EX-1 (MSG463): The period is not valid
- EX-2 (MSG484): The query parameter is not valid
