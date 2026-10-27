# Bàn giao FE-07 cho Lộc — phần tài liệu chưa code

**Ngày:** 27/10/2026 (rà soát lại cùng ngày). **Người viết:** KhanhPG. **Nguồn:** Jira SCRUM-21/22/23/25/58/91/92/96/98/107/110/111/112
đọc ngày 27/10/2026; Report 3 (Markdown) mục 7.2–7.7; catalogue MSG; code `AgendaEndpoints.cs`, `AgendaService.cs`,
`AgendaRepository.cs`, `MeetingDocumentEndpoints.cs`, `DocumentConfiguration.cs`; `docs/06-sequencediagram/document-management/`;
`docs/07-classdiagram/`; bản đồ UC–BR–MSG sinh bằng `scripts/trace_br.py`.

Note có hai phần:

- **Phần A (mục 1–5)** cho Lộc: hiện trạng, chỗ lệch, ticket và thứ tự code.
- **Phần B (mục 6–7)** cho KhanhPG: kế hoạch vẽ sequence và class diagram (SCRUM-91, SCRUM-92), chia thành bước nhỏ để giao
  agent, mỗi bước có đầu vào, nội dung cần vẽ, điều kiện xong và checklist review.
- **Phần C (mục 10)** cho KhanhPG: mọi việc tài liệu FE-07 còn lại ngoài sequence và class diagram — BA Final Review các
  ticket đã code, màn hình Report 3 và wireframe, screen flow, ERD — để không sót khi đóng FE-07.

**Phân công:**

| Người | Làm | Không làm |
|---|---|---|
| Lộc | Code BE + FE của SCRUM-23, 110, 111, 112. Trong **cùng commit** với code: contract `docs/03-api/` cho endpoint mới, dòng catalogue MSG và "Mã tiếp theo" (`MessageCodeCatalogueTests` bắt buộc), test. Ghi comment Jira khi code lệch ticket hoặc đặc tả. Hỏi PO các ý ở mục 5 | Sequence, class diagram, Report 3 |
| KhanhPG | Note này; Jira bước 0 (gán ticket, comment SCRUM-112); sequence SCRUM-91 và class diagram SCRUM-92 theo mục 7; sau khi Lộc merge từng ticket, sửa diagram PROPOSED thành as-built theo code | Code, `docs/03-api/`, catalogue MSG |

[DEVELOPMENT_WORKFLOW](../DEVELOPMENT_WORKFLOW.md) để BE viết sequence cơ bản trước khi code. Ở FE-07, nhóm thống nhất
KhanhPG vẽ thay (SCRUM-91/92 đã gán KhanhPG), nên Lộc code theo sequence PROPOSED ở mục 7. Cần làm xong ít nhất bước 1–3
trước khi Lộc bắt đầu ticket tương ứng; lệch so với sequence thì Lộc ghi vào Jira, KhanhPG cập nhật diagram ở BA Final Review.

## 1. Hiện trạng

| Nhóm chức năng | UC | Ticket | Trạng thái Jira | Code | Sequence | Việc còn thiếu |
|---|---|---|---|---|---|---|
| Tải tệp từ máy lên cuộc họp | UC-33a AF-1 | SCRUM-107 | BA Final Review | Đã merge | UC33a (AF-1) | Không |
| Chọn tài liệu từ kho để thêm vào cuộc họp | UC-33a luồng chính | **Chưa có** | — | Chưa code | **Chưa có** | Ticket, sequence |
| Xem danh sách, đọc tệp | UC-32a, 32c | SCRUM-21, 22 | BA Final Review / Backend | Đã merge | UC32a, UC32c | Không |
| Gắn tài liệu vào mục nghị sự | UC-33b | SCRUM-112 | To Do | Tạm qua sửa mục nghị sự, chỉ Thư ký (mục 2.2) | UC33b (PROPOSED) | Chức năng riêng theo đặc tả, Chủ toạ và Thư ký (đã chốt (b)) |
| Gỡ tài liệu khỏi một mục nghị sự | UC-33d | SCRUM-112 | To Do | Tạm qua sửa mục nghị sự, chỉ Thư ký (mục 2.2) | UC33d (PROPOSED) | Chức năng riêng theo đặc tả, Chủ toạ và Thư ký (đã chốt (b)) |
| Gỡ tài liệu khỏi cuộc họp | UC-33c | SCRUM-112 | To Do | Chưa code | **Chưa có** | Code, sequence |
| Bình luận tài liệu | UC-35 | SCRUM-23 | Backend | Chưa code | UC35 | Code. Ticket đủ tiêu chí, doc đã xong |
| Xác nhận đọc tài liệu biểu quyết | UC-74 | SCRUM-96 (Huy) | To Do | Chưa code | UC74 | Code, thuộc SCRUM-96 |
| Thư mục kho cá nhân | UC-72a–d | SCRUM-110 | To Do, chưa gán | Chưa code | **Chưa có** | Code, sequence |
| Tài liệu trong kho cá nhân | UC-34, 36, 71a–c | SCRUM-111 | To Do, chưa gán | Chưa code | **Chưa có** | Code, sequence |
| Class diagram FE-07 phần kho, gắn/gỡ | tất cả UC trên | SCRUM-92 (KhanhPG) | To Do | — | — | Vẽ sau khi có sequence |

## 2. Chỗ lệch đã phát hiện

### 2.1 Đã xử lý

1. **UC-74 thuộc SCRUM-96, không phải SCRUM-74.** SCRUM-74 là subtask sequence của FE-04. Tiêu đề SCRUM-96 đã ghi "xác nhận
   đã đọc (UC-74)", nên không cần ticket riêng. README sequence của `document-management` cũng đã đổi ticket UC-74 sang SCRUM-96.
2. **UC-33d đã có ticket.** Nằm trong SCRUM-112 (58c).
3. **SCRUM-58 đã tách** thành SCRUM-110 (58a), SCRUM-111 (58b), SCRUM-112 (58c), đều là subtask của SCRUM-58.

### 2.2 Còn mở

1. **Luồng chính UC-33a (chọn tài liệu từ kho để thêm vào cuộc họp) chưa ticket nào nhận.** SCRUM-107 chỉ làm AF-1 (tải từ
   máy). README sequence ghi luồng chính "thuộc SCRUM-58", nhưng 58a/b/c đều không có nó. Đề xuất: thêm vào SCRUM-112 và đổi
   tên thành "Gắn tài liệu từ kho và gỡ tài liệu của cuộc họp (UC-33a luồng chính, UC-33b AF-2, UC-33c, UC-33d)". Phụ thuộc
   SCRUM-111 vì cần tài liệu trong kho. MSG: 161, 162, 165, 166, 172, 174, 249.
2. **UC-33b và UC-33d đang chạy qua API sửa mục nghị sự, không qua endpoint riêng như đặc tả.**
   `PUT /api/meetings/{id}/agenda/{itemId}` nhận `documentIds` và `AgendaRepository.AssociateAsync` thay cả tập liên kết của mục
   (thêm cái mới, xoá cái không còn). Như vậy:
   - gắn (UC-33b luồng chính, chọn từ tài liệu cuộc họp) và gỡ khỏi một mục (UC-33d) **đã dùng được**;
   - lỗi trả về là mã của agenda (`MSG61`, `MSG66`, `MSG67`, `MSG69`), không phải mã đặc tả (`MSG238`–`MSG247`, `MSG170`).
     `documentIds` sai chỉ trả lỗi trường "Tài liệu không hợp lệ.";
   - chỉ **Thư ký** sửa được mục nghị sự (`MSG67` 403 khi thiếu Secretary), và cuộc họp đã khoá thì bị chặn (`MSG67` 409).
     Đặc tả UC-33b/33d cho cả **Chủ toạ** và chỉ chặn khi cuộc họp đã kết thúc (BR-184, BR-185, BR-279);
   - UC-33b AF-1 (tải từ máy rồi gắn luôn) và AF-2 (chọn từ kho rồi gắn luôn) chưa có.

   **Đã chốt 27/10/2026 (BA): phương án (b) — làm theo đặc tả.** Lộc làm chức năng gắn/gỡ riêng trong SCRUM-112:
   - Chủ toạ **và** Thư ký hiện tại đều gắn/gỡ được, đến khi cuộc họp kết thúc (BR-184, BR-185, BR-279);
   - UC-33b có đủ ba nguồn: tài liệu cuộc họp, tải từ máy (AF-1), chọn từ kho (AF-2), lưu tất cả hoặc không (BR-304);
   - UC-33d gỡ đúng một liên kết, tài liệu vẫn ở cuộc họp (BR-280);
   - dùng mã đặc tả `MSG238`–`MSG247`, `MSG170`, `MSG174` (và `MSG164`/`MSG165`/`MSG166`/`MSG249` cho AF-1/AF-2);
   - thêm contract `docs/03-api/` và đổi trạng thái các mã trong catalogue cùng commit code.
   API agenda có giữ `documentIds` hay không là việc của Lộc; nếu giữ thì vẫn chỉ Thư ký, không thay chức năng mới.
   Sequence UC-33b/33d ở mục 7 bước 3 vẽ lại **PROPOSED theo đặc tả**.
3. **Hạn mức dung lượng kho cá nhân** chưa chốt (ADR-0020 ghi "remain undecided").
4. **Quyền đọc phần chưa gắn trong kho của người đã rời tổ chức** chưa chốt.
5. **SCRUM-110/111/112 chưa gán người.**

## 3. Ticket và thứ tự code cho Lộc

Thứ tự đề xuất: SCRUM-23 chạy độc lập ngay (sequence UC35 đã có). Trong SCRUM-58: 110 → 111 → 112 (phần sau dùng lại phần
trước), mỗi ticket bắt đầu khi KhanhPG đã xong bước sequence tương ứng ở mục 7 (110 ← bước 1, 111 ← bước 2, 112 ← bước 3).
Nhánh code của Lộc đặt theo quy ước của DEVELOPMENT_WORKFLOW, tách khỏi hai nhánh docs ở mục 4.

| Ticket | Phạm vi | UC | BR | MSG | Phụ thuộc |
|---|---|---|---|---|---|
| SCRUM-110 (58a) | Thư mục kho cá nhân | UC-72a, 72b, 72c, 72d | 191, 192, 204, 219–223, 227, 228, 242 | 175, 179, 187–198 | Không |
| SCRUM-111 (58b) | Tài liệu trong kho cá nhân | UC-34, 36, 71a, 71b, 71c | 183, 186, 187, 204, 214–216, 219, 224, 225, 229, 232, 242, 245, 251, 276, 286 | 164, 175–181, 183–186 (`MSG182` ngừng dùng) | SCRUM-110 |
| SCRUM-112 (58c) | Gắn từ kho, gắn/gỡ mục nghị sự, gỡ khỏi cuộc họp | UC-33a luồng chính, UC-33b AF-1/AF-2, UC-33c, UC-33d | 184, 185, 193, 194, 195, 197, 209, 210, 211, 212, 231, 276–281, 284, 286, 304 | 161, 162, 164–167, 169–172, 174, 238–249 | SCRUM-111 cho phần chọn từ kho; UC-33c/33d làm trước được |

### SCRUM-110 — Thư mục kho cá nhân
- **Nghiệm thu:** tên không trống, không bắt duy nhất (BR-220, BR-221); lồng tối đa 5 cấp (BR-191); không chuyển vào chính nó
  hay thư mục con (BR-222); xoá chỉ khi rỗng, cả tài liệu lẫn thư mục con (BR-223). FK `Document.FolderId` đã `Restrict` ở
  `DocumentConfiguration`, nhưng service vẫn phải kiểm trước để trả `MSG196` thay vì lỗi DB. Chỉ chủ sở hữu thấy và sửa thư
  mục của mình, thư mục người khác trả `MSG175` như thư mục không tồn tại (BR-242). Thư mục tự tạo từ UC-33a (BR-192, tên
  `<tên cuộc họp> – dd/mm/yyyy`) hiện và sửa được như mọi thư mục; đổi tên hay chuyển thì vẫn dùng lại được (BR-227); xoá rồi
  thì lần tải lên sau tạo lại (BR-228).
- **Endpoint gợi ý:** `GET/POST /api/folders`, `PATCH /api/folders/{id}` (đổi tên hoặc `parentId`), `DELETE /api/folders/{id}`.
  Entity `Folder` đã có từ SCRUM-107, không cần migration mới trừ khi thêm index.

### SCRUM-111 — Tài liệu trong kho cá nhân
- **Nghiệm thu:** dùng lại kiểm tra tệp và `FileStorage` của SCRUM-107 (PDF/DOCX, đuôi khớp nội dung, ≤ 50 MB mỗi tệp,
  tất cả hoặc không – BR-286). Đặc tả UC-36 không ghi giới hạn 10 tệp / 200 MB; nếu áp như UC-33a (`MSG288`) thì ghi lệch vào
  ticket. Xoá chỉ xoá dòng kho, cuộc họp đang dùng vẫn đọc bình thường (BR-186, BR-232). Đổi tên không đổi tên đã chép vào cuộc
  họp (BR-276). Màn kho hiện "Đang dùng trong n cuộc họp" chỉ với cuộc họp người dùng được xem (BR-251). Tìm theo tên, lọc theo
  loại tệp (UC-34 AF-1). Guest không có kho (BR-204).
- **Endpoint gợi ý:** `GET /api/documents?folderId=&q=&type=`, `POST /api/documents/upload`, `PATCH /api/documents/{id}`
  (đổi tên hoặc `folderId`), `DELETE /api/documents/{id}`.

### SCRUM-112 — Gắn và gỡ tài liệu của cuộc họp
- **Nghiệm thu:** chỉ Chủ toạ hoặc Thư ký hiện tại của chính cuộc họp (BR-184); chặn sau khi cuộc họp kết thúc (BR-185);
  trạng thái mục nghị sự không khoá liên kết (BR-279). Gỡ khỏi cuộc họp là đặt `RemovedAt`/`RemovedById` và bỏ mọi liên kết
  mục nghị sự; góp ý giữ làm lịch sử nhưng không hiện (BR-281, BR-284); thêm lại là một lần thêm mới, không lấy lại góp ý cũ
  (BR-212). Gỡ khỏi một mục không gỡ khỏi cuộc họp (BR-280). Chọn từ kho: tài liệu phải của người thao tác và chưa có trong
  cuộc họp (BR-193, BR-209), sai thì `MSG249` / `MSG165`.
- **Endpoint gợi ý:** `POST /api/meetings/{id}/documents` (body `documentIds` từ kho), `DELETE
  /api/meetings/{id}/documents/{meetingDocumentId}`. Gắn/gỡ mục nghị sự: theo quyết định ở mục 2.2 ý 2.
- **Hub:** phát `documentsChanged` sau khi thêm hoặc gỡ, như SCRUM-21 (kèm `eventId`, `occurredAt`).

## 4. Nhánh

Chỉ dùng **hai nhánh**. Nhánh 1 cắt từ `origin/main`, nhánh 2 cắt từ nhánh 1 (xem mục 7 bước 0):

| Nhánh | Nội dung | Ticket | PR |
|---|---|---|---|
| `docs/scrum-58-ban-giao-kho-tai-lieu` (nhánh hiện tại) | Note này, README notes, sửa ticket UC-74 trong README sequence, script trace và `uc-map.html` đã có | SCRUM-58 | PR 1 |
| `docs/scrum-91-92-fe07-kho-tai-lieu` | Toàn bộ sequence (SCRUM-91) và class diagram (SCRUM-92) của mục 7, làm tuần tự | SCRUM-91, SCRUM-92 | PR 2 |

Lý do gộp: các bước diagram cùng sửa `README.md` của `document-management` và file draw.io gộp
`TrustCabinet_Sequence_Diagrams.drawio`. File draw.io là XML, nhiều nhánh cùng sửa thì merge rất dễ vỡ. Làm tuần tự trên một
nhánh, **mỗi bước một commit**, review xong bước trước mới chạy bước sau, thì không có conflict và vẫn review được từng phần
qua `git show <commit>`.

## 5. Việc cần chốt với PO / BA

1. ~~UC-33b / UC-33d: giữ API agenda hay thêm endpoint riêng~~ — đã chốt 27/10/2026: phương án (b), Chủ toạ và Thư ký đều gắn/gỡ được.
2. Đưa UC-33a luồng chính vào SCRUM-112 (mục 2.2 ý 1).
3. Hạn mức dung lượng kho cá nhân.
4. Quyền đọc kho của người đã rời tổ chức.
5. UC-36 có áp giới hạn 10 tệp / 200 MB mỗi lần như UC-33a không.

---

## 6. Kế hoạch diagram (Phần B) — quy tắc chung cho mọi bước

Áp dụng cho agent chạy từng bước ở mục 7.

**Đọc trước, theo thứ tự:** `AGENTS.md`; `docs/06-sequencediagram/README.md` (đặc biệt §2 một request từ Actor, §3 lifeline,
§5 message, §7 BR, §8 alt, §12 template, §14 checklist, §15 tên file); `docs/06-sequencediagram/document-management/README.md`;
`UC33a_UploadDocumentsToMeeting.txt` làm mẫu style; Report 3 mục của UC đang vẽ; catalogue
`docs/03-api/00-danh-muc-ma-thong-bao.md` cho chữ của từng MSG.

**Quy tắc vẽ:**
- Source là SequenceDiagram.org (`.txt`), tên file `UC<số><chữ>_<TênTiếngAnh>.txt` + `.png` cùng tên. Xuất ảnh bằng
  `node scripts/export-diagrams.js docs/06-sequencediagram/document-management`.
- Lifeline đặt tên giống UC33a: `actor`, `boundary` màn hình, `control ":… Controller"`, `control ":… Service"`,
  `participant ":… Repository"` / `":Meeting Access Policy"` / `":Object Storage (R2)"`, `database ":Database"`.
- Một request từ Actor vào hệ thống. UC có hộp thoại xác nhận (UC-33c, 71a, 72d) thì bắt đầu từ lúc bấm xác nhận.
- Thứ tự `alt` theo đúng thứ tự kiểm ở bước của đặc tả; mỗi nhánh ghi `- EX-n` và MSG đầy đủ chữ tiếng Anh trong catalogue.
- Ghi BR trong ngoặc ở message kiểm tra, như `(BR-191, BR-219)`.
- UC chưa có code: lifeline và tên hàm là **PROPOSED**, ghi một dòng trong README thư mục. UC đã có code: vẽ **as-built**
  theo code và ghi chỗ lệch đặc tả trong README.
- MSG nào đặc tả có mà catalogue không có, hoặc ngược lại: liệt kê ở cuối báo cáo bước, **không** tự sửa catalogue hay Report 3.
- Thêm dòng vào bảng README thư mục và sửa câu "Các UC tài liệu khác … chưa có sequence" cho đúng sau mỗi bước.
- Bước 1–3 **không** sửa file draw.io gộp; bước 4 làm một lần.
- Không commit khi KhanhPG chưa bảo. Khi được bảo, commit message tiếng Việt theo lịch sử repo, mỗi bước một commit.

**Báo cáo cuối mỗi bước gồm:** danh sách file tạo/sửa; bảng UC → EX → MSG đã vẽ; chỗ lệch giữa đặc tả, catalogue và code;
ảnh PNG đã mở xem lại chưa.

## 7. Các bước

### Bước 0 — Chuẩn bị nhánh và Jira (KhanhPG làm tay, không giao agent)

1. Commit note trên nhánh 1, push và mở PR 1.
2. Cắt nhánh 2 **từ nhánh 1** để có sẵn note và README đã sửa:
   `git switch -c docs/scrum-91-92-fe07-kho-tai-lieu docs/scrum-58-ban-giao-kho-tai-lieu`.
   Mở PR 2 với base là nhánh 1, để diff chỉ hiện phần diagram. PR 1 merge và nhánh 1 bị xoá thì GitHub tự đổi base PR 2
   sang `main`. Nhánh 1 có commit mới thì `git merge docs/scrum-58-ban-giao-kho-tai-lieu` vào nhánh 2.
3. Jira: comment SCRUM-112 về mục 2.2 ý 1–2; gán SCRUM-110/111/112 cho Lộc; chuyển SCRUM-91 sang In Progress.

### Bước 1 — Sequence thư mục UC-72a–d (SCRUM-91)

**Đầu vào riêng:** Report 3 mục 7.7; `Folder` entity (`backend/TrustCabinet.Domain/Entities/Folder.cs`).
**Lifeline:** `actor "Internal User"`, `boundary "Personal Library Screen"`, `control ":Folder Controller"`,
`control ":Folder Service"`, `participant ":Folder Repository"`, `database ":Database"`. Tất cả PROPOSED.

| File | Luồng chính | Nhánh `alt` theo thứ tự |
|---|---|---|
| `UC72a_CreateFolder` | Kiểm chủ sở hữu, thư mục cha, tên không trống, độ sâu ≤ 5 (BR-191, BR-219–221, BR-242) → tạo → `MSG187` | EX-2 thư mục cha không còn/không phải của mình `MSG179`; EX-1 tên trống `MSG188`; EX-3 quá 5 cấp `MSG189`; EX-4 lỗi lưu `MSG190` |
| `UC72b_RenameFolder` | Kiểm chủ sở hữu, còn tồn tại, tên không trống, cho trùng tên (BR-219–221, BR-242) → lưu tên → `MSG191` | EX-1 không còn `MSG175`; EX-2 tên trống `MSG188`; EX-3 lỗi lưu `MSG192` |
| `UC72c_MoveFolder` | Kiểm chủ sở hữu thư mục và đích, đích không phải chính nó/con cháu, cả cây sau khi chuyển ≤ 5 cấp (BR-191, BR-219, BR-222, BR-242) → chuyển cả cây → `MSG194`. Self-call tính độ sâu cây con | EX-1 thư mục không còn `MSG175`; EX-2 đích không còn `MSG179`; EX-3 vào chính nó/con cháu `MSG193`; EX-4 quá 5 cấp `MSG189`; EX-5 lỗi lưu `MSG195`. AF-1 (chuyển ra gốc) ghi trong câu của request, không thêm `alt` |
| `UC72d_DeleteFolder` | Bắt đầu từ lúc xác nhận. Kiểm chủ sở hữu, còn tồn tại, không có tài liệu hay thư mục con (BR-219, BR-223, BR-242) → xoá → `MSG197` | EX-1 không còn `MSG175`; EX-2 không rỗng `MSG196`; EX-3 lỗi lưu `MSG198`. BR-228 (thư mục cuộc họp bị xoá sẽ được tạo lại ở lần tải lên sau) ghi ở README thư mục, không vẽ |

**Xong khi:** 4 cặp `.txt`/`.png`, 4 dòng README, đủ mọi EX của đặc tả, không còn MSG nào lệch catalogue.

### Bước 2 — Sequence tài liệu trong kho UC-34, 36, 71a–c (SCRUM-91)

**Đầu vào riêng:** Report 3 mục 7.3, 7.5, 7.6; luồng AF-1 trong `UC33a_UploadDocumentsToMeeting.txt` (kiểm tệp, ghi dòng tệp lưu trữ,
đẩy R2, rồi ghi tài liệu trong một transaction; lỗi thì xoá object đã đẩy).
**Lifeline:** `actor "Internal User"`, `boundary "Personal Library Screen"`, `control ":Document Controller"`,
`control ":Document Service"`, `participant ":Folder Repository"`, `participant ":Document Repository"`,
`participant ":Object Storage (R2)"` (chỉ UC-36), `database ":Database"`. PROPOSED.

| File | Luồng chính | Nhánh `alt` theo thứ tự |
|---|---|---|
| `UC34_ViewPersonalLibrary` | Kiểm quyền kho (BR-204, BR-242) → lấy thư mục và tài liệu của người dùng, kèm số cuộc họp người dùng được xem đang dùng tài liệu (BR-219, BR-251) → hiển thị | EX-2 thư mục đã chọn không còn `MSG175`; EX-1 không tải được `MSG176`; AF-2 kết quả rỗng là danh sách rỗng, không phải lỗi. AF-1 tìm/lọc ghi trong câu của request |
| `UC36_UploadToPersonalLibrary` | Kiểm thư mục đích (BR-242) → kiểm từng tệp PDF/DOCX ≤ 50 MB, đuôi khớp nội dung (BR-187, BR-214, BR-215) → ghi dòng `StoredFile` trước (ADR-0024 §4, như UC-33a) → đẩy R2 → ghi `Document` trong một transaction (BR-183, BR-216, BR-229, BR-286) → `MSG177` | EX-2 đích không còn `MSG179`; EX-1 tệp sai `MSG164`; EX-3 lỗi lưu, xoá object đã đẩy `MSG178` |
| `UC71a_DeleteLibraryDocument` | Bắt đầu từ lúc xác nhận. Kiểm chủ sở hữu, còn tồn tại (BR-242) → xoá dòng `Document`, không đụng `MeetingDocument` (BR-186, BR-232) → `MSG180` | EX-1 không còn `MSG175`; EX-2 lỗi lưu `MSG181` |
| `UC71b_RenameLibraryDocument` | Kiểm chủ sở hữu (BR-242) → lưu tên mới, không đổi `StoredFile` và tên đã chép vào cuộc họp (BR-183, BR-224, BR-276) → `MSG183` | EX-1 không còn `MSG175`; EX-2 lỗi lưu `MSG184` |
| `UC71c_MoveLibraryDocument` | Kiểm chủ sở hữu tài liệu và thư mục đích (BR-225, BR-242) → đổi `folderId` (BR-245) → `MSG185` | EX-1 tài liệu không còn `MSG175`; EX-2 đích không còn `MSG179`; EX-3 lỗi lưu `MSG186`. AF-1 chuyển ra gốc ghi trong câu của request |

Lưu ý UC-71b: đặc tả không có EX tên trống (BR-243 và `MSG182` đã bỏ). Không tự thêm.

**Xong khi:** 5 cặp `.txt`/`.png`, 5 dòng README, có ghi trong README rằng UC-36 dùng chung cách kiểm tệp và R2 với UC-33a AF-1.

### Bước 3 — Sequence gắn và gỡ UC-33a luồng chính, 33b, 33c, 33d (SCRUM-91)

**Đầu vào riêng:** Report 3 mục 7.2; mục 2.2 ý 2 của note này (đã chốt phương án (b)). Code agenda chỉ để biết hiện
trạng, không vẽ theo.

| File | Cách vẽ | Nội dung |
|---|---|---|
| `UC33a_UploadDocumentsToMeeting` (sửa file cũ) hoặc file mới `UC33a_AddDocumentsFromLibrary` | PROPOSED | Luồng chính chọn từ kho: kiểm quyền và trạng thái như AF-1 (BR-184, BR-195) → kiểm từng tài liệu thuộc người thao tác, còn tồn tại, chưa có trong cuộc họp (BR-193, BR-209) → tạo `MeetingDocument` chép tên (BR-197, BR-276) trong một transaction (BR-286) → `MSG161`. `alt`: EX-7 `MSG174`; EX-1 `MSG162`; EX-6 `MSG172`; EX-2 `MSG249`; EX-4 `MSG165`; EX-5 `MSG166`. Chọn một trong hai cách đặt file và ghi lý do trong README (quy tắc §2 một request: hai luồng là hai request khác nhau, nên tách file thì dễ đọc hơn) |
| `UC33b_AttachDocumentsToAgendaItem` | PROPOSED theo đặc tả (vẽ lại 27/10/2026, phương án (b)) | Actor Chủ toạ hoặc Thư ký, lifeline như UC33c. Một request, nguồn tài liệu nằm trong câu của request: tài liệu cuộc họp (luồng chính), tải từ máy (AF-1), chọn từ kho (AF-2). Kiểm truy cập, vai trò, chưa kết thúc, mục thuộc cuộc họp, tài liệu còn trong cuộc họp và chưa gắn (BR-184, BR-185, BR-277, BR-278, BR-279) → gắn tất cả hoặc không (BR-304) → `MSG238`. `alt`: EX-7 `MSG174`; EX-1 `MSG240`; EX-6 `MSG247`; EX-3 `MSG242`; EX-2 `MSG170`; EX-4 `MSG243`; EX-5 `MSG244`; AF-1/AF-2 thêm `MSG164`, `MSG166`, `MSG249`, `MSG165` |
| `UC33c_RemoveDocumentFromMeeting` | PROPOSED | Bắt đầu từ lúc xác nhận. Lifeline như UC33a. Kiểm truy cập, Chủ toạ/Thư ký, chưa kết thúc, tài liệu còn trong cuộc họp (BR-184, BR-185, BR-279) → đặt `RemovedAt`, `RemovedById`, xoá mọi `MeetingDocumentAgendaItem` của nó, giữ góp ý (BR-211, BR-212, BR-231, BR-197, BR-281, BR-284) → `MSG167`. `alt`: EX-5 `MSG174`; EX-2 `MSG169`; EX-1 `MSG248`; EX-3 `MSG170`; EX-4 `MSG171` |
| `UC33d_DetachDocumentFromAgendaItem` | PROPOSED theo đặc tả (vẽ lại 27/10/2026, phương án (b)) | Bắt đầu từ lúc xác nhận gỡ. Kiểm như bước 2 đặc tả (BR-184, BR-185, BR-277, BR-278, BR-279) → xoá đúng một liên kết, tài liệu vẫn ở cuộc họp (BR-280) → `MSG239`. `alt`: EX-7 `MSG174`; EX-1 `MSG241`; EX-6 `MSG247`; EX-3 `MSG242`; EX-2 `MSG170`; EX-4 `MSG246`; EX-5 `MSG245` |

**Xong khi:** 3–4 cặp `.txt`/`.png`, README ghi rõ UC nào as-built, UC nào PROPOSED, và bảng lệch đặc tả–code của UC-33b/33d.

### Bước 4 — Tab trong file draw.io gộp (SCRUM-91)

**Đầu vào riêng:** `docs/06-sequencediagram/README.md` mục "Cấu trúc file và cách đồng bộ" và "Style chung của file draw.io
gộp"; `DRAWIO_EDITING_RULES.md`; skill `drawio-skill`.
**Việc:** thêm một tab cho mỗi UC của bước 1–3 vào `TrustCabinet_Sequence_Diagrams.drawio`, cùng cách tab UC-74 (bộ chuyển
Mermaid của draw.io, style chuẩn hoá); xuất ảnh vào `drawio-png/`. Không đổi tab đã có, không sinh lại cả file.
**Xong khi:** số tab mới bằng số `.txt` mới; mở từng tab xem không vỡ layout; README `document-management` ghi đã có tab.

### Bước 5 — Class diagram FE-07 (SCRUM-92)

**Đầu vào riêng:** `docs/07-classdiagram/README.md` (cả mục 2 nguồn và mục 3 quy trình); file
`feature-07-document-management-class-diagram.drawio`; toàn bộ `.txt` mới của bước 1–3; `DRAWIO_EDITING_RULES.md`;
skill `drawio-skill`.
**Việc, sửa có chọn lọc, giữ ID cũ:**
1. Boundary mới `PersonalLibraryScreen`: `viewLibrary(folderId?, query?, type?)`, `createFolder`, `renameFolder`, `moveFolder`,
   `deleteFolder`, `uploadToLibrary`, `renameDocument`, `moveDocument`, `deleteDocument`.
2. Control và service mới `FolderController`/`FolderService`, `DocumentController`/`DocumentService`, hàm lấy đúng từ
   message trong sequence bước 1–2.
3. Thêm lại `FolderRepository` (CRUD, `findSubtree`, tính độ sâu) và `DocumentRepository`, cả hai PROPOSED. Từ 27/10/2026
   diagram không còn `FolderRepository` vì code SCRUM-107 tìm/tạo thư mục cuộc họp ngay trong `MeetingDocumentRepository`
   (`findUploadFolder`, `addFolder`); nếu Lộc giữ cách đó thì vẽ theo code.
4. `MeetingDocumentsTab`/`MeetingDocumentController`/`MeetingDocumentService`: thêm `addLibraryDocuments`, `removeDocument`
   (UC-33a luồng chính, UC-33c); `MeetingDocumentRepository`: `markRemoved`, `existsInMeeting`.
5. UC-33b/33d (PROPOSED, phương án (b)): thêm `attachDocuments`, `detachDocument` vào `MeetingDocumentsTab`/`MeetingDocumentController`/
   `MeetingDocumentService`, và `addAgendaLinks`/`removeAgendaLink` vào `MeetingDocumentRepository`; không vẽ class agenda.
6. Quan hệ: `Folder` tự tham chiếu (parent 0..1 – children 0..*), `Folder` 0..1 — 0..* `Document`, `Document` 0..1 — 0..*
   `MeetingDocument`, `Document` * — 1 `StoredFile`.
7. Không gắn nhãn `PROPOSED` trên class diagram (BA yêu cầu 27/10/2026); phần chưa có code liệt kê ở §6d README class diagram.
8. Sửa tiêu đề diagram, dòng FE-07 trong bảng mục 1 và đoạn mô tả FE-07 của `docs/07-classdiagram/README.md`; xuất lại
   `.drawio.png`.

**Không làm:** quan hệ `DocumentComment.authorId → User` (để SCRUM-23); phần UC-74 (để SCRUM-96).
**Xong khi:** mỗi message trong sequence bước 1–3 có hàm tương ứng trên class diagram (đối chiếu hai chiều theo README
class diagram); PNG xuất từ đúng source; README cập nhật.

## 8. Checklist KhanhPG review sau mỗi bước

1. Đủ mọi EX và AF của đặc tả, đúng thứ tự kiểm.
2. Chữ MSG khớp catalogue từng ký tự; không có mã ngừng dùng (`MSG163`, `MSG168`, `MSG173`, `MSG182`).
3. BR ghi đúng ở bước kiểm, không ghi BR không có trong đặc tả UC đó.
4. As-built hay PROPOSED ghi rõ; as-built khớp code thật (mở file code đối chiếu).
5. Một request từ Actor; không có `loop` cho việc gửi lại; AF chỉ dùng khi UC vẫn thành công.
6. `.png` xuất từ đúng `.txt` hiện tại (mở ảnh xem).
7. README thư mục đã thêm dòng và sửa câu "chưa có sequence".
8. Diff chỉ chạm file của bước đó.
9. Chỗ lệch được liệt kê ở báo cáo, chưa tự sửa catalogue hay Report 3.

## 9. Độ phủ BR/MSG hiện có trong code (để Lộc biết phần nào đã có tag)

| UC | BR có code / tổng | BR có test |
|---|---|---|
| UC-33b | 7 / 15 | 3 |
| UC-33c | 2 / 9 | 1 |
| UC-33d | 3 / 6 | 1 |
| UC-34 | 0 / 4 | 0 |
| UC-36 | 7 / 9 | 4 |
| UC-71a / b / c | 0 / 4, 1 / 5, 0 / 4 | 0, 1, 0 |
| UC-72a / b / c / d | 0 / 6, 1 / 6, 1 / 6, 2 / 6 | 0 |
| UC-35 | 3 / 10 | 0 |
| UC-74 | 2 / 7 | 0 |

Số đếm bằng tag `BR-nn` trong comment, chỉ là gợi ý chỗ đã có; xem bản đồ chi tiết bằng
`python scripts/trace_br.py --html` rồi mở `docs/uc-map.html`. Phần UC-33b/33d có tag là nhờ code agenda (mục 2.2 ý 2).

## 10. Phần C — toàn bộ việc tài liệu FE-07 của BA (KhanhPG)

Rà ngày 27/10/2026 mọi loại tài liệu trong `docs/` có chạm FE-07. Bảng dưới là danh sách đầy đủ; mục 7 chỉ là phần diagram.

### 10.1 Bảng tổng

| # | Tài liệu | Hiện trạng | Việc | Khi nào |
|---|---|---|---|---|
| C1 | BA Final Review SCRUM-107 (UC-33a AF-1) | Jira đang ở BA Final Review | Bước 6 | **Ngay** |
| C2 | BA Final Review SCRUM-21 (UC-32a, UC-32c) | Jira đang ở BA Final Review | Bước 6 | **Ngay** |
| C3 | Sequence, class diagram phần kho, gắn/gỡ | Chưa có | Mục 7 bước 1–5 | Trước khi Lộc code từng ticket |
| C4 | Report 3 §III màn 7.1, 7.2, 7.4 (kho, "Từ kho của tôi") và menu ⋯ của 7.3 | Ghi "chờ SCRUM-58" | Bước 8, qua wireframe | Sau khi Lộc làm xong FE của ticket tương ứng |
| C5 | Screen flow `docs/12-screenflow` | Có 7.1–7.5, nhưng nhãn 7.5 còn "Versions" (đã bỏ phiên bản theo ADR-0018) | Bước 7 | Lúc nào cũng được |
| C6 | ERD hiện tại `docs/13-erd/hien-tai` | Snapshot cũ: còn `document_versions`, thiếu `folders`, `stored_files`, `meeting_documents`, `meeting_document_agenda_items` | Nhờ dev sinh lại từ database; thư mục này **không sửa tay** | Báo Lộc/leader, không phải việc BA |
| C7 | ERD mục tiêu, Chen, `02-design/02-erd.md` | Đã có đủ `Folder`, `Document`, `StoredFile`, `MeetingDocument`, `MeetingDocumentAgendaItem`, `DocumentComment` | Chỉ rà lại khi Lộc thêm cột hoặc bảng | Bước 9 |
| C8 | UC diagram `docs/14-uc-diagram` | Đã có UC-33 (gộp), 34, 36, 71, 72 | Không cần sửa | — |
| C9 | State machine | SM-19 DocumentComment đã có; thư mục, tài liệu kho không có vòng đời trạng thái | Không cần thêm | — |
| C10 | BA Final Review SCRUM-110, 111, 112, 23 | Chưa code | Bước 9, lặp cho từng ticket | Khi ticket sang BA Final Review |
| C11 | UC-74 (xác nhận đọc) | Sequence đã có; code thuộc SCRUM-96 | Bước 9 khi SCRUM-96 sang BA Final Review | Theo Huy |

**Không phải việc BA:** contract `docs/03-api/11-tai-lieu-cuoc-hop.md` và endpoint mới, catalogue MSG (Lộc, cùng commit code);
test report Report 5 (Tester); ERD hiện tại (sinh từ database).

**Cách sửa Report 3:** Report 3 sửa trong DOCX ở `docs/08-reports/source/`, rồi chạy pipeline sinh lại Markdown
(`scripts/sync_reports.py`, đã gộp pipeline ở commit `7e353de`). Không sửa thẳng file `.md`. Phạm vi mặc định: §I.4.2 danh
sách UC, §II đặc tả UC, §V.1 BR, §V.2 MSG; §III màn hình chỉ khi bước 8. Ảnh màn hình lấy từ wireframe
(`docs/wireframe/TrustCabinet-wireframe-vN.html` → `_tools` → `screens/*.png`), **không** chụp app đang chạy.

### 10.2 Bước 6 — BA Final Review SCRUM-107 và SCRUM-21 (làm ngay)

Hai ticket đã merge code và đang chờ BA. Làm SCRUM-107 trước (nhỏ hơn), cùng nhánh 2, mỗi ticket một commit.

**Agent làm (đọc code thay BA):**
1. Đọc mọi comment trên Jira SCRUM-107 / SCRUM-21 (BE, FE ghi thay đổi so với ticket).
2. Đọc contract `docs/03-api/11-tai-lieu-cuoc-hop.md`, catalogue MSG (`MSG156`–`MSG166`, `MSG172`, `MSG174`, `MSG288`) và code
   `MeetingDocumentEndpoints.cs` cùng service của nó.
3. Lập bảng đối chiếu theo từng bước của đặc tả (UC-33a AF-1 / UC-32a / UC-32c ở Report 3 mục 7.1–7.2): bước đặc tả →
   code làm gì → khớp / lệch / thiếu. Kèm BR và MSG.
4. Đối chiếu tương tự với sequence `UC33a`, `UC32a`, `UC32c` và class diagram FE-07.
5. **Chỉ báo cáo**, chưa sửa gì. Viết bằng ngôn ngữ nghiệp vụ, không cần tên class C#.

**KhanhPG quyết từng chỗ lệch:**
- Lệch đã ghi trên Jira và PO chấp nhận → sửa đặc tả, BR, MSG, diagram theo code.
- Lệch **không** ghi trên Jira → hỏi Lộc; không chấp nhận được thì mở lại ticket cho BE/FE, không sửa tài liệu cho khớp.
- Sửa Report 3 qua DOCX như trên; sửa sequence/class diagram như mục 6–7.

**Xong khi:** đặc tả, BR, MSG, sequence, class diagram khớp nhau và khớp code; comment tóm tắt lên Jira; chuyển ticket sang
bước tiếp theo theo DEVELOPMENT_WORKFLOW (Tester).

### 10.3 Bước 7 — Screen flow

Sửa nhãn màn 7.5 trong `docs/12-screenflow/TrustCabinet-screen-flow.drawio` bỏ "Versions"; rà cạnh nối 7.1/7.2 ↔ 7.3/7.4 có
đúng luồng "Từ kho của tôi" (UC-33a luồng chính) và mở viewer 7.5 không. Theo `DRAWIO_EDITING_RULES.md`, sửa có chọn lọc,
xuất lại PNG, sửa README thư mục nếu có ghi chú cũ.

### 10.4 Bước 8 — Màn hình Report 3 và wireframe (sau khi Lộc làm FE)

Với mỗi ticket 110/111/112 đã có FE chạy trên server test:
1. Sửa wireframe phiên bản mới nhất (`TrustCabinet-wireframe-v9.html` hoặc tạo v10 theo `docs/wireframe/README.md`) cho màn
   7.1, 7.2, 7.4 tab "Từ kho của tôi", menu ⋯ của 7.3, khớp bản đã chạy. Ghi dòng mới ở bảng phiên bản README wireframe.
2. Sinh lại ảnh `screens/screen-7_*.png` bằng `_tools`.
3. Sửa Report 3 §III mục 7.1–7.4: bỏ chữ "chờ SCRUM-58", mô tả trường và nút theo bản đã chạy, chèn ảnh mới.

### 10.5 Bước 9 — Vòng BA Final Review cho từng ticket còn lại

Lặp như bước 6 cho SCRUM-110, 111, 112, 23 (và SCRUM-96 phần UC-74), mỗi khi ticket sang BA Final Review. Thêm:
- đổi sequence và class diagram PROPOSED thành as-built;
- nếu Lộc thêm bảng hoặc cột, rà ERD mục tiêu, Chen và `02-erd.md`;
- bước 8 cho màn của ticket đó;
- cuối cùng sửa câu "chưa có sequence" / "một phần" trong README `06-sequencediagram/document-management` và
  `07-classdiagram` thành trạng thái đủ.

### 10.6 Thứ tự đề xuất cho KhanhPG

1. Bước 0 (Jira) → **Bước 6** (hai ticket đang chờ BA) → Bước 1 → Bước 2 → Bước 3 → Bước 7.
2. Bước 4, Bước 5 khi bước 1–3 đã review xong.
3. Bước 8, Bước 9 lặp theo nhịp Lộc merge.

FE-07 đóng phần tài liệu khi C1–C5, C7, C10, C11 đều xong và C6 đã báo dev.

## 11. Bàn giao BE cho Lộc — đã đủ tài liệu để code (27/10/2026)

Sequence và class diagram của mọi UC còn lại trong FE-07 đã vẽ xong (SCRUM-91, SCRUM-92). Lộc code theo thứ tự
**SCRUM-23 → SCRUM-110 → SCRUM-111 → SCRUM-112**. Mỗi ticket: đọc sequence của nó, làm đúng thứ tự kiểm và mã thông báo
trong sơ đồ; lệch thì ghi comment Jira, BA cập nhật ở BA Final Review.

| Ticket | Sequence (`docs/06-sequencediagram/document-management/`) | Class (class diagram FE-07) | Endpoint gợi ý | MSG |
|---|---|---|---|---|
| SCRUM-23 | `UC35_CommentOnMeetingDocument` | `DocumentCommentController/Service/Repository` | `GET/POST /api/meetings/{id}/documents/{meetingDocumentId}/comments` | 199, 200, 202, 204, 251, 156 |
| SCRUM-110 | `UC72a`–`UC72d` | `PersonalLibraryScreen`, `FolderController`, `FolderService` (`computeSubtreeDepth`), `FolderRepository` | `GET/POST /api/folders`, `PATCH /api/folders/{id}`, `DELETE /api/folders/{id}` | 175, 179, 187–198 |
| SCRUM-111 | `UC34`, `UC36`, `UC71a`–`UC71c` | `DocumentController`, `DocumentService`, `DocumentRepository`; dùng lại `DocumentFileRules`, `FileStorage` | `GET /api/documents`, `POST /api/documents/upload`, `PATCH /api/documents/{id}`, `DELETE /api/documents/{id}` | 164, 175–181, 183–186 |
| SCRUM-112 | `UC33a_AddDocumentsFromLibrary`, `UC33b`, `UC33c`, `UC33d` | `MeetingDocumentController/Service`: `addLibraryDocuments`, `removeDocument`, `attachDocuments`, `detachDocument`; `MeetingDocumentRepository`: `addAgendaLinks`, `removeAgendaLink`, `markRemoved`… | `POST /api/meetings/{id}/documents` (từ kho), `DELETE …/documents/{meetingDocumentId}`, `POST/DELETE …/agenda/{itemId}/documents` | 161, 162, 164–167, 169–172, 174, 238–249 |

**Bắt buộc khi code (AGENTS.md):**
- Contract `docs/03-api/` cho endpoint mới, dòng catalogue MSG đổi "Dành sẵn" → "Đã triển khai" và "Mã tiếp theo" trong
  **cùng commit** code; `MessageCodeCatalogueTests` phải xanh.
- Kiểm quyền ở server: kho chỉ chủ sở hữu thấy (BR-242, thư mục/tài liệu người khác trả `MSG175` như không tồn tại);
  cuộc họp không xem được trả `MSG174` 404, không 403.
- UC-36 và UC-33b AF-1 dùng lại kiểm tệp và thứ tự ghi của SCRUM-107: ghi dòng `StoredFile` → đẩy R2 → transaction; lỗi
  thì xoá object đã đẩy (ADR-0024 §4, BR-286).
- UC-33b/33d theo phương án (b) (mục 2.2): chức năng riêng, **Chủ toạ và Thư ký** đều dùng được, chặn khi cuộc họp kết thúc.
- Gỡ khỏi cuộc họp (UC-33c) là đặt `RemovedAt`/`RemovedById`, xoá liên kết mục nghị sự, giữ góp ý (BR-281, BR-284).
- Không cần migration cho `Folder`, `Document`, `StoredFile`, `MeetingDocumentAgendaItem` (đã có từ SCRUM-107); cần thêm
  index thì thêm migration mới.
- Phát `documentsChanged` trên hub sau khi thêm, gỡ, gắn hoặc bỏ gắn, như SCRUM-21.

**Câu hỏi còn mở (mục 5), hỏi PO trước khi chốt code:** hạn mức dung lượng kho; quyền đọc kho của người đã rời tổ chức;
UC-36 và UC-33b AF-1 có áp giới hạn 10 tệp / 200 MB (`MSG288`) như UC-33a không.
