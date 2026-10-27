# Bàn giao FE-07 cho Lộc — phần tài liệu chưa code

**Ngày:** 27/10/2026 (rà soát lại cùng ngày). **Người viết:** KhanhPG. **Nguồn:** Jira SCRUM-21/22/23/25/58/91/92/96/98/107/110/111/112
đọc ngày 27/10/2026; Report 3 (Markdown) mục 7.2–7.7; catalogue MSG; code `AgendaEndpoints.cs`, `AgendaService.cs`,
`AgendaRepository.cs`, `MeetingDocumentEndpoints.cs`, `DocumentConfiguration.cs`; `docs/06-sequencediagram/document-management/`;
`docs/07-classdiagram/`; bản đồ UC–BR–MSG sinh bằng `scripts/trace_br.py`.

Note có hai phần:

- **Phần A (mục 1–5)** cho Lộc: hiện trạng, chỗ lệch, ticket và thứ tự code.
- **Phần B (mục 6–7)** cho KhanhPG: kế hoạch vẽ sequence và class diagram (SCRUM-91, SCRUM-92), chia thành bước nhỏ để giao
  agent, mỗi bước có đầu vào, nội dung cần vẽ, điều kiện xong và checklist review.

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
| Gắn tài liệu vào mục nghị sự | UC-33b | SCRUM-25 (FE) / SCRUM-98 (BE) | Frontend / Backend (Huy) | Một phần, qua sửa mục nghị sự (mục 2.2) | **Chưa có** | Sequence; AF-1, AF-2 và MSG riêng chưa code |
| Gỡ tài liệu khỏi một mục nghị sự | UC-33d | SCRUM-112 | To Do | Một phần, qua sửa mục nghị sự (mục 2.2) | **Chưa có** | Sequence; MSG riêng chưa code |
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

   Lộc cần chốt với PO một trong hai cách và ghi vào SCRUM-112:
   (a) giữ API agenda, BA sửa đặc tả UC-33b/33d theo code, các mã `MSG238`–`MSG247` giữ dành sẵn;
   (b) thêm endpoint riêng cho gắn/gỡ một liên kết với đúng mã đặc tả, còn API agenda vẫn nhận `documentIds` như cũ.
   Không tự chọn khi chưa có ý PO. Chọn (b) thì Lộc thêm contract `docs/03-api/` và đổi trạng thái các mã trong catalogue
   cùng commit code. Sequence ở mục 7 bước 3 vẽ **as-built** (cách a); đổi sang (b) thì vẽ lại.
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

1. UC-33b / UC-33d: giữ API agenda hay thêm endpoint riêng (mục 2.2 ý 2), và Chủ toạ có được gắn/gỡ như đặc tả không.
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
| `UC72c_MoveFolder` | Kiểm chủ sở hữu thư mục và đích, đích không phải chính nó/con cháu, cả cây sau khi chuyển ≤ 5 cấp (BR-191, BR-219, BR-222, BR-242) → chuyển cả cây → `MSG194`. Self-call tính độ sâu cây con | EX-1 thư mục không còn `MSG175`; EX-2 đích không còn `MSG179`; EX-3 vào chính nó/con cháu `MSG193`; EX-4 quá 5 cấp `MSG189`; EX-5 lỗi lưu `MSG195`. AF-1 (chuyển ra gốc) ghi bằng `note`, không thêm `alt` |
| `UC72d_DeleteFolder` | Bắt đầu từ lúc xác nhận. Kiểm chủ sở hữu, còn tồn tại, không có tài liệu hay thư mục con (BR-219, BR-223, BR-242) → xoá → `MSG197` | EX-1 không còn `MSG175`; EX-2 không rỗng `MSG196`; EX-3 lỗi lưu `MSG198`. `note` BR-228: thư mục cuộc họp bị xoá sẽ được tạo lại ở lần tải lên sau |

**Xong khi:** 4 cặp `.txt`/`.png`, 4 dòng README, đủ mọi EX của đặc tả, không còn MSG nào lệch catalogue.

### Bước 2 — Sequence tài liệu trong kho UC-34, 36, 71a–c (SCRUM-91)

**Đầu vào riêng:** Report 3 mục 7.3, 7.5, 7.6; luồng AF-1 trong `UC33a_UploadDocumentsToMeeting.txt` (kiểm tệp, đẩy R2 trước,
ghi DB sau, lỗi thì xoá object đã đẩy).
**Lifeline:** `actor "Internal User"`, `boundary "Personal Library Screen"`, `control ":Document Controller"`,
`control ":Document Service"`, `participant ":Folder Repository"`, `participant ":Document Repository"`,
`participant ":Object Storage (R2)"` (chỉ UC-36), `database ":Database"`. PROPOSED.

| File | Luồng chính | Nhánh `alt` theo thứ tự |
|---|---|---|
| `UC34_ViewPersonalLibrary` | Kiểm quyền kho (BR-204, BR-242) → lấy thư mục và tài liệu của người dùng, kèm số cuộc họp người dùng được xem đang dùng tài liệu (BR-219, BR-251) → hiển thị | EX-2 thư mục đã chọn không còn `MSG175`; EX-1 không tải được `MSG176`; AF-2 kết quả rỗng là danh sách rỗng, không phải lỗi. AF-1 tìm/lọc ghi bằng `note` trên request |
| `UC36_UploadToPersonalLibrary` | Kiểm thư mục đích (BR-242) → kiểm từng tệp PDF/DOCX ≤ 50 MB, đuôi khớp nội dung (BR-187, BR-214, BR-215) → đẩy R2 → ghi `StoredFile` + `Document` trong một transaction (BR-183, BR-216, BR-229, BR-286) → `MSG177` | EX-2 đích không còn `MSG179`; EX-1 tệp sai `MSG164`; EX-3 lỗi lưu, xoá object đã đẩy `MSG178` |
| `UC71a_DeleteLibraryDocument` | Bắt đầu từ lúc xác nhận. Kiểm chủ sở hữu, còn tồn tại (BR-242) → xoá dòng `Document`, không đụng `MeetingDocument` (BR-186, BR-232) → `MSG180` | EX-1 không còn `MSG175`; EX-2 lỗi lưu `MSG181` |
| `UC71b_RenameLibraryDocument` | Kiểm chủ sở hữu (BR-242) → lưu tên mới, không đổi `StoredFile` và tên đã chép vào cuộc họp (BR-183, BR-224, BR-276) → `MSG183` | EX-1 không còn `MSG175`; EX-2 lỗi lưu `MSG184` |
| `UC71c_MoveLibraryDocument` | Kiểm chủ sở hữu tài liệu và thư mục đích (BR-225, BR-242) → đổi `folderId` (BR-245) → `MSG185` | EX-1 tài liệu không còn `MSG175`; EX-2 đích không còn `MSG179`; EX-3 lỗi lưu `MSG186`. AF-1 chuyển ra gốc ghi bằng `note` |

Lưu ý UC-71b: đặc tả không có EX tên trống (BR-243 và `MSG182` đã bỏ). Không tự thêm.

**Xong khi:** 5 cặp `.txt`/`.png`, 5 dòng README, có ghi trong README rằng UC-36 dùng chung cách kiểm tệp và R2 với UC-33a AF-1.

### Bước 3 — Sequence gắn và gỡ UC-33a luồng chính, 33b, 33c, 33d (SCRUM-91)

**Đầu vào riêng:** Report 3 mục 7.2; code `AgendaEndpoints.cs` (PUT mục nghị sự, `DocumentIds`), `AgendaService.cs`
(nhánh Create/Update, `CanAssociateAsync`), `AgendaRepository.AssociateAsync`; mục 2.2 ý 2 của note này.

| File | Cách vẽ | Nội dung |
|---|---|---|
| `UC33a_UploadDocumentsToMeeting` (sửa file cũ) hoặc file mới `UC33a_AddDocumentsFromLibrary` | PROPOSED | Luồng chính chọn từ kho: kiểm quyền và trạng thái như AF-1 (BR-184, BR-195) → kiểm từng tài liệu thuộc người thao tác, còn tồn tại, chưa có trong cuộc họp (BR-193, BR-209) → tạo `MeetingDocument` chép tên (BR-197, BR-276) trong một transaction (BR-286) → `MSG161`. `alt`: EX-7 `MSG174`; EX-1 `MSG162`; EX-6 `MSG172`; EX-2 `MSG249`; EX-4 `MSG165`; EX-5 `MSG166`. Chọn một trong hai cách đặt file và ghi lý do trong README (quy tắc §2 một request: hai luồng là hai request khác nhau, nên tách file thì dễ đọc hơn) |
| `UC33b_AttachDocumentsToAgendaItem` | **As-built** | Actor là Thư ký (code hiện chỉ cho Thư ký, Chủ toạ nhận `MSG67`), boundary `"Agenda Item Editor"`, controller `":Agenda Controller"`, service `":Agenda Service"`, repository `":Agenda Repository"`. Request là lưu mục nghị sự kèm `documentIds`. Kiểm theo đúng thứ tự trong `AgendaService` → `CanAssociateAsync` (tài liệu thuộc cuộc họp, chưa gỡ) → `AssociateAsync` thay tập liên kết. Ghi đúng mã code đang trả (`MSG61` thành công, `MSG66` dữ liệu sai gồm `documentIds`, `MSG67` thiếu quyền Secretary hoặc cuộc họp đã khoá, `MSG69` lỗi lưu, `MSG174`). `note` nói rõ: AF-1, AF-2 và các mã `MSG238`, `MSG240`, `MSG242`–`MSG244`, `MSG247` chưa triển khai, chờ quyết định ở mục 2.2 ý 2 |
| `UC33c_RemoveDocumentFromMeeting` | PROPOSED | Bắt đầu từ lúc xác nhận. Lifeline như UC33a. Kiểm truy cập, Chủ toạ/Thư ký, chưa kết thúc, tài liệu còn trong cuộc họp (BR-184, BR-185, BR-279) → đặt `RemovedAt`, `RemovedById`, xoá mọi `MeetingDocumentAgendaItem` của nó, giữ góp ý (BR-211, BR-212, BR-231, BR-197, BR-281, BR-284) → `MSG167`. `alt`: EX-5 `MSG174`; EX-2 `MSG169`; EX-1 `MSG248`; EX-3 `MSG170`; EX-4 `MSG171` |
| `UC33d_DetachDocumentFromAgendaItem` | **As-built** | Cùng request với UC33b (lưu mục nghị sự với `documentIds` đã bỏ tài liệu). Vẽ phần `AssociateAsync` xoá liên kết không còn trong tập; tài liệu vẫn ở cuộc họp (BR-280). `note` nói rõ `MSG239`, `MSG241`, `MSG245`, `MSG246`, `MSG247` chưa triển khai |

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
3. Mở rộng `FolderRepository` (CRUD, `findSubtree`, tính độ sâu), thêm `DocumentRepository`.
4. `MeetingDocumentsTab`/`MeetingDocumentController`/`MeetingDocumentService`: thêm `addFromLibrary`, `removeDocument`
   (UC-33a luồng chính, UC-33c); `MeetingDocumentRepository`: `markRemoved`, `existsInMeeting`.
5. Phía agenda (UC-33b/33d as-built): vẽ `AgendaService.update` và `AgendaRepository.associate` như class ngoài feature,
   đường gạch phụ thuộc, không chép toàn bộ class agenda.
6. Quan hệ: `Folder` tự tham chiếu (parent 0..1 – children 0..*), `Folder` 0..1 — 0..* `Document`, `Document` 0..1 — 0..*
   `MeetingDocument`, `Document` * — 1 `StoredFile`.
7. Mọi class, hàm chưa có code ghi stereotype hoặc ghi chú `PROPOSED`.
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
