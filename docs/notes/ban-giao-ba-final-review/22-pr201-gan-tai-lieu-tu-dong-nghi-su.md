# PR #201 — Gắn tài liệu từ dòng mục nghị sự, trang đích sau đăng nhập: việc BA Final Review

**Người ghi:** KhanhPG (BA), 01/11/2026; kiểm lại 03/11/2026 sau khi merge `main`. **Nguồn:** mô tả và phần docs của
[PR #201](https://github.com/callmehai/FU-FA26-G116/pull/201) (mục "Notes for BA Final Review"). **Không có ticket Jira**: người dùng
yêu cầu trực tiếp. Note này gom việc BA, không thay đặc tả. Code là nguồn sự thật.

> **Tiến độ 01/11/2026: CHƯA LÀM GÌ.** Mới ghi note. Không có mục nào dưới đây đã sửa vào Report 3 hay sơ đồ.
>
> **Kiểm lại 03/11/2026 sau khi merge `main` (abea1f3): trên `main` chưa ai sửa tài liệu yêu cầu.** Report 3, UC, BR và sequence y
> nguyên như note này ghi; từng chỗ có bằng chứng ở §6. Chỉ phần **vẽ màn hình** là đã có: wireframe v11 (02/11, commit bcadf40) đã vẽ
> kẹp giấy, hộp Sửa nội dung không còn danh sách tài liệu, hộp gắn tài liệu đủ 3 nguồn. Thêm nữa, **PR #235 đang mở** (gumarr, 03/11)
> đã sửa sẵn Report 3 §7.3/§7.4 (tab Tài liệu, hộp thêm tài liệu) và UC-32a AF-1/UC-32c — đừng làm trùng, xem §6. `main` 02/11 cũng
> mang về hai điều chỉnh vào note này: kẹp giấy dùng chung với biểu quyết (điểm 8) và tài liệu gắn biểu quyết vẫn nằm trong "Tài liệu
> chung" (điểm 7b).

## 1. Trạng thái

- PR #201 đã có code, test (1717 pass) và Playwright. Trong PR, `docs/03-api/08-chuong-trinh-nghi-su.md` và
  `docs/03-api/11-tai-lieu-cuoc-hop.md` đã cập nhật. **Không** có ADR, **không** có mã MSG mới, **không** đổi phạm vi.
- Commit a745089 (ADR-0043 cũ, giữ tài liệu chung sau khi gắn) đã hoàn tác ở f0b5195. Hành vi "Tài liệu chung" lúc đó khớp AI-eCabinet
  và khớp BR-280 / ADR-0020 hiện có: không đổi code cho phần này.
- `main` ngày 02/11 thêm SCRUM-96 (biểu quyết và tài liệu biểu quyết): kẹp giấy tách thành `AttachMenu` dùng chung cho mục nghị sự
  (UC-33b) và nháp biểu quyết (UC-76a), tab Tài liệu có thêm nhóm Biểu quyết. Việc BA phát sinh ở điểm 8; quy tắc "tài liệu chung"
  thành lệch AI-eCabinet thật (điểm 7b), không còn là chuyện chờ SCRUM-96.
- Report 3 và sơ đồ sequence còn chưa theo kịp code ở 8 điểm dưới đây.

## 2. Tám điểm khác tài liệu, và chỗ phải sửa

| # | Khác gì | Chỗ BA sửa |
|---|---|---|
| 1 | Form thêm/sửa mục nghị sự **không còn chọn tài liệu**; FE không gửi `documentIds` khi tạo hay sửa. Tài liệu gắn qua UC-33b (kẹp giấy trên dòng mục hoặc tab Tài liệu). API vẫn nhận `documentIds` | **UC-23a, UC-23b** (UC-23b bước 2 hiện ghi "document associations for editing" và bước 4 "any selected Meeting documents": bỏ chọn tài liệu khỏi form, trỏ sang UC-33b/33d). Sequence UC-23a/UC-23b: `createAgendaItem(..., documentIds)` và luồng sửa còn truyền `documentIds` (xem `docs/06-sequencediagram/README.md`); không sai nhưng không còn là đường đi của UI |
| 2 | **Điểm vào mới của UC-33b:** mỗi dòng mục (bản trang) có nút kẹp giấy, menu 3 nguồn: *Tải tệp lên từ máy* (cũng lưu vào kho người tải), *Chọn từ tài liệu cuộc họp* (mọi tài liệu của cuộc họp chưa gắn vào mục đó), *Chọn từ kho của tôi* | **UC-33b** Normal Flow/điểm vào. Đặc tả màn hình: wireframe v11 đã vẽ (xem §3), **Report 3 chưa chép** — thêm nút kẹp giấy vào §5.1 và ảnh mới |
| 3 | `PUT /agenda/{itemId}`: thiếu hoặc null `documentIds` thì **giữ** liên kết; chỉ `[]` mới xoá (trước đây thiếu là xoá) | Không có UC riêng cho API. Ghi vào UC-23b: sửa mục không đụng liên kết tài liệu trừ qua UC-33b/33d. Kiểm tra UC-33d còn khớp |
| 4 | Cờ mới `actions.canLinkDocuments` trên mỗi mục nghị sự: true với Chủ toạ hoặc Thư ký hiện tại, đến khi cuộc họp `Ended` hoặc `Cancelled`. **Chủ toạ gắn được tài liệu từ tab Nghị sự dù không sửa được mục** | **UC-33b** Actor/PRE (Chủ toạ và Thư ký); đối chiếu BR gắn tài liệu (BR-277, BR-278, BR-279 trong UC-23b) xem có câu nào giới hạn Thư ký |
| 5 | **UC-01 / UC-02:** POST-3, POST-4 ghi "redirected to the appropriate authenticated page". Code: đăng nhập xong vào **lịch** (`/calendar`, "Lịch làm việc"), trước đây là danh sách cuộc họp. Ngoại lệ: mở link sâu khi chưa đăng nhập (ví dụ link cuộc họp trong thư mời) thì quay lại trang đó; tài khoản mới không mở được trang đó thì vào lịch (SCRUM-66); phiên hết hạn thì về trang đang xem; **đăng xuất chủ động thì lần đăng nhập sau luôn vào lịch**. `/` cũng mở lịch | **UC-01, UC-02** POST: gọi tên lịch. Có thể thêm AF cho link sâu. Kiểm tra UC-03 (đăng nhập Google) có cùng POST không |
| 6 | **Tài liệu chung (BR-280):** không đổi, khớp AI-eCabinet. Một tài liệu là "chung" chỉ khi **chưa gắn vào mục nghị sự nào**. Gắn vào mục thì chuyển khỏi nhóm chung; gỡ liên kết cuối thì quay lại. Tải thẳng vào mục chỉ nằm dưới mục đó. Người review từng ngạc nhiên. Từ 02/11 code tính "chung" **chỉ theo mục nghị sự**: tài liệu gắn biểu quyết mà không gắn mục vẫn hiện ở cả nhóm Biểu quyết lẫn Tài liệu chung (`MeetingDocumentService.ListAsync`; `docs/03-api/11-tai-lieu-cuoc-hop.md` ghi rõ) — xem điểm 7b | **BR-280** và **UC-33b**: nói rõ quy tắc này, và nói rõ tài liệu của biểu quyết thì sao |
| 7 | Khác AI-eCabinet, **giữ có chủ ý**: (a) gắn trùng hoặc gỡ cái chưa gắn: AI-eCabinet trả 204 và bỏ qua, ta trả `409 MSG243` và `404 MSG246`, UI hiện thông báo dữ liệu cũ; (b) tài liệu chỉ gắn vào biểu quyết: AI-eCabinet **không** tính là chung, từ 02/11 (SCRUM-96) ta **vẫn** tính là chung, nên một tài liệu hiện ở cả hai nhóm. PR #235 đã ghi hành vi này vào UC-32a AF-1 và Report 3 §7.3 | (a) UC-33b/33d EX: kiểm tra đã có MSG243/MSG246. (b) **BR-280** vẫn nguyên văn cũ: thêm câu "tài liệu chỉ gắn biểu quyết vẫn là tài liệu chung khi chưa gắn mục" cho khớp UC-32a AF-1. Muốn đổi hành vi cho giống AI-eCabinet thì phải hỏi PO trước, không tự sửa docs |
| 8 | **Kẹp giấy dùng chung:** từ SCRUM-96 (main 02/11) `AttachMenu` là một component cho cả mục nghị sự (UC-33b) và nháp biểu quyết (UC-76a), ba nguồn như nhau (BR-304). Nút hiện ở tab Nội dung (mục nghị sự, `actions.canLinkDocuments`) và tab Biểu quyết (nháp, `actions.canAttachDocuments`), cả Chủ toạ và Thư ký | **UC-76a** Normal Flow/điểm vào: bước 1 vẫn ghi "chooses Add document", cũng không có kẹp giấy. **UC-33b** thêm một câu "cùng menu với nháp biểu quyết (BR-304)". Đặc tả màn hình: màn tab Biểu quyết cần kẹp giấy trên thẻ nháp |

## 3. Việc BA, theo thứ tự

- [ ] UC-23a, UC-23b: bỏ chọn tài liệu khỏi form (điểm 1), thêm câu giữ liên kết khi sửa (điểm 3).
- [ ] UC-33b: điểm vào kẹp giấy và 3 nguồn (điểm 2), Chủ toạ được gắn (điểm 4), quy tắc tài liệu chung (điểm 6).
- [ ] UC-76a: điểm vào kẹp giấy dùng chung (điểm 8).
- [ ] BR-280: câu "tài liệu chung = chưa gắn vào mục nào" (điểm 6) và câu cho tài liệu của biểu quyết, **sau khi PO chốt** (điểm 7b).
- [ ] UC-01, UC-02 POST (và UC-03 nếu cùng): trang đích sau đăng nhập là lịch (điểm 5).
- [ ] Sequence UC-23a, UC-23b: bỏ `documentIds` khỏi tham số hoặc chú thích "do UC-33b" (điểm 1). Xuất lại PNG.
- [x] **Wireframe: ĐÃ CÓ** (v11, 02/11, commit bcadf40) — kẹp giấy ở tab Nội dung, hộp Sửa nội dung bỏ phần tài liệu, hộp gắn tài liệu
      3 nguồn có "Trong cuộc họp", tab Tài liệu có nhóm Biểu quyết. Không cần vẽ lại.
- [x] **Report 3 §7.3, §7.4 và UC-32a AF-1, UC-32c: PR #235 ĐANG MỞ đã sửa** (gumarr, 03/11, chưa merge) — kẹp giấy trên khối mục,
      hộp thêm tài liệu 3 nguồn, nhóm Biểu quyết, câu "general gồm cả tài liệu biểu quyết". Đừng làm lại; khi #235 merge thì chỉ rà.
- [ ] **CÒN LẠI CỦA ĐIỂM 2 — ĐẶC TẢ MÀN HÌNH TRONG REPORT 3**, chép theo wireframe v11 (phần §7.3/§7.4 đã có ở #235):
      - §5.1 tab Nội dung (FE-05): câu "Edit Agenda Item"/"Row action" còn nói sửa "document links"; thêm nút kẹp giấy trên dòng và
        ghi Chủ toạ cũng thấy nút này;
      - §5.2 hộp Sửa/Thêm nội dung: bỏ "checklist of the Meeting's documents" và ô Documents;
      - hộp thêm tài liệu: thêm ba trạng thái chưa ai ghi — "Đang tải…" khi đọc danh sách tài liệu cuộc họp, lỗi kèm "Thử lại",
        "Cuộc họp chưa có tài liệu." khi danh sách rỗng;
      - chụp lại ảnh màn hình từ wireframe v11.
- [ ] Sync Report 3: `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`.
- [ ] Change Log Report 3.
- [ ] Ghi kết quả vào Jira nếu có ticket; PR này không có ticket nên báo lại trong PR #201 hoặc cho Lộc.

## 4. Hỏi PO / Hải

- **Trang đích sau đăng nhập là lịch** (điểm 5): PO đã chốt chưa, hay chỉ là yêu cầu trực tiếp của người dùng. Nếu chưa chốt thì UC-01/02 ghi
  "NEEDS REVIEW PO".
- **Chủ toạ gắn được tài liệu** (điểm 4): có đúng ý PO không, vì Chủ toạ không sửa được mục.
- **Tài liệu gắn biểu quyết có nằm trong "Tài liệu chung" không** (điểm 7b): code 02/11 đang tính là chung (hiện ở cả hai nhóm) và
  PR #235 đã ghi đúng như vậy vào UC-32a AF-1; AI-eCabinet thì không. Hỏi PO có đồng ý giữ hướng này không trước khi viết vào BR-280.

## 5. Việc không phải của BA

- `editor-options` vẫn trả `documents` dù FE không đọc nữa (giữ để tương thích API).
- `SkillMirrorTests` fail trong PR là do skill cá nhân chỉ có trên máy tác giả, không phải lỗi docs.

## 6. Đã ai sửa chưa — kiểm lại 03/11/2026 (sau merge `main` abea1f3)

Kết luận: **trên `main` chưa ai sửa tài liệu yêu cầu**; wireframe thì đã vẽ (02/11) và **PR #235 đang mở đã sửa trước một phần** (xem
bảng dưới). Kiểm bằng cách đọc bản sinh ra từ Report 3 và các file sơ đồ trên `main` hôm nay:

| Chỗ cần sửa | Trên `main` | Thấy gì / ghi chú |
|---|---|---|
| UC-23a, UC-23b | chưa | `05-use-case-specifications.md`: POST-2 "any selected documents" (dòng 651), NF-2 "selects available Meeting documents" (652); UC-23b Description/NF-2 vẫn "document associations for editing" (664, 667) |
| UC-33b | chưa | dòng 946–950: NF-1 vẫn "selects an Agenda Item and chooses Add document", không có kẹp giấy |
| UC-76a | chưa | dòng 1735: NF-1 "selects a Draft Vote and chooses Add document", không có kẹp giấy |
| UC-32a AF-1, UC-32c | chưa | **PR #235 sửa**: AF-1 nói rõ nhóm Biểu quyết và "general gồm cả tài liệu của biểu quyết"; UC-32c bỏ phần ghi nhận "đọc tới trang cuối" (chỉ còn xác nhận đã đọc ở UC-74) |
| BR-280 | chưa | `06-business-rules.md` dòng 252: nguyên văn cũ, chưa có câu về biểu quyết; #235 chỉ sửa BR-215 |
| UC-01, UC-02 | chưa | dòng 221 (UC-01 POST-3) và 236 (UC-02 POST-4): vẫn "the appropriate authenticated page"; code vẫn vào lịch (`App.tsx` dòng 77: `/` → `/calendar`) |
| Sequence UC-23a/UC-23b | chưa | `agenda-management/UC23a_*.txt` và `UC23b_*.txt` dòng 18 vẫn có `documentIds`; PNG giữ nguyên |
| Report 3 §5.1, §5.2 (FE-05) | chưa | dòng 2903, 2911, 2934 (sửa "document links"); 2945, 2955, 2969 (checklist tài liệu trong hộp). #235 không đụng hai mục này |
| Report 3 §7.3, §7.4 (FE-07) | chưa | **PR #235 sửa**: bỏ "There is no paperclip menu" (dòng 3235, 3247), thêm kẹp giấy, 3 nguồn, nhóm Biểu quyết, tab của hộp thêm tài liệu. Chưa có 3 trạng thái "Đang tải…", lỗi + "Thử lại", "Cuộc họp chưa có tài liệu." |
| Change Log Report 3 | chưa | không có dòng nào cho PR #201; #235 có dòng riêng của nó |
| Wireframe v11 + ảnh chụp | **rồi** | `TrustCabinet-wireframe-v11.html`: mục nghị sự có kẹp giấy (959), hộp Sửa nội dung không có phần tài liệu (1224), hộp gắn tài liệu 3 nguồn có "Trong cuộc họp" (1298), tab Tài liệu có nhóm Biểu quyết (905) |

PR #235 (`fix/documents-vote-group-guest-comments-e2e-followups`, gumarr, head f340710, 03/11, chưa merge) là PR của PR #224: nó sửa
thêm Report 3 §7.3/§7.4, UC-32a AF-1, UC-32c, `docs/03-api/11`, sequence UC-32a/32c/33b/76a và thêm note 27
(`27-pr224-tai-lieu-sau-e2e.md`, LocHD). Vì vậy: **đừng sửa lại những chỗ đó trong note này**; khi #235 merge thì rà lại một lượt, còn
UC-23a/23b, UC-33b, UC-76a, BR-280, UC-01/02, sequence UC-23a/23b, Report 3 §5.1/§5.2 là còn trống.

Ngoài ra `main` đã đổi code so với lúc viết note, BA đừng chép lại mô tả cũ trong PR #201:

- Kẹp giấy không còn nằm trong `AgendaPanel`: `frontend/src/features/documents/AttachMenu.tsx` dùng cho cả mục nghị sự
  (`AgendaPanel.tsx` dòng 159, cờ `canLinkDocuments`) và nháp biểu quyết (`VotingPanel.tsx` dòng 169, cờ `canAttachDocuments`).
- Tab Tài liệu trả thêm `votes` (UC-32a AF-1, SCRUM-96); "chung" tính theo mục nghị sự
  (`MeetingDocumentService.ListAsync` dòng 46: `ItemIds.Count == 0`), không trừ tài liệu của biểu quyết.
