# SCRUM-116 — Truy cập cuộc họp qua lời mời Khách (UC-04): việc BA Final Review

**Người ghi:** KhanhPG (BA), 01/11/2026. **Nguồn:** comment của Hoàng Đức Lộc trên SCRUM-116 (30–31/10), PR #203 mục
"Deviations from the docs", [contract UC-04](../../03-api/16-truy-cap-khach-uc04.md), contract
[Agenda](../../03-api/08-agenda-contract-tich-hop-guest-uc04.md) và [Thành phần tham dự](../../03-api/09-thanh-phan-tham-du-guest-uc04.md),
danh mục mã (mục "Truy cập Khách — SCRUM-116"). Note này gom việc BA, không thay Jira hay đặc tả.

> **Tiến độ 01/11/2026: phần UC, BR, MSG, sequence đã làm (xem §3, ô `[x]`). ĐẶC TẢ MÀN HÌNH CHƯA LÀM** — màn 1.3 và mọi chỗ
> liên quan màn hình còn trống có chủ ý, tìm chữ "CHƯA LÀM" trong note. Còn lại khác: ADR (chờ số từ Hải),
> comment Jira. Change Log đã ghi (02/11/2026).
>
> **02/11/2026: PO đã chốt 4 câu hỏi ở §4** (che email roster, khoá tên, không thu hồi riêng phiên, Khách Online có ghi âm) —
> xem §4.1; điểm 3 và 6 ở bảng §2 theo đó đổi.

## 1. Trạng thái

- PR #203 (SCRUM-116) đã **MERGED vào `main`** ngày 31/10/2026; SCRUM-123 (#208, Khách xem tài liệu) cũng đã vào `main`.
  Comment Jira còn ghi "chưa merge" là cũ.
- Jira: SCRUM-116 ở **Frontend**; SCRUM-117–120 và 123 **Done**; SCRUM-121 (FE màn 1.3) và 122 (E2E) **To Do**.
  Chưa phải BA Final Review: BA chốt tài liệu sau khi FE xong (quy trình DEVELOPMENT_WORKFLOW §2.1), nhưng sáu điểm khác
  tài liệu dưới đây đã rõ, làm trước được.
- Report 3 hiện **chưa** có MSG371–MSG379 (snapshot chỉ có MSG126). Danh mục mã đã có, mã tiếp theo là **MSG380**.

## 2. Sáu điểm khác tài liệu, và chỗ phải sửa

| # | Khác gì | Chỗ BA sửa (Report 3 DOCX) |
|---|---|---|
| 1 | Giá trị OTP và phiên: mã hiệu lực 15 phút, sai tối đa 5 lần (khoá mã), gửi lại cách nhau tối thiểu 1 phút, **tối đa 20 mã mỗi lời mời trong 24 giờ**, phiên Khách 12 giờ không gia hạn. Nhập mã cũ sau khi gửi mã mới vẫn báo lỗi nhưng không tính là lần sai của mã mới | UC-04 EX-2 tách thêm: khoá sau 5 lần sai, gửi lại quá sớm, quá 20 mã/24 giờ; BR-153 (xác minh email) và **BR-155** ("hạn theo cấu hình" → 12 giờ) ; màn 1.3: nút Gửi lại đếm ngược, hiện thông báo khi quá giới hạn — **CHƯA LÀM (đặc tả màn hình chưa làm)** |
| 2 | **Khách mất mọi quyền khi cuộc họp `Ended`** (PO 30/10/2026) cho mọi đường đọc: Agenda, thành phần, điểm danh, biểu quyết, tài liệu, realtime | **UC-26 PRE-1** (dòng "Published, InProgress or Ended" → bỏ Ended); **BR-181** ("như Participant" thêm: chỉ khi cuộc họp Published hoặc InProgress); UC-32a, UC-32c (Khách không đọc tài liệu sau khi Ended); rà các UC trong cột UC của BR-181 (UC-27–31, 35, 37–42, 52a, 52b, 55) xem có câu nào ngầm cho Khách sau khi kết thúc. UC-32a bước 2 còn viết "Guest access ... is not built in this version": đã làm xong, sửa |
| 3 | Phải **khai tên hiển thị mới vào được cuộc họp** (bước 5 trước bước 6). Tên bắt buộc ≤200 ký tự; chức vụ tuỳ chọn ≤200; cấm ký tự điều khiển. Khai lại thì ghi đè; **chưa khoá tên sau lần khai đầu** | UC-04 bước 5 ghi bắt buộc; thêm EX thông tin hiển thị không hợp lệ (MSG377); **BR-180** hiện ghi "chưa chốt validation": chốt theo code. **Khoá tên: PO đã chốt "khoá" (02/11), xem §4.1 B** |
| 4 | "Lời mời hết hạn" (EX-1) nghĩa là cuộc họp không còn Published/InProgress; lời mời **không có hạn riêng**. Mọi lý do EX-1 trả cùng một thông báo, không nêu lý do cụ thể | UC-04 PRE-2 ("not revoked or expired") và EX-1: định nghĩa "expired"; BR-155 |
| 5 | Mã mới: MSG371 (EX-1), 372 (khoá), 373 (gửi lại quá sớm), 374 (EX-3), 375 (đã gửi mã), 376 (lưu hồ sơ), 377 (hồ sơ không hợp lệ), 378 (phiên đã kết thúc), 379 (quá 20 mã/24 giờ). **MSG368–370 bỏ qua có chủ ý** (đã lên main cùng ADR-0043 rồi hoàn tác, giữ cho lần làm lại) | Report 3 §V.2: thêm 9 dòng, bản tiếng Anh theo cột "Report 3" trong danh mục; **không** dùng MSG368–370; ghi MSG126 giữ nguyên cho mã sai/hết hạn/đã dùng |
| 6 | Đăng xuất Khách chỉ kết thúc phiên đó; thu hồi lời mời kết thúc mọi phiên; **chưa có** chức năng Thư ký thu hồi riêng một phiên | UC-31 (thu hồi lời mời) POST: ghi "ends every session"; không viết UC mới. **PO chốt không làm, ngoài phạm vi (02/11), xem §4.1 C** |

## 3. Việc BA, theo thứ tự

- [ ] **ADR mới thay một phần ADR-0016** (ADR đã chấp nhận thì không sửa): Khách không còn quyền "như Member" sau khi `Ended`.
      Số ADR do Hải cấp (0043 đã dùng rồi hoàn tác). Hỏi Hải trước khi đặt số.
- [x] UC-04 (01/11/2026): PRE-2, POST-1, bước 2 và 5, AF-2 mới (xin mã mới), EX-1 viết lại, EX-2/EX-3 thêm mã MSG, EX-4..EX-8 mới.
- [x] BR-153, BR-155, BR-180, BR-181 sửa theo mục 2. **BR-371 mới** (gửi lại cách ≥1 phút, tối đa 20 mã/24 giờ), cột duyệt ghi
      "NEEDS REVIEW PO".
- [x] BR còn dở (02/11/2026): đã thêm UC-04 vào cột UC của BR-153 và BR-155; đã ghi quyết định PO 30/10/2026 vào ô duyệt của BR-181.
- [x] UC-26 PRE-1; UC-32a, UC-32c; UC-30 PRE-1; rà các UC trong cột UC của BR-181 (BR-181 đã phủ, không sửa thêm).
- [x] Thu hồi lời mời (điểm 6): **UC-28c** (Remove Meeting Participant) POST-2, không phải "UC-31" như note ghi.
- [ ] **CHƯA LÀM — ĐẶC TẢ MÀN HÌNH (cố ý để lại, chưa làm gì, 01/11/2026).** Màn hình 1.3 (Report 3 §III): Resend code đếm ngược
      theo giây còn chờ, hai thông báo MSG373/MSG379, bước nhập tên bắt buộc. Làm qua wireframe ([plan 06](06-plan-dac-ta-man-hinh.md));
      chờ FE SCRUM-121 để chụp đúng. Ai làm tiếp: đừng coi UC-04 là xong trọn gói, phần màn hình vẫn trống.
      **02/11/2026:** FE SCRUM-121 đã merge (PR #212), chụp được rồi; điểm lệch màn hình xem [note 23](23-pr212-scrum-121-122-123-man-khach.md).
      Khách trong phòng họp: [note 24](24-pr213-215-scrum-59-127-khach-phong-hop.md); gán người nói là Khách: [note 25](25-pr218-gan-nguoi-noi-la-khach.md).
- [x] Report 3 §V.2: MSG371–MSG379 (dòng #371–#379, tiếng Anh); danh mục `00-danh-muc-ma-thong-bao.md` thêm dòng tiếng Anh cho MSG379.
- [x] Sequence `docs/06-sequencediagram/auth/UC04_AccessMeetingViaGuestInvitation.txt` + PNG: thêm nhánh `alt` EX-1, EX-4..EX-7, MSG376, phiên 12 giờ.
- [x] Sync: đã chạy `sync_reports.py --report 3` (snapshot MD, 05/06 extract, `uc-map.html` đã sinh lại).
- Sửa UC-04/BR theo 4 điểm PO chốt (§4.1), kiểm lại code trên `main` ngày 02/11/2026. **Việc giao BE cho A và B (chỗ sửa, test, tài
  liệu, câu còn mở): [note 26](26-giao-viec-be-scrum-116-che-email-khoa-ten.md).**
  - [x] **C** (không thu hồi riêng một phiên): UC-28c POST-2 đã đúng; đã ghi vào mục "Ngoài phạm vi UC" của
        `docs/01-requirements/00-master-use-case-inventory.md` §8.
  - [ ] **A** (che email roster khi người xem là Khách): **code chưa làm.** `ParticipantService.ReadAsync` vẫn trả `g.Email` và
        `p.User.Email` cho mọi người xem, kể cả Khách; contract
        [09-thanh-phan-tham-du.md](../../03-api/09-thanh-phan-tham-du.md) dòng 47–53 vẫn nêu `email`. #212 (FE màn Khách),
        #213/#215 (phòng họp), #218 (gán người nói) đã vào `main` nhưng không có phần này. Việc còn lại: hỏi Lộc/gumarr
        PR che email ở đâu; khi merge thì sửa BR-181 (hoặc BR danh sách thành phần), UC-28 nhóm, contract 09.
  - [ ] **B** (khoá tên sau lần khai đầu): **code chưa làm.** `GuestAccessService.SaveProfileAsync` vẫn ghi đè
        `SelfDeclaredDisplayName`; contract [16](../../03-api/16-truy-cap-khach-uc04.md) dòng 208 còn ghi "tạm chưa làm". Khi merge:
        BR-180 bỏ "chưa khoá tên", UC-04 bước 5 và EX khai lại bị từ chối (lấy mã MSG theo code).
  - [ ] **D** (Khách Online có ghi âm theo micro): chờ SCRUM-129. Code hiện chưa ghi: màn chờ của Khách không gắn bộ ghi âm
        ([note 24](24-pr213-215-scrum-59-127-khach-phong-hop.md)). Khi làm: sửa BR-370 ("NEEDS REVIEW PO") và UC-39a, UC-43a,
        UC-80 ở [note 19](19-plan-fe-09-fe-10-ghi-am-go-bang-voice-id.md); rà thông báo ghi âm cho Khách (Luật 91/2025/QH15).
- [x] Change Log của Report 3 (02/11/2026): đã ghi một dòng cho Final Review SCRUM-116. Điểm A, B, D chưa ghi vì chưa sửa gì.
- [ ] Ghi kết quả vào comment Jira SCRUM-116 sau khi FE (SCRUM-121) xong.

## 4. Hỏi PO / Hải

### 4.1. PO đã chốt (Hải, ghi 02/11/2026)

Nguồn: [comment PO trên SCRUM-116](https://sep490g116.atlassian.net/browse/SCRUM-116?focusedCommentId=10394). Bốn điểm này
**lệch docs hiện tại**, BA (Khánh) sửa UC-04/BR lúc Final Review.

| # | Câu hỏi | PO chốt | Ai làm | Chỗ BA sửa |
|---|---|---|---|---|
| A | Roster lộ email | **Che email khi người xem là Khách**, chỉ hiện tên + vai trò | gumarr, trong stack Khách (#212/#213) | BR-181 (hoặc BR về danh sách thành phần): Khách xem roster không thấy email; UC liên quan xem danh sách thành phần (UC-28 nhóm) ghi rõ; contract [09](../../03-api/09-thanh-phan-tham-du-guest-uc04.md) do BE sửa cùng code |
| B | Khoá tên sau lần khai đầu (điểm 3) | **Khoá** | gumarr | BR-180: bỏ "chưa khoá tên", ghi khoá sau lần khai đầu; UC-04 bước 5 + AF/EX khai lại bị từ chối (mã MSG theo code khi merge) |
| C | Thư ký thu hồi riêng một phiên (điểm 6) | **Không làm**, ngoài phạm vi đồ án: gỡ lời mời là phiên Khách hết hiệu lực luôn. Chỉ mở lại khi hội đồng/PO yêu cầu | — | Giữ UC-28c POST-2 như đã sửa; ghi vào danh sách ngoài phạm vi, không viết UC mới |
| D | Khách họp Online có ghi âm theo micro (note 19 D6) | **Có ghi** | Hải, sau khi #213/#215 merge — SCRUM-129 | UC ghi âm/gỡ băng: Khách Online cũng được ghi theo micro; rà consent/thông báo ghi âm cho Khách (Luật 91/2025/QH15) |

Thứ tự: C đã xong; A và B chờ code (chưa có trên `main`, xem §3); D chờ SCRUM-129.

### 4.2. Còn chờ

- **BR-371 (mới, do BA thêm 01/11/2026)**: gửi lại cách ≥1 phút, tối đa 20 mã/24 giờ. Lấy từ code SCRUM-116, PO chưa xác nhận;
  ô duyệt trong Report 3 đang ghi "NEEDS REVIEW PO".

## 5. Việc không phải của BA

- Đổi `Authentication:SigningKey` làm chết mọi link mời đã gửi (đã ghi vào `deploy/README.md` và contract §2). Chỉ cần BA biết khi viết UC-31
  hoặc hướng dẫn vận hành.
- Chưa giới hạn số phiên Khách mỗi lời mời; còn một vòng kiểm tra thừa trong `AttendanceChangePublisher` (đã có từ trước). Lộc ghi "noted".
