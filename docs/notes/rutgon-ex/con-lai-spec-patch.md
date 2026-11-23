# Spec patch — nhóm con-lai (auth + user-management + role-management + notification-management + reporting)
n> **Lưu ý:** nếu mục này lệch với `con-lai-spec-patch.md` thì theo file `con-lai-ex-final.md` (sinh từ diagram sau lượt chọn cuối của BA).

Nhóm thư mục: `auth`, `user-management`, `role-management`, `notification-management`, `reporting`.
Áp dụng vào Report 3 (UC Spec) ở bước 5. Chỉ ghi EX giữ lại (số mới + MSG) và EX bỏ/gộp (số cũ + lý do).

## UC-01 – Sign in with Email and Password
- Giữ: EX-1 invalid credentials (MSG01), EX-2 deactivated account (MSG02), EX-3 no active role/access (MSG04).
- Bỏ: none. (đã ≤ 3)

## UC-02 – Sign in with Google
- Giữ: EX-1 Google authentication failed/cancelled (MSG01), EX-2 Google account not linked (MSG03), EX-3 internal account deactivated (MSG04).
- Bỏ: nhánh "User has no active role or access" (MSG05) — không phải EX trong spec UC-02; bỏ để khớp spec.

## UC-03 – Sign Out
- Giữ: none.
- Bỏ: EX-1 "JWT cannot be cleared" (hạ tầng / try again, MSG01).

## UC-04 – Access Meeting via Guest Invitation
- Chuyển sang `con-lai-ba-chon.md` (9 EX nghiệp vụ, không rút được ≤ 3). Không sửa `.txt`.

## UC-05 – View User Accounts
- Không đổi (0 EX).

## UC-06a – Create User Account
- Giữ: EX-1 invalid user information, EX-2 username/email already exists (MSG08).
- Bỏ: none.

## UC-06b – Update User Account
- Giữ: EX-1 invalid user information.
- Bỏ: none.

## UC-06c – Deactivate / Reactivate User Account
- Giữ: EX-1 status change not allowed.
- Bỏ: none.

## UC-07 – Reset User Password
- Giữ: EX-1 account no longer exists (cũ EX-3), EX-2 account deactivated or has no password (cũ EX-1, MSG14).
- Bỏ: EX-2 email cannot be sent (hạ tầng, MSG15).

## UC-08 – Assign System Role to User
- Giữ: EX-1 selected role not available (MSG09), EX-2 assignment removes last active administrator (MSG12).
- Bỏ: none.

## UC-10 – View Role Permission Matrix
- Không đổi (0 EX).

## UC-11 – Configure Role Permissions
- Giữ: EX-1 invalid permission configuration (MSG17).
- Bỏ: "Permission configuration cannot be saved" (hạ tầng, MSG18).

## UC-12a – Create Role
- Giữ: EX-1 invalid role information (MSG20), EX-2 duplicate role name (MSG21).
- Bỏ: "Role cannot be saved" (hạ tầng, MSG26).

## UC-12b – Update Role
- Giữ: EX-1 predefined role (MSG24), EX-2 invalid role information (MSG20), EX-3 duplicate role name (MSG21).
- Bỏ: nhánh "Role not found" (không phải EX trong spec), "Role cannot be saved" (hạ tầng, MSG27).

## UC-12c – Delete Role
- Giữ: EX-1 predefined role (MSG24), EX-2 role still held by user accounts (MSG25).
- Bỏ: nhánh "Role not found" (không phải EX trong spec), "Role cannot be deleted" (hạ tầng, MSG28).

## UC-13 – View Audit Log
- Giữ: EX-1 caller lacks User administration permission (403), EX-2 invalid filter parameter (MSG469).
- Bỏ: "The entries cannot be read" (hạ tầng, MSG470). Nhánh session expired (401) giữ nguyên (auth, không phải EX).

## UC-66 – View Notifications
- Giữ: none.
- Bỏ: EX-1 notifications cannot be retrieved (hạ tầng, MSG40).

## UC-67a – Mark Notification as Read
- Giữ: EX-1 notification does not exist or belongs to another user.
- Bỏ: "Read status cannot be saved" (hạ tầng, try again).

## UC-67b – Mark All Notifications as Read
- Giữ: none (AF-1 no unread notifications giữ nguyên).
- Bỏ: "Notifications cannot be updated" (hạ tầng, try again).

## UC-68 – Open Notification Target
- Giữ: EX-1 notification does not exist or belongs to another user (404), EX-2 target no longer exists or no access (404).
- Bỏ: "Read status cannot be saved" (hạ tầng, EX-3).

## UC-70a – View Reports & Statistics
- Giữ: EX-1 invalid report period (MSG463), EX-2 invalid report parameters (cũ EX-3, MSG484).
- Bỏ: "The data cannot be read" (hạ tầng, cũ EX-2, MSG464).

## UC-70b – Export Report
- Giữ: EX-1 invalid report period (MSG463), EX-2 invalid report parameters (cũ EX-3, MSG484).
- Bỏ: "The data cannot be read" (hạ tầng, cũ EX-2, MSG465) và "The export cannot be recorded" (hạ tầng, cũ EX-2, MSG465).