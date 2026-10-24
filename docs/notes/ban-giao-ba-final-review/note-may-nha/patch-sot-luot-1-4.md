# Sót lượt 1–4 — kiểm kê và patch Report 3 (BA Final Review 24/10/2026)

Gom mọi việc lượt 1–4 (SCRUM-26, 107, 97, 18/20) và màn hình wireframe v8 còn mở sau commit `89e312f`. **Đã dán vào DOCX** ngày 24/10/2026 (unzip → sửa `word/document.xml` → zip → `python scripts/sync_reports.py --report 3`; `validate.py` chỉ báo `bookmark id=151` trùng sẵn có ở 8.5/8.6). Không gồm: Voice ID (§4 plan 05), nghị sự (§6b, lượt 5), IV.2.3/IV.2.8 (13/2023, lượt 7), dữ liệu mẫu "Chủ trì" trong wireframe.

## Checklist

| # | Việc | Trạng thái |
|---|---|---|
| a | Permission Matrix I.4.4: UC-29 bỏ Chair (ô Chair = No); chú thích ¹⁰; mô tả actor Chair ("responds to the invitation" bỏ) | Xong |
| b | I.3.2 InvitationResponse/RespondedAt (participant và guest) và I.3.3 Data Business Rule mới "Invitation response": trả lời một lần, Chair giữ Pending (không có chữ "latest/may change" trong DOCX) | Xong |
| c | I.3.2 StoredFile.PageCount | Xong |
| d | Ảnh: use case Chair (`uc-chair.png` thay `media/image11.png`), BF-02 (thay `media/image4.png`); giữ `wp:extent` (tỉ lệ khớp). SM-04 và sequence **không nhúng** trong Report 3 (chỉ dẫn đường dẫn) nên không thay | Xong |
| e | Email lời mời 13.2: dòng Decline "trả lời một lần" (patch-18-20 mục 10.5) và bullet Respond ghi Chủ tọa không được hỏi; danh mục màn hình 6.4 và §III Meeting List/Create/Participants/Invitation Response đã làm ở lượt 6 | Xong |
| f | V.2: thêm 31 dòng (#261–#291), MSG152/MSG153 → Retired, dòng #8 phiên hết hạn MSG140 → MSG01 (xem bảng dưới) | Xong, còn 3 điểm mở |
| g | Change Log: thêm 5 dòng 24/10/2026 (SCRUM-107, 97, 18/20, màn hình v8, rà soát tổng); dòng SCRUM-26 đã có | Xong |
| h | `00-master-use-case-inventory.md` UC-29 bỏ Chair; `05-use-case-specifications.md` (UC-14/17/29/30/31/33a/28b) và `06-business-rules.md` (BR-30/105/106/115/126/172/215/295/320/321) đã khớp DOCX | Xong |
| i | Plan 05 mục 1 (Jira/PR) | Chuyển session Jira |
| j | Plan 05 mục 2, 3, 5, 6, 7 và plan 06 SCRUM-18/20/97/26/107: đánh [x] | Xong; còn [ ] ngoài phạm vi: I.5.1.a (chờ session Screen Flow), Voice ID, nghị sự, Jira |

Bổ sung sau: hình I.5.1.a thay bằng PNG Screen Flow mới (`media/image15.png`, thêm node `s8_7` và cạnh `e61` từ 8.2) và thêm dòng 8.7 vào Screen List I.5.1.b.

## Còn mở, cần quyết định

1. **Quyền bổ sung** (PO bỏ 20/10): MSG152/MSG153 đã Retired ở V.2 nhưng UC-28a EX-2, UC-28b (tiêu đề, bước 7, AF-2, EX-1), BR-174–BR-177, ma trận quyền ("each view requires its matching additional permission") và III.6.1 vẫn nhắc. Cần BA/PO chốt ai xem được phản hồi khi không còn grant rồi sửa một lượt riêng. Cũng còn "Recovery Secretary" ở UC-28a.
2. **MSG141/MSG144**: V.2 giữ Retired (đặc tả bỏ 18/10) trong khi catalogue ghi BE còn trả; chờ SCRUM-25 sửa. Không đổi.
3. **MSG140**: catalogue dành cho UC-18 EX-5 (đã đúng ở dòng #135); dòng #8 sai mã đã sửa thành MSG01 theo catalogue dòng 623.
4. **Loại hiển thị / Where** của 31 dòng mới do mình chọn theo tình huống (catalogue không ghi); BA đổi nếu cần. Câu tiếng Anh dịch từ câu tiếng Việt catalogue (MSG117/118/123/124 dùng bảng tiếng Anh sẵn có). MSG119–122, MSG125 (dành sẵn) không thêm; MSG271–274 để lượt 5.
5. Bảng tiếng Anh trong catalogue không thêm dòng cho 31 mã mới (catalogue đã có câu tiếng Việt).
6. Email 13.2: bullet "Respond" thêm câu Chủ tọa không được hỏi là suy ra từ BR-105/UC-29; kiểm lại nếu email Chủ tọa vẫn có nút.
7. Khung wireframe `4.1e` (khung xem nhanh trên lịch) chưa có; III.4.x "Chủ trì/Chủ tọa", "Đang họp/Đang diễn ra": BA chọn một cách gọi (patch-dac-ta-man-hinh mục 5–6).

## Patch (câu cũ → câu mới)

| Mục | Ô | Câu cũ | Câu mới |
|---|---|---|---|
| I.4.4 | Invitation response / Respond, cột Chair | `Restricted ¹⁰` | `No` |
| I.4.4 | Chú thích ¹⁰ | `¹⁰ Only the user's own invitation, while the meeting is still open for responses.` | `¹⁰ Only the user's own invitation, while the Meeting is Published or In Progress; each invitation is answered once and the answer cannot be changed afterwards. The Chair does not respond. (BR-105, BR-106)` |
| I.4.1 | Mô tả actor Chair | `The Chair responds to the invitation, starts and ends the meeting, …` | `The Chair starts and ends the meeting, … The Chair does not respond to the invitation because the Chair runs the meeting. …` |
| I.3.2 | Participant, InvitationResponse | `Answer to the meeting invitation. Values: Pending, Accepted, Declined.` | `Answer to the meeting invitation, recorded once and not changed afterwards. Values: Pending, Accepted, Declined. The Chair does not respond, so the Chair's value stays Pending.` |
| I.3.2 | Participant, RespondedAt | `Date and time the participant responded to the invitation.` | `Date and time the participant answered the invitation; set once together with InvitationResponse.` |
| I.3.2 | Guest, InvitationResponse | `Guest's answer to the invitation.` | `Guest's answer to the invitation, recorded once and not changed afterwards.` |
| I.3.2 | Guest, RespondedAt | `Date and time the guest responded.` | `Date and time the guest answered the invitation; set once together with InvitationResponse.` |
| I.3.3 | Dòng mới sau "Meeting schedule" | — | `Invitation response` · `Each invitation has one response: Pending until the invitee answers Accepted or Declined. The answer is recorded once with its response time and is not changed afterwards; the Chair does not answer, so the Chair's invitation stays Pending. The initial participants of a new Meeting are saved with the Meeting in one save, each Pending.` · `Meeting creation; Invitation response` |
| I.3.2 | StoredFile.PageCount | `Number of pages of a PDF; null for a DOCX, which is previewed as one continuous document.` | câu mới trong [patch-scrum-107 mục 4](patch-scrum-107.md) |
| 13.2 | Decline [Không tham dự] | `… Internal participants and Guests may accept or decline and change their own response while responses are allowed (BR-105).` | `… Internal participants other than the Chair and Guests answer once and cannot change the answer afterwards (BR-105, BR-106).` |
| 13.2 | Bullet Respond to Meeting Invitation | `… with that choice pre-selected (UC-29).` | `… with that choice pre-selected (UC-29). The answer is recorded once; the Chair is not asked to respond.` |
| V.2 | #8 | `MSG140` (Common – Session Renewal) | `MSG01` |
| V.2 | #147 MSG152, #148 MSG153 | `Field / Form error`, `Toast` + câu cũ | `Retired`; `Retired; formerly …`; `Retired on 20/10/2026 (PO 20/10/2026): additional Meeting permissions per participant were removed. This code must not be reused.` |
| V.2 | #261–#291 (dòng mới) | — | MSG78–82, 85–89, 117, 118, 123, 124, 130–137, 275, 276, 278, 280–284, 287 (nội dung trong Report 3 V.2) |
| Change Log | 5 dòng 24/10/2026 | — | trong Report 3 |
| Ảnh | I.4.3.4 | `media/image11.png` | `docs/assets/use-case-diagrams/uc-chair.png` |
| Ảnh | 2.2 BF-02 | `media/image4.png` | `docs/09-bussinessflow/BF-02_Meeting_Preparation_Approval.drawio.png` |
