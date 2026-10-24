# Patch Report 3 — SCRUM-97 (UC-73 giao Chủ tọa, UC-28b)

**Ngày:** 24/10/2026 · **Lượt 3** theo [05-plan](../05-plan-sua-uc-br-msg-diagram.md) mục 8 · **Nguồn:** [02](../02-scrum-97-giao-chu-toa.md),
[bàn giao SCRUM-97, mục "Nghiệp vụ đổi vai trò đã chốt"](../../../03-api/09-thanh-phan-tham-du-ban-giao.md), [API thành phần tham dự](../../../03-api/09-thanh-phan-tham-du.md),
catalogue MSG256–MSG260. PO Hải chốt 23/10/2026; FE ở PR #123.

**Chưa áp dụng vào DOCX.** Khánh dán các dòng dưới vào `Report_3_...docx`, rồi chạy `python scripts/sync_reports.py --report 3`.
Bản Markdown `06-business-rules.md`, bảng tiếng Anh `00-danh-muc-ma-thong-bao.md` và UC-28b trong `05-use-case-specifications.md` đã sửa tay
cho khớp patch này (bản 05 là bản rút gọn của Report 3: UC-28b chỉ có Description, AF-1 và EX-5 tương ứng; UC-73 không có trong file này).
Các đoạn không ghi ở đây giữ nguyên.

Tên cột: **Mục** (vị trí trong Report 3) · **Ô / bước** · **Câu cũ** · **Câu mới**.

## 1. II.6.6 UC-73 – Hand Over Chair to Another Participant

Ý chính: FE không có nút "Hand over Chair" riêng và không hiện danh sách người nhận. Chủ tọa bấm **Change role** trên dòng người muốn giao, chọn
**Chair** trong danh sách vai trò (mỗi vai trò có một dòng mô tả quyền), rồi bấm **Hand over Chair**. Lựa chọn Chair chỉ có trên dòng người nhận được,
nên không ai nhận được thì không có lựa chọn và MSG258 không bao giờ hiện trên UI. Quy tắc nghiệp vụ không đổi; số bước (1–7) và nhãn EX giữ nguyên.

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| II.6.6 | Preconditions, PRE-3 | `● PRE-3: The Meeting has at least one current internal participant who is neither the Chair nor the current Secretary. (BR-295)` | `● PRE-3: The Meeting has at least one current active internal participant who is neither the Chair nor the current Secretary, so that the Chair choice is offered on at least one participant's row. (BR-295)` |
| II.6.6 | Normal Flow, bước 1 | `1. The Chair opens the Meeting participant list and selects Hand over Chair.` | `1. The Chair opens the Meeting participant list and selects Change role on the row of the participant who will receive the Chair role.` |
| II.6.6 | Normal Flow, bước 2 | `2. The system displays the current internal participants who can receive the Chair role. Guests and the current Secretary are not listed. (BR-295)` | `2. The system displays the Meeting Roles as a list of options, each with a short description of what the role allows. The Chair option appears only for a current active internal participant who is neither a Guest nor the current Secretary. (BR-295)` |
| II.6.6 | Normal Flow, bước 3 | `3. The Chair selects one participant and confirms the hand-over.` | `3. The Chair selects the Chair option and confirms with Hand over Chair.` |
| II.6.6 | Alternative Flows, AF-1, tiêu đề + bước 1 | `AF-1 – No participant can receive the Chair role` / `1. At Step 2, no current internal participant other than the Chair and the current Secretary exists.` | `AF-1 – No participant can receive the Chair role` / `1. At Step 2, no current active internal participant other than the Chair and the current Secretary exists.` |
| II.6.6 | AF-1, bước 2 | `2. The system displays MSG258 – "No participant can receive the Chair role. Add an internal participant first." No role changes.` | `2. The system offers the Chair option on no participant's row and displays no message. No role changes. MSG258 is returned by the API but is not displayed on the screen.` |
| II.6.6 | EX-1, bước 3 | `3. The flow resumes at Step 2 of the Main Flow.` | Giữ nguyên (bước 2 vẫn là danh sách vai trò, người dùng chọn lại) |

Không đổi: Description, PRE-1, PRE-2, Postconditions, Normal Flow bước 4–7, AF-1 bước 3, EX-1 bước 1–2, EX-2, EX-3.

## 2. II.6.2.2 UC-28b – Manage Meeting Role & Additional Permissions

Ý chính: sau khi công bố, Thư ký **chỉ** thay Chủ tọa khi **tài khoản Chủ tọa bị khoá**; Chủ tọa và Thư ký **không hoán đổi vai trò**; đổi Thư ký = cho người mới
làm Thư ký rồi đổi người cũ về Thành viên (hai thao tác, cuộc họp luôn còn Thư ký). Trước công bố vẫn đổi tự do.

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| II.6.2.2 | Description | `... Before the Meeting is Published, the Secretary may also replace the Chair. From publication onward, only the current Chair can give the Chair role to someone else, through UC-73. (BR-126, BR-294)` | `... Before the Meeting is Published, the Secretary may also replace the Chair. From publication onward, only the current Chair can give the Chair role to someone else, through UC-73; the only exception is that the Secretary may replace the Chair when the Chair's account is locked. The Chair and the Secretary do not swap roles. To change the Secretary, give the Secretary role to the new person, then change the previous Secretary to Member. (BR-126, BR-294, BR-295)` |
| II.6.2.2 | Postconditions, POST-2 | `● POST-2: A Chair replacement before publication changes both affected internal Meeting Roles together. System Role and Invitation Response remain unchanged. (BR-96, BR-169, BR-177)` | `● POST-2: A Chair replacement changes both affected internal Meeting Roles together. System Role and Invitation Response remain unchanged. (BR-96, BR-169, BR-177)` |
| II.6.2.2 | AF-1, bước 1 | `1. At Step 3, while the Meeting is not yet Published, the Secretary selects a current internal participant as the new Chair. (BR-294)` | `1. At Step 3, while the Meeting is not yet Published, or once it is Published or InProgress and the Chair's account is locked, the Secretary selects a current active internal participant as the new Chair. After publication the selected participant must not be the Secretary. (BR-126, BR-294, BR-295)` |
| II.6.2.2 | AF-1, bước 2 | `2. The system changes the former Chair to Member and the selected participant to Chair in the same save, subject to the current Secretary requirements.` | Giữ nguyên |
| II.6.2.2 | EX-5, bước 1 | `1. At Step 5, the Meeting is Published or InProgress and the change would replace the current Chair, give the Chair role to another participant or change the current Chair to another Meeting Role. (BR-126)` | `1. At Step 5, the Meeting is Published or InProgress, the Chair's account is active, and the change would replace the current Chair, give the Chair role to another participant or change the current Chair to another Meeting Role. When the Chair's account is locked, this exception does not apply and the change continues as AF-1. (BR-126)` |
| II.6.2.2 | EX-5, bước 2 | `2. No role or permission change is saved. The system displays MSG256 – "From publication onward, only the current Chair can hand over the Chair role."` | `2. No role or permission change is saved. The system displays MSG256 – "From publication onward, only the current Chair can hand over the Chair role while the Chair's account is active."` |

Không đổi: Primary Actors, Preconditions, Normal Flow, AF-2, EX-1–EX-4, EX-5 bước 3. Trong `05-use-case-specifications.md` UC-28b rút gọn chỉ có EX-1–EX-3; EX-5 mới thêm vào
đó giữ đúng nhãn của Report 3 (không có EX-4 ở bản 05).

## 3. V.1 Business Rules

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| V.1 | BR-126, cột mô tả | `A Published or InProgress Meeting must retain its current Chair. From publication onward, the Chair role changes only when the current Chair hands it over through UC-73; the Secretary and the Meeting Organizer cannot replace the Chair. (BR-169)` | `A Published or InProgress Meeting must retain its current Chair. From publication onward, the Chair role changes only when the current Chair hands it over through UC-73; the Secretary and the Meeting Organizer cannot replace the Chair, except that the Secretary may replace the Chair when the Chair's account is locked. (BR-169, BR-295)` |
| V.1 | BR-126, cột nguồn | `PO 16/10/2026 — Chair hands over the Chair role; Secretary does not replace the Chair` | `PO 16/10/2026 — Chair hands over the Chair role; Secretary does not replace the Chair; PO 23/10/2026 — Secretary replaces the Chair only when the Chair's account is locked` |
| V.1 | BR-294 | — | Không đổi. "Before the Meeting is Published, the Secretary may assign or replace the Chair" khớp "đổi vai trò tự do trước công bố"; ngoại lệ sau công bố nằm ở BR-126 |
| V.1 | BR-295, cột mô tả | `The Chair role may be handed over only to a current active internal participant of the Meeting who is neither a Guest nor the current Secretary.` | `The Chair role may be handed over only to a current active internal participant of the Meeting who is neither a Guest nor the current Secretary, so the Chair and the Secretary do not swap roles. The same rule applies when the Secretary replaces a Chair whose account is locked.` |
| V.1 | BR-295, cột UC | `UC-73` | `UC-28b, UC-73` |
| V.1 | BR-295, cột nguồn | `PO 16/10/2026 — Chair hands over the Chair role; Secretary does not replace the Chair` | `PO 16/10/2026 — Chair hands over the Chair role; Secretary does not replace the Chair; PO 23/10/2026 — no swap between Chair and Secretary` |

BR-296 và BR-169: không đổi. Quy tắc "không hoán đổi" gộp vào BR-295 thay vì BR mới (mặc định theo plan, tránh BR-320).

## 4. V.2 System Messages

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| V.2 | Dòng `237 / MSG256`, cột *Message* | `From publication onward, only the current Chair can hand over the Chair role.` | `From publication onward, only the current Chair can hand over the Chair role while the Chair's account is active.` |
| V.2 | Dòng `239 / MSG258`, cột loại hiển thị | `Dialog` | `Not displayed` |
| V.2 | Dòng `239 / MSG258`, cột *Where* | `UC-73 / AF-1` | `UC-73 / AF-1 (returned by the API; the screen offers no Chair option instead of showing it)` |

Câu MSG258 và các dòng MSG257, MSG259, MSG260 giữ nguyên. Không xoá mã MSG258, không tạo mã mới.

## 5. Change Log (Report 3, cuối bảng)

| Date | A/M/D | Person in charge | Description |
|---|---|---|---|
| 24/10/2026 | M | KhanhPG | Align UC-73 and UC-28b with SCRUM-97 as implemented: UC-73 starts from Change role on the recipient's row and a role option list instead of a recipient list, AF-1 no longer displays MSG258 (no Chair option is offered), PRE-3 says active participant; UC-28b Description, AF-1 and EX-5 allow the Secretary to replace the Chair after publication only when the Chair's account is locked; BR-126 gets that exception, BR-295 states that Chair and Secretary do not swap roles and applies to UC-28b; MSG256 adds the active-account condition, MSG258 is not displayed. |

(Nếu dùng chung một dòng Change Log với lượt khác cùng ngày 24/10/2026, gộp mô tả.)

## 6. Điểm lệch / mặc định đã dùng

1. **Không đổi BR-294**: nội dung đã đúng; ngoại lệ đặt ở BR-126.
2. **"Không hoán đổi"** gộp vào BR-295 (plan cho phép BR-295 hoặc BR mới), không dùng BR-320.
3. **UC-73 không có trong `05-use-case-specifications.md`** (chỉ Report 3 và sequence có), nên bản 05 chỉ sửa UC-28b.
4. **UC-28b bản 05 rút gọn** không có EX-4; EX-5 mới thêm với nhãn của Report 3.
5. **MSG258 cột loại hiển thị** đổi sang `Not displayed` (Report 3 V.2 chỉ có cột loại hiển thị, không có giá trị chuẩn cho trường hợp này; Khánh đổi nếu muốn giữ `Dialog`).
6. **FE chọn API ẩn với người dùng** (Chủ tọa ở Published/InProgress gọi POST chair-handover; trước công bố và Thư ký thay Chủ tọa khoá gọi PUT assignments kèm `chairReplacement`). Sequence UC-73 và UC-28b vẽ theo hai API này; không thêm bước chọn API vào UC.
7. **Ranh giới "tài khoản bị khoá"** lấy từ code: Chair `inactive` (tài liệu API); đặc tả dùng "locked" theo note 02.
8. **UC-28 mục 647 và dòng 692 (danh sách UC)** vẫn nói "From publication onward the Chair hands over the role through UC-73": chưa thêm ngoại lệ vì chỉ là tóm tắt.

## 7. Việc còn lại cho máy nhà (Khánh)

Dán patch vào DOCX rồi sync. PNG sequence `UC73_HandOverChair` và `UC28b_AssignChangeMeetingRole`: xem [README](README.md).
