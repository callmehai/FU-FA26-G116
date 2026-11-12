# 39 — SCRUM-142: sequence còn thiếu cho chia sẻ lịch

**Ngày:** 08/11/2026 · **Ticket:** SCRUM-142 (chia sẻ lịch), liên quan SCRUM-137 · **Trạng thái:** đã làm S1–S6 và xuất ảnh/validate; PO đã chốt Q-A→Q-E 09/11/2026 (Q4 không còn), đã sửa theo trong PR #295; còn Report 3 DOCX và comment Jira

Nguồn: góp ý review gửi qua nhóm, BE đối chiếu lại với repo (nhánh `docs/ba-final-review-note37`, `47fb138e`).
Phần SCRUM-142 mới là đặc tả sơ bộ, **chưa có code**, nên sửa tài liệu lúc này là rẻ nhất.

Đọc trước khi sửa:

- [Đặc tả sơ bộ SCRUM-137/142](../NOTE-dac-ta-so-bo-scrum-137-142.md): §5 bảng UC bị ảnh hưởng (dòng 256–258), §6 API dự kiến, §7 Q4
- [ADR-0058](../../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md)
- [Convention sequence](../../06-sequencediagram/README.md), [calendar-sharing/README.md](../../06-sequencediagram/calendar-sharing/README.md)
- Report 3: UC-06c bước 10 + POST-5, UC-08 bước 10 + POST-5, UC-11 bước 7 + POST-4, BR-424 → BR-426

## 1. Kết quả đối chiếu

| # | Góp ý | Kết luận | Mức |
|---|---|---|---|
| 1 | Sequence UC-06c, UC-08, UC-11 chưa có bước dừng chia sẻ lịch | **Đúng.** Report 3 đã có bước và POST, ba file `.txt` không có chữ "share" hay "BR-424" nào. Tài liệu tự mâu thuẫn. | Cao |
| 2 | Không có sequence cho job hết hạn | **Đúng.** Job chỉ được nhắc ở §6 và Q4 của đặc tả sơ bộ. Job là **tác nhân hệ thống, không phải UC**, nên không đánh số UC mới; vẫn cần một sequence vì job ghi audit và báo hai bên (BR-425, BR-426). | Trung bình |
| 3 | Không có sequence cho `GET /api/calendar/shares` | **Đúng.** Endpoint gắn với UC-84 gom, mà UC gom không có spec hay sequence. Màn danh sách hai chiều kèm trạng thái chưa được mô tả ở đâu. | Trung bình |
| 4 | Không có danh sách người để chọn | **Đúng, lỗ hổng lớn nhất.** UC-84a/84b bắt chọn người nhưng §6 không có endpoint trả danh sách đó. | Cao |
| 5 | *(phát hiện thêm)* BR-424 "either account is locked" | Report 3 định nghĩa *Locked* là khoá 15 phút sau 5 lần sai mật khẩu. Đọc đúng chữ thì gõ sai mật khẩu 5 lần là dừng vĩnh viễn mọi chia sẻ của người khác. Cần PO chốt (§7). | Cao |
| 6 | *(phát hiện thêm)* Tiêu đề `UC11_ConfigureRolePermissions.txt` | Dòng 1 ghi `title UC-10 - Configure Role Permissions`, đúng ra là UC-11. | Thấp |

## 2. Việc S1 — UC-06c: thêm bước dừng chia sẻ

File: [UC06c_DeactivateReactivateUserAccount.txt](../../06-sequencediagram/user-management/UC06c_DeactivateReactivateUserAccount.txt)

- Thêm lifeline sau `:User Repository`:
  `participant ":Calendar Share Repository" as ShareRepository` và `control ":Notification Service" as NotificationService`
  (audit đã có `:Audit Service`).
- Chèn **trong nhánh `alt Deactivate Account`**, sau khối audit vô hiệu hoá (dòng 37–41) và **trước**
  `UserService-->UserController` (dòng 42). (Sequence này cũng chưa có bước kết thúc Voice ID ở bước 8 của Report 3,
  BR-372. Không thuộc SCRUM-142, nhưng nên sửa luôn trong lần này.) Đúng thứ tự Report 3: bước 9 audit, bước 10 dừng chia sẻ.
- Nhánh `else Reactivate Account` **không** thêm gì, nhưng thêm một `note` ngắn: mở lại tài khoản không làm chia sẻ chạy lại (BR-424).

Khối gợi ý:

```text
        UserService->ShareRepository: Stop every Effective share where the account\nis the owner or the viewer (BR-424)
        activate ShareRepository
        ShareRepository->Database: Set those shares to Stopped
        Database-->ShareRepository: Stopped shares
        ShareRepository-->UserService: Stopped shares
        deactivate ShareRepository
        opt At least one share was stopped
            UserService->AuditService: Record each stopped share (BR-426)
            activate AuditService
            AuditService->Database: Store audit records
            Database-->AuditService: Audit records created
            AuditService-->UserService: Stops recorded
            deactivate AuditService
            UserService->NotificationService: Notify both sides of each stopped share (BR-426)
        end
```

Cần ghi rõ trong diagram: dừng chia sẻ nằm **cùng giao dịch** với đổi trạng thái tài khoản (giống Voice ID ở bước 8),
nếu không thì sẽ có lúc tài khoản đã Inactive mà chia sẻ vẫn Effective. Nếu BE muốn tách giao dịch thì phải ghi vào ticket.

**Pending cũng dừng?** Ban đầu khối trên làm theo cách hiểu Pending cũng dừng (khớp UC-84c bước 2 cũ); PO 09/11/2026 Q-C đã chốt
ngược lại: chỉ **Effective** dừng, Pending giữ lại (xem UC-84c EX-5/`MSG468`). Khối trên đã sửa thành Effective.

## 3. Việc S2 — UC-08: thêm bước dừng chia sẻ

File: [UC08_AssignSystemRoleToUser.txt](../../06-sequencediagram/user-management/UC08_AssignSystemRoleToUser.txt)

- Thêm lifeline `:Calendar Share Repository`, `:Notification Service`. File này ghi audit qua `:Audit Log Repository`, nên giữ cách đó.
- Chèn trong nhánh `else Assignment is permitted`, **sau** dòng 85 (`Notify the user that their access changed ... BR-314`)
  và **trước** `UserService-->UserController` (dòng 87), đúng thứ tự bước 9 → 10 của Report 3.
- Bọc trong `opt The new role no longer grants the meeting-creation right (MeetingCreator)`. Bên trong:
  dừng các chia sẻ **Đang hiệu lực** mà user này **là bên xem** (chỉ bên xem, không phải chủ lịch: chủ lịch không cần quyền tạo họp, BR-430),
  ghi audit cho từng chia sẻ, báo hai bên.
- Thêm `note`: có lại quyền thì chia sẻ không chạy lại (BR-424); mất quyền thì ẩn màn Chia sẻ lịch (PO Q-E).

## 4. Việc S3 — UC-11: thêm bước dừng chia sẻ, sửa tiêu đề

File: [UC11_ConfigureRolePermissions.txt](../../06-sequencediagram/role-management/UC11_ConfigureRolePermissions.txt)

- Dòng 1: `title UC-10 - ...` → `title UC-11 - Configure Role Permissions`.
- Thêm lifeline `:Calendar Share Repository`, `:Notification Service`.
- Chèn trong nhánh `else Permission configuration saved`, sau dòng 41 (`Notify users currently holding the role ... BR-313`),
  trước dòng 42. Bọc trong `opt The change removes the meeting-creation right from the role`. Bên trong: tìm mọi
  tài khoản đang giữ vai trò này **và không còn quyền tạo họp từ vai trò khác** (nếu một người có nhiều vai trò; nếu
  mỗi người chỉ có một vai trò thì bỏ vế này đi), dừng các chia sẻ **Đang hiệu lực** họ đang xem, audit, báo hai bên; mất quyền thì ẩn màn Chia sẻ lịch (PO Q-E).
- Xem lại `deactivate Database`: hiện `Database` được activate ở dòng 26 bên trong khối lưu. Khối mới phải nằm trước `deactivate Database` (dòng 47).

## 5. Việc S4 — sequence job hết hạn (tác nhân hệ thống)

> **Đã bỏ theo PO 09/11/2026 (Q-B):** không có job chạy đêm, file `SYS_ExpireCalendarShares.txt/.png`, tab drawio và ảnh `drawio-png/` đã xoá trong PR #295.
> Hết hạn chỉ là điều kiện loại ra lúc đọc (BR-425), không thêm trạng thái `Expired`. Phần đề xuất dưới đây giữ lại để đối chiếu lịch sử, không còn hiệu lực.

**Không phải UC**, không có màn hình, không đánh số UC mới. Đề xuất:

- File mới: `docs/06-sequencediagram/calendar-sharing/SYS_ExpireCalendarShares.txt`, tiêu đề
  `title System - Expire Calendar Shares (BR-425)`.
- Actor: `actor "System Scheduler" as Scheduler`. Đây là ngoại lệ có chủ đích so với §2.1 của convention (không có
  người dùng, không có Boundary). Ghi ngoại lệ này vào README của `calendar-sharing/` và `06-sequencediagram/`.
- Lifeline: `:Calendar Share Expiry Job` (control), `:Calendar Share Repository`, `:Audit Service`, `:Notification Service`, `:Database`.

Luồng gợi ý:

```text
Scheduler->ExpiryJob: Run daily at 00:00 server time (§7 Q4)
ExpiryJob->ShareRepository: Find Effective shares whose end date is before today (BR-425)
ShareRepository->Database: Retrieve expiring shares
Database-->ShareRepository: Expiring shares
ShareRepository-->ExpiryJob: Expiring shares
alt No share expires
    ExpiryJob-->Scheduler: Nothing to do
else Shares expire
    loop Each expiring share
        ExpiryJob->ShareRepository: Set status to Expired
        ExpiryJob->AuditService: Record expiry (BR-426)
        ExpiryJob->NotificationService: Notify both sides (BR-426)
    end
    ExpiryJob-->Scheduler: Shares expired
end
```

Cần nêu trong diagram:

- Mỗi chia sẻ được xử lý **độc lập**, lỗi ở một chia sẻ không chặn những chia sẻ khác; lần chạy sau thử lại.
- Chạy lại nhiều lần **không gửi thông báo trùng** (`DeduplicationKey`, §6 của đặc tả sơ bộ).
- Không phụ thuộc job để chặn việc xem: UC-85a/85c phải tự loại các chia sẻ có `end_date < today` ngay lúc đọc. Nếu
  không, giữa nửa đêm và lúc job chạy sẽ có khoảng hở làm lộ giờ bận. Thêm một dòng về điều này vào sequence UC85a/UC85c
  (hiện hai file chỉ lọc theo `status`).
- Pending quá ngày kết thúc và *thay đổi đang chờ* (UC-84d) quá hạn xử lý thế nào: xem Q-B.

Report 3: BR-425 đã có. Không thêm UC; chỉ cập nhật cột UC của BR-425 nếu nhóm muốn trỏ tới sequence hệ thống (không bắt buộc).

## 6. Việc S5 — danh sách chia sẻ `GET /api/calendar/shares`

Đã làm file `calendar-sharing/UC84f_ViewCalendarShares.txt` (đổi từ `UC84_ViewCalendarShares.txt` theo PO 09/11/2026 Q-D).

- Actor: `User` có `MeetingCreator` (mất quyền thì ẩn màn, PO 09/11/2026 Q-E). Đã sửa: không còn xem chỉ-đọc.
- Boundary: `Calendar Sharing Page` (màn *Chia sẻ lịch*).
- Luồng: mở trang → lấy **hai danh sách** (tôi chia sẻ cho ai / ai chia sẻ cho tôi) → **server chỉ trả chia sẻ mà
  actor là một trong hai bên** (BR-423, AGENTS: list endpoint lọc ở server) → hiện trạng thái Chờ đồng ý / Đang hiệu lực /
  Đã dừng (`MSG439`) / đã đóng. Chia sẻ quá ngày kết thúc bị loại lúc đọc (BR-425), không hiện `Expired` (`MSG440` ngừng dùng).
- Nhánh: danh sách rỗng; đọc lỗi → `MSG453` (giống UC-85c EX-1); mất `MeetingCreator` → `403 MSG448` (EX-2, ẩn màn).

Q-D đã chốt 09/11/2026: danh sách chia sẻ **tách thành UC con** UC-84f dưới UC-84 (giống UC-32a dưới UC-32).
File đổi tên thành `UC84f_ViewCalendarShares.txt`, tiêu đề `UC-84f - View Calendar Shares`.

## 7. Việc S6 — danh sách người để chọn

Hiện trạng:

- UC-84a bước 1: chọn chủ lịch (*mọi tài khoản nội bộ đang hoạt động, trừ chính mình*); EX-4 "refreshes the list".
- UC-84b bước 1: chọn người nhận (*phải là Meeting Organizer*).
- Endpoint có sẵn `GET /api/meetings/participant-options` ([09-thanh-phan-tham-du.md](../../03-api/09-thanh-phan-tham-du.md)):
  gác `MeetingCreator`, user active, chỉ trả `id/fullName/email`, có `q` và phân trang. **Không dùng lại được cho cả hai UC**:
  - UC-84b: chủ lịch có thể không có `MeetingCreator`, nên sẽ nhận `403`;
  - không lọc được "chỉ người có `MeetingCreator`" cho UC-84b, cũng không loại chính mình;
  - lỗi của nó là `MSG95` (danh sách thành phần cuộc họp), sai tình huống (một mã = một tình huống).
- `GET /api/users` cần quyền quản trị, không dùng được.

Đề xuất endpoint mới, thêm vào §6 của đặc tả sơ bộ:

| Method | Đường dẫn | Ghi chú |
|---|---|---|
| `GET` | `/api/calendar/share-candidates?purpose=request\|direct&q=&page=&pageSize=` | `purpose=request` (UC-84a): cần `MeetingCreator`, trả tài khoản nội bộ active trừ chính mình. `purpose=direct` (UC-84b): chỉ cần đăng nhập, trả tài khoản active trừ chính mình **và có `MeetingCreator`**. Mỗi dòng chỉ `id`, `fullName` (cân nhắc bỏ `email`, BR-427). Có thể kèm cờ `hasOpenShare` để FE làm mờ người đã có chia sẻ Pending/Effective cùng chiều (BR-423), server vẫn kiểm lại khi gửi. Đọc lỗi → **mã mới lấy từ catalogue lúc làm** (không tự đặt số trong note). |

Sửa sequence:

- [UC84a_RequestCalendarShare.txt](../../06-sequencediagram/calendar-sharing/UC84a_RequestCalendarShare.txt),
  [UC84b_ShareCalendarDirectly.txt](../../06-sequencediagram/calendar-sharing/UC84b_ShareCalendarDirectly.txt):
  thêm đoạn **tải danh sách người** lúc đầu (Page → Controller → Service → User Repository, lọc ở server như bảng trên),
  trước bước actor gửi. Đây là ngoại lệ "nhiều mũi tên actor → boundary" như UC-04, ghi vào README.
- UC-84a EX-4: thêm bước tải lại danh sách (đúng như spec ghi "refreshes the list").
- UC-85a tìm giờ: người tìm có `MeetingCreator`, nên **dùng lại được** `participant-options` cho danh sách người mời. Ghi rõ
  điều này trong sequence UC85a để BE không làm thêm endpoint thứ ba.

Khi làm code: endpoint mới phải vào `docs/03-api/`, `frontend/src/types`, `frontend/src/api` (skill `add-endpoint`).

## 8. Câu hỏi cần PO / BA chốt

| # | Câu hỏi | Đề xuất |
|---|---|---|
| Q-A | BR-424 "either account is **locked**" nghĩa là *vô hiệu hoá* (UC-06c) hay cả *khoá 15 phút do sai mật khẩu*? | **Chỉ vô hiệu hoá.** Nếu tính cả khoá do sai mật khẩu thì ai cũng có thể cố tình gõ sai 5 lần để dừng vĩnh viễn chia sẻ của người khác, và phải thêm bước dừng vào luồng đăng nhập. Sửa chữ "locked" → "deactivated" ở BR-424, `MSG439` (`tài khoản bị khoá` → `tài khoản bị vô hiệu hoá`), UC-84a EX-4, đặc tả sơ bộ dòng 44 và 336. |
| Q-B | Pending đã qua ngày kết thúc, và thay đổi đang chờ (UC-84d) đã quá hạn, thì job xử lý ra sao? | Pending quá hạn → đóng (Expired) và báo bên gửi; thay đổi đang chờ quá hạn → tự rút, chia sẻ giữ nguyên. Ghi vào BR-425. |
| Q-C | Dừng (BR-424) có áp dụng cả cho chia sẻ Pending không? | Có (khớp UC-84c bước 2). Ghi rõ "Pending or Effective" vào BR-424. |
| Q-D | Danh sách chia sẻ có thành UC con riêng không? | Không, để dưới UC-84 gom (§6). |
| Q-E | Người đã mất `MeetingCreator` có còn xem được danh sách "ai chia sẻ cho tôi" (chỉ xem, thấy chia sẻ đã bị dừng) không? | Có, chỉ xem; không gửi hay dùng được. Sequence UC-84 đã vẽ theo cách này. |
| Q4 *(còn mở ở đặc tả sơ bộ)* | Giờ chạy job, múi giờ | Nửa đêm múi giờ máy chủ (`Asia/Ho_Chi_Minh`); chốt trước khi vẽ S4. |

**PO đã chốt 09/11/2026 (Hải, kênh `note-gửi-ba`; thay các đề xuất ở bảng trên):**

| # | Quyết định | Khác với đề xuất |
|---|---|---|
| Q-A | Chỉ tính tài khoản **bị vô hiệu hoá**. Khoá tạm do gõ sai mật khẩu không tính. | Giống. Sửa "locked" → "deactivated" ở BR-424, MSG439, UC-84a EX-4. |
| Q-B | Hết ngày kết thúc thì chia sẻ **tự hết**; lời mời hoặc yêu cầu sửa còn treo cũng hết theo. **Không có job chạy đêm, không báo, không thêm trạng thái** (không `Expired`). | **Khác.** Bỏ job và sequence `SYS_ExpireCalendarShares`; bỏ BR-425/BR-426 phần hết hạn tự đóng; hết hạn chỉ là điều kiện loại ra lúc đọc. Q4 (giờ chạy job) không còn. |
| Q-C | Tài khoản bị vô hiệu hoá thì chia sẻ đang có **dừng luôn**. Lời mời còn chờ thì **giữ**; người nhận thấy "Tài khoản người gửi đã bị vô hiệu hoá" và chỉ bấm được **Từ chối**; server cũng chặn **Chấp nhận**. | **Khác.** BR-424 không dừng Pending; thêm chữ trên màn và chặn Chấp nhận ở UC-84c (EX mới, cần mã MSG mới). |
| Q-D | Danh sách chia sẻ **tách thành UC con** dưới UC-84, giống UC-32a dưới UC-32. | **Khác.** Thêm UC con (vd. UC-84f View Calendar Shares) vào Report 3. |
| Q-E | Mất quyền tạo cuộc họp thì **ẩn luôn màn Chia sẻ lịch**. | **Khác.** Không còn xem chỉ-đọc; sequence UC-84 và nhánh UC-08/UC-11 phải sửa. |

Đã làm trong PR #295 (09/11/2026): sửa sequence (bỏ tab `System - Expire Calendar Shares`, sửa UC-84f, UC-08, UC-11, UC-84c với EX-5/`MSG468`, UC-85a/85c), BR-424/425/426, MSG439 (deactivated) và `MSG468` mới; `MSG440` ngừng dùng. Còn lại: DOCX Report 3 và `sync_reports.py` (không sửa trong PR này trừ khi reviewer đòi).

## 9. Sau khi sửa

1. Xuất ảnh và ghép tab, từ thư mục gốc repo:
   ```bash
   python scripts/txt_to_sequence_drawio_png.py \
     docs/06-sequencediagram/user-management/UC06c_DeactivateReactivateUserAccount.txt \
     docs/06-sequencediagram/user-management/UC08_AssignSystemRoleToUser.txt \
     docs/06-sequencediagram/role-management/UC11_ConfigureRolePermissions.txt \
     docs/06-sequencediagram/calendar-sharing/*.txt
   ```
   Mở từng ảnh, kiểm tra không bị cắt, rồi chạy validate của drawio-skill trên `TrustCabinet_Sequence_Diagrams.drawio`.
   Tab UC-11 đổi tiêu đề nên phải kiểm tra không sinh ra tab trùng.
2. Cập nhật README: [calendar-sharing/README.md](../../06-sequencediagram/calendar-sharing/README.md) (9 sequence: UC-84a → UC-84f, UC-85a → UC-85c),
   `user-management/README.md`, `role-management/README.md` nếu có liệt kê nội dung. Tab bỏ: `System - Expire Calendar Shares` và `UC-84 - View Calendar Shares` (đổi thành `UC-84f`).
3. Đặc tả sơ bộ: thêm endpoint `share-candidates` vào §6, đổi trạng thái câu hỏi ở §7, thêm Q-A → Q-E, Q4 không còn; thêm UC-84f và `MSG468`.
4. Report 3 (PO đã trả lời Q-A → Q-E 09/11/2026): sửa DOCX trong `docs/08-reports/source/` (BR-424/425/426, MSG439, MSG468, UC-84f), rồi chạy
   `python scripts/sync_reports.py --source docs/08-reports/source/<file>.docx --report 3`. **Không sửa file `.md` sinh ra.**
   Giữ ngôn ngữ nghiệp vụ, không ghi tên endpoint. Việc này **chưa làm trong PR #295** (trừ khi reviewer đòi) — xem §8.
5. Comment lên SCRUM-142: liệt kê S1–S6, quyết định PO Q-A → Q-E 09/11/2026, PR + commit.
6. Thêm dòng 39 vào [README.md](README.md) của thư mục này.

**Pending: đặc tả màn hình.** Note này chỉ sửa sequence và đặc tả sơ bộ. Màn FE-16 (*Chia sẻ lịch*, danh sách
hai chiều ở S5, ô chọn người ở S6, màn tìm giờ) chưa có đặc tả màn hình trong Report 3 (§I.5.1 + §III) và chưa có
wireframe. Để sau, khi FE có màn, theo việc còn mở ở đặc tả sơ bộ (`BA Final Review ... màn FE-16`). Không viết
đặc tả màn hình trong lần sửa này.

## 10. Checklist

- [x] S1 UC-06c: khối dừng chia sẻ trong nhánh vô hiệu hoá, note ở nhánh mở lại; nay chỉ dừng **Effective**, Pending giữ lại (Q-C)
- [x] S2 UC-08: `opt` mất `MeetingCreator`, chỉ bên xem; nay chỉ dừng **Effective**, mất quyền ẩn màn (Q-C, Q-E)
- [x] S3 UC-11: sửa tiêu đề UC-10 → UC-11, `opt` vai trò mất quyền tạo họp; nay chỉ dừng **Effective**, mất quyền ẩn màn (Q-C, Q-E)
- [x] S4 (đã bỏ theo PO Q-B 09/11): xoá `SYS_ExpireCalendarShares.txt/.png`, tab `System - Expire Calendar Shares`, ảnh `drawio-png/sys_expirecalendarshares.drawio.png`; UC85a/UC85c giữ loại `end_date < today` lúc đọc, không job, không `Expired`
- [x] S5 `UC84f_ViewCalendarShares.txt` (đổi từ `UC84_ViewCalendarShares.txt` theo Q-D); mất `MeetingCreator` → `403 MSG448`, ẩn màn (Q-E)
- [x] S6 endpoint `share-candidates` trong §6 + bước tải danh sách ở UC84a/UC84b; UC85a ghi dùng `participant-options`
- [x] PO trả lời Q-A → Q-E (09/11/2026); Q4 không còn vì bỏ job. Đã sửa theo câu trả lời trong PR #295 (sequence, BR-424/425/426, MSG439 deactivated, UC-84c EX-5/`MSG468`, `MSG440` ngừng dùng), xem khối "PO đã chốt 09/11/2026"
- [x] Xuất ảnh, ghép tab, validate drawio (chỉ tab có `.txt` đổi thật; tab khác giữ nguyên từ main)
- [x] README + đặc tả sơ bộ
- [x] Report 3 DOCX (Q-A → Q-E: BR-424/425/426, MSG439, MSG468, UC-84f) + sync_reports.py — xong 11/11/2026, PR #322
- [x] Comment SCRUM-142 — xong 12/11/2026, comment `10521`
- [ ] *(pending, chờ FE có màn)* Đặc tả màn hình FE-16 trong Report 3 (§I.5.1 + §III) và wireframe
