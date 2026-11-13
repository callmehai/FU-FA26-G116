# PR #311 SCRUM-16 — nhật ký kiểm toán; BA Final Review

**Người ghi:** KhánhPG/BA, 11/2026. **Nguồn:** comment Jira SCRUM-16 `10353` (Khách ghi audit), `10493` (đặc tả, D1–D7), `10494` (BE xong, D8/D9, việc còn lại cho BA),
`10497` (PO chuyển sang BA Final Review), mô tả PR #311, [NOTE-dac-ta-so-bo-scrum-16.md](../NOTE-dac-ta-so-bo-scrum-16.md), contract
[21-nhat-ky-kiem-toan.md](../../03-api/21-nhat-ky-kiem-toan.md). Note này là sổ việc, không thay Jira hay đặc tả
([DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update)).

## 0. Ticket và PR

| Ticket | Trạng thái Jira (đọc 11/11/2026) | PR | Trên `main` |
|---|---|---|---|
| SCRUM-16 Nhật ký kiểm toán các hành động quan trọng | **`BA Final Review`** | #311 (BE), gồm #312 (FE màn 3.3 `/mgmt/admin/audit-log`) | Có — merge `7c61335c` (10/11/2026), đã kiểm `git branch -r --contains` |
| Đặc tả sơ bộ | — | #309 (Report 3: UC-13, BR-440 → BR-446, MSG469–470) | Có |

Report 3 đã được BE sửa một phần ngay trong #311 (`126d178c`: BR-444, BR-445, UC-14/15 vào *Applies to*, Change Log, sinh lại extract). BA **không làm lại** những chỗ đó.

## 1. Hiện trạng đã kiểm (11/11/2026)

| Phần | Trạng thái | Bằng chứng |
|---|---|---|
| UC-13 (AF-1/2/3, EX-1/2/3), BR-440 → BR-446, MSG469/470 trong Report 3 | Đã có, khớp code | Report 3 dòng 1267–1276, 4856–4862; catalogue dòng 699–700 |
| Sequence UC-13 | **Lệch tên lớp so với code** | `.txt` dùng `:Audit Log Controller`, `getAuditEntries`, `AuditLogRepository → findReadOnlyPage`; code là `AuditLogEndpoints` (Minimal API) → `IAuditLogQueryService.GetPageAsync` → `TrustCabinetDbContext`, không có repository riêng |
| Class diagram FE-03 | Còn `AuditLogRepository` | [07-classdiagram/README.md](../../07-classdiagram/README.md) dòng 176 và 450 |
| Đặc tả màn 3.3 (Report 3 §III, dòng 1010, 2809+) | **Còn nội dung cũ** | Còn "export to CSV", "before/after details", chưa có lựa chọn Khách / Hệ thống |
| UC khác cần nhắc BR-444 → BR-446 | Chưa | Xem §2, 42-4 |

## 2. Việc BA

| # | Chỗ sửa | Sửa thành |
|---|---|---|
| 42-1 | Sequence UC-13 (`role-management/UC13_ViewAuditLog.txt` + PNG + tab drawio) | Đổi lifeline cho khớp tên lớp: bỏ `AuditLogRepository`, dịch vụ truy vấn đọc thẳng `Database`; `AuditLogController` thành `AuditLogEndpoints`; `getAuditEntries` thành `getPage` (code: `GetPageAsync`). Giữ MSG469/MSG470 |
| 42-2 | Class diagram FE-03 (`.drawio` + PNG) và [07-classdiagram/README.md](../../07-classdiagram/README.md) dòng 176, 450 | Thay `AuditLogRepository` bằng `AuditLogEndpoints` + `AuditLogQueryService`; bảng đối chiếu UC-13 ghi lại tên hàm thật. Làm theo [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md) |
| 42-3 | UC-14, UC-15, UC-18 → UC-22, UC-75, UC-64 | Thêm một bước nhắc **ghi nhật ký** (BR-445 cho cuộc họp, BR-446 cho UC-64), theo cách đã làm với BR-318 ở các UC khác |
| 42-4 | UC-01, UC-02, UC-04 | Thêm bước nhắc BR-444 (đăng nhập thành công / thất bại vào tài khoản có thật; Khách xác minh OTP). Email lạ không ghi |
| 42-5 | UC-13 AF-2 | Kiểm lại câu "for a change, the values before and after it": API trả `details` key–value, tên trường khác nhau theo loại sự kiện (comment `10494`), **không phải** luôn có before/after. **Đề xuất sửa:** "the details recorded with that entry" |
| 42-6 | Change Log Report 3 | Một dòng cho phần BA (UC, sequence, class diagram). Dòng của BE ở `126d178c` giữ nguyên |
| 42-7 | Master inventory, UC map | `uc-map.html` đã sinh lại ở #311; chạy lại `sync_reports.py --report 3` sau khi sửa DOCX để chắc khớp |
| 42-8 | Đầu màn 3.3 (Report 3 §III, dòng 2811) | Một câu cảnh báo: "Xuất CSV, giá trị trước/sau và '5 lần liên tiếp' không có trong bản hiện tại (SCRUM-16 D3, D5, D6); đặc tả màn đang chờ cập nhật." |

**PENDING — đặc tả màn 3.3 và wireframe** (không làm trong đợt này, theo quyết định BA từ 08/11/2026; **không bỏ**). Màn 3.3 vẫn mô tả
CSV, before/after, "5 lần liên tiếp". Bảng as-built và các bước mở khoá ở [§7](#7-pending--đặc-tả-màn-33-nhật-ký-hệ-thống). Trong lúc
chờ, thêm **một câu cảnh báo đầu màn 3.3** (việc 42-8). Các chỗ lệch cũng đã nằm trong comment Jira `10493`/`10494`.

## 3. Chỗ code lệch đặc tả/ticket cần ghi nhận (đều đã có ở Jira, không sửa code)

| # | Lệch | Ghi ở |
|---|---|---|
| D1 | Không ghi lượt **tải** tài liệu (ADR-0057: chỉ ghi lượt **xem** tài liệu Mật; Mật không tải được, BR-188) | `10493` |
| D2 | Quyền xem: `AdminUsers`, không thêm quyền mới | `10493` |
| D3 | Màn 3.3: **không xuất CSV** | `10493` |
| D4 | Ô lọc người thực hiện thêm Khách và Hệ thống | `10493` |
| D5 | Không có "5 lần đăng nhập sai liên tiếp"; mỗi lần sai là một dòng | `10493` |
| D6 | Không có cột "kết quả" (`audit_log` không có cột này) | `10493` |
| D7 | Cột Đối tượng: chỉ tài khoản và vai trò có tên; cuộc họp, tài liệu hiện loại + mã (BR-443) | `10493`; PR #311 sau review FE mở rộng `q` cho tên tài khoản/vai trò |
| D8 | Đăng nhập sai ghi **tài khoản bị thử** làm người thực hiện (không để trống) | `10494`, BR-444 đã sửa |
| D9 | BR-445 ghi **mọi dòng** lịch sử cuộc họp (tạo, đổi lịch, chuyển người tổ chức), không chỉ đổi trạng thái | `10494`, BR-445 đã sửa |

Hai điểm BE đã chấp nhận (ghi để Tester biết, không phải lỗi): email lạ không ghi (chậm hơn vài ms ở nhánh "sai mật khẩu"); nếu ghi audit lỗi lúc đăng nhập thì người dùng nhận 500 (giữ bằng chứng theo BR-444).

## 4. Câu hỏi — PO đã trả lời 11/11/2026

| # | Câu hỏi | PO chốt (11/11/2026) | Việc kéo theo |
|---|---|---|---|
| Q-A | UC-13 AF-2 (xem chi tiết) giữ chữ "before and after" hay đổi như 42-5? | **Sửa đặc tả theo code**: chi tiết mỗi loại sự kiện một kiểu, không bắt buộc có trước/sau. eCab cũng không có. | Không làm lại BE. 42-5 đã sửa DOCX 11/11 ("the details recorded with that entry") — đúng hướng PO, không cần sửa thêm. |
| Q-B | Comment `10353` (SCRUM-123): có ghi nhật ký khi Khách **đăng xuất**? #311 chỉ ghi lúc Khách xác minh OTP (`guest.verified`). | **Không ghi.** | Không mở ticket mới; không sửa code. Khi đóng ticket, trả lời comment `10353` ở SCRUM-123 bằng quyết định này. |

## 5. Jira — làm sau khi PR docs vào `main`

1. Comment SCRUM-16: UC-13 / sequence / class diagram / UC-01, 02, 04, 14, 15, 18–22, 64, 75 đã đổi; PR docs và commit merge (kiểm `git log origin/main`); việc còn lại: đặc tả màn 3.3 (hoãn). Ghi quyết định PO 11/11/2026: Q-A sửa đặc tả theo code, Q-B Khách đăng xuất không ghi nhật ký (trả lời luôn comment `10353` ở SCRUM-123).
2. Chuyển `BA Final Review` → `In Review` cho Tester. Nhắc Tester: lọc theo Khách / Hệ thống; `%2B07:00` trong URL; Khách đọc tài liệu Mật hiện là Khách chứ không phải "Hệ thống"; thiếu quyền → 403 không mã; route thiếu quyền hiện 404.

## 6. Kế hoạch chi tiết

Thứ tự: Report 3 (DOCX) → sync → sequence → class diagram → PR → Jira. Sequence và class diagram đứng sau DOCX vì AF-2 (42-5)
đổi lời của UC, mà sequence trích lời đó. Ước lượng thô: Pha 1–2 một buổi, Pha 3–4 một buổi, Pha 5–6 một giờ.

### Pha 0 — Chuẩn bị (15 phút)

1. Dùng nhánh `docs/ba-final-review-pr297-pr311` (từ `origin/main`).
2. Chạy `python scripts/check_docx.py` trước khi sửa, ghi số lỗi / cảnh báo ban đầu.
3. Đọc nguồn đối chiếu: [21-nhat-ky-kiem-toan.md](../../03-api/21-nhat-ky-kiem-toan.md), `backend/TrustCabinet.Web/Endpoints/AuditLogEndpoints.cs`,
   `backend/TrustCabinet.Infrastructure/Audit/AuditLogQueryService.cs`, `AuditEventCatalog.cs`, `tests/TrustCabinet.Web.Tests/Audit/AuditLogEndpointTests.cs`.
4. Lấy danh sách UC cần sửa bước ghi nhật ký và đọc từng UC trong DOCX trước khi gõ (xem 1.2).

**Cổng 0:** biết số lỗi `check_docx.py` ban đầu; đã đọc 4 nguồn.

### Pha 1 — Report 3 DOCX

| Bước | Việc | Chi tiết |
|---|---|---|
| 1.1 | **UC-13 AF-2** (42-5) | Bước 2: "The system displays the details recorded with that entry, read-only. The fields differ between event types. (BR-441, BR-443)". Bỏ "for a change, the values before and after it". Kiểm mục Description và POST không còn nhắc before/after |
| 1.2 | **Bước ghi nhật ký vào UC** (42-3, 42-4) | Cách làm giống UC-06a: một bước "The system records … in the audit log (BR-xxx)." trước bước hiển thị MSG, và một POST. Từng UC: **UC-01** (đăng nhập thành công và đăng nhập sai vào tài khoản có thật, ghi phương thức; EX sai mật khẩu ghi "naming the account as the actor"; email lạ không ghi — BR-444); **UC-02** (như UC-01, phương thức Google); **UC-04** (Khách xác minh OTP thành công, BR-444); **UC-14** (tạo cuộc họp, BR-445); **UC-15** (sửa, đổi lịch, BR-445); **UC-18** (gửi duyệt), **UC-19** (duyệt / từ chối), **UC-20** (công bố), **UC-21** (bắt đầu), **UC-22** (kết thúc, và job tự huỷ), **UC-75** (huỷ) — mỗi cái một dòng "meeting lifecycle history entry is recorded in the audit log (BR-445)"; **UC-64** (ban hành, kể cả bản đính chính, BR-446) — làm cùng lúc với note 41 để khỏi mở UC-64 hai lần |
| 1.3 | **BR-444 → BR-446 "Applies to"** | Kiểm cột UC khớp 1.2 (BR-444: UC-01, 02, 04; BR-445: UC-14, 15, 18–22, 75; BR-446: UC-64). BE đã sửa BR-445, BA chỉ kiểm |
| 1.4 | **Đầu màn 3.3** (42-8) | Một câu cảnh báo, tiếng Anh như phần còn lại, thêm bản tiếng Việt trong ngoặc vuông nếu mục đang theo kiểu đó |
| 1.5 | **Change Log** (42-6) | Một dòng ngày làm: UC-13 AF-2, UC-01/02/04/14/15/18–22/64/75 thêm bước ghi nhật ký, màn 3.3 ghi chú đang chờ. Không xoá dòng của BE ở `126d178c` |
| 1.6 | UC-13 còn lại | Đối chiếu một lần với `AuditLogEndpointTests`: AF-1 (period, actor, event, keyword — keyword khớp **giá trị**, id, tên tài khoản/vai trò, không khớp tên khoá); EX-1 gồm tham số lạ; EX-3 token còn claim nhưng DB đã bỏ quyền. Chỉ sửa nếu lệch |

**Cổng 1:** mỗi UC ở 1.2 có đúng một bước ghi nhật ký và một POST; mọi `BR-44x` trong UC đều có trong bảng BR; AF-2 không còn "before and after".

### Pha 2 — Sync và kiểm tra (20 phút)

```bash
python scripts/check_docx.py docs/08-reports/source/Report_3_Software_Requirement_Specification.docx
python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3
python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3   # lần hai không đổi file
git diff --stat docs/            # DOCX, Report-3 .md, 05/06 extracts, uc-map.html
```

Làm **một lần** chung với note 41 nếu hai PR cùng đi trước khi đóng DOCX. Không sửa tay snapshot ([AGENTS.md](../../../AGENTS.md#documentation)).

**Cổng 2:** `check_docx.py` không thêm lỗi; sync lần hai không đổi file.

### Pha 3 — Sequence UC-13 (45 phút)

File: `docs/06-sequencediagram/role-management/UC13_ViewAuditLog.txt`.

| Dòng hiện tại | Sửa thành |
|---|---|
| 4–7 lifeline `:Audit Log Controller`, `:Audit Log Query Service`, `:Audit Log Repository` | `control ":Audit Log Endpoints"` (Minimal API) và `control ":Audit Log Query Service"`; **bỏ** `AuditLogRepository`, truy vấn đi thẳng `database ":Database"` |
| 20 `getAuditEntries(criteria, page)` | `getPage(query)` |
| 29–36 khối "Find the entries … AuditLogRepository → Database" | Dịch vụ truy vấn đọc `Database` trực tiếp ("Retrieve the matching audit entries and their total") |
| 43–50 khối tên và email | Cũng đi thẳng `Database` ("Retrieve the names and emails of the accounts and Guest invitations named by the entries") |
| 16–18 nhánh thiếu quyền | Ghi rõ **403 không mã** và nhắc đăng nhập hết hạn = 401 (hiện chỉ "Request refused") |
| 22 kiểm bộ lọc | Đặt kiểm tra trong `Endpoints` (parser tham số) thay vì dịch vụ, nếu đọc code thấy parser nằm ở `AuditLogEndpoints` (`AuditLogQueryParser`); ghi nhánh tham số lạ / lặp |

Sau khi sửa:

```bash
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/role-management/UC13_ViewAuditLog.txt
```

1. Mở PNG, đọc chữ, kiểm MSG469/MSG470 nguyên văn.
2. Cập nhật [role-management/README.md](../../06-sequencediagram/role-management/README.md) (dòng UC-13: lifeline và hàm).
3. `git diff --stat` của `TrustCabinet_Sequence_Diagrams.drawio` chỉ đổi tab UC-13.

**Cổng 3:** không còn chữ `Repository` trong UC13 `.txt`; PNG mới.

### Pha 4 — Class diagram FE-03 (45 phút)

File: `docs/07-classdiagram/feature-03-role-management-class-diagram.drawio` (+ `.drawio.png`). Làm theo [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md) và [drawio-skill](../../../.agents/skills/drawio-skill/SKILL.md): sửa có mục tiêu, giữ ID và quan hệ, không vẽ lại toàn bộ.

1. Mở file, tìm lớp `AuditLogRepository`, `AuditLogQueryService`, controller của UC-13.
2. Đổi/bổ: `AuditLogEndpoints` (không phải controller lớp); `AuditLogQueryService.getPage`; bỏ `AuditLogRepository` và các quan hệ tới nó, nối `AuditLogQueryService` tới thực thể `AuditLogEntry` qua `TrustCabinetDbContext` hoặc ghi "DbContext" như các feature khác đang làm (đọc FE-04 làm mẫu).
3. Thêm lớp trả về nếu feature khác cũng vẽ DTO: `AuditLogPage`, `AuditLogItem`, `AuditActor` (kind: user, guest, system).
4. Validate drawio (skill drawio có validator), xuất PNG, mở xem.
5. [07-classdiagram/README.md](../../07-classdiagram/README.md): dòng 176 (`AuditLogRepository phục vụ BR-318` — chỉ sửa nếu đoạn đó nói về UC-13/FE-03, còn là FE-07 thì giữ) và dòng 450 (bảng đối chiếu UC-13: tên hàm thật `AuditLogQueryService → GetPageAsync`, cột ghi chú bỏ "Trùng tên").

**Cổng 4:** validator 0 lỗi; PNG mới; README khớp.

### Pha 5 — PR docs (30 phút)

1. Commit nhỏ, tiếng Việt: `[Docs] SCRUM-16: UC-13 AF-2 và bước ghi nhật ký BR-444 → BR-446 vào UC`, `[Docs] SCRUM-16: sequence UC-13 và class diagram FE-03 theo code`, `[Docs] Note 41/42: kế hoạch BA Final Review PR #297, #311`.
2. Tick checklist §8 và sửa dòng 42 trong [README](README.md) cùng commit.
3. PR tiêu đề **tiếng Anh**: `[Docs] SCRUM-16 BA Final Review: UC-13, audit steps in UC-01/02/04/14/15/18–22/64/75, UC-13 sequence and FE-03 class diagram`; mô tả liệt kê chỗ đổi, D8/D9 đã xử lý ở `126d178c`, **PENDING** màn 3.3.
4. CI xanh rồi mới nhờ review (Vietnamese review comment).

### Pha 6 — Đóng ticket (sau khi PR **đã vào `main`**)

1. `git fetch && git log --oneline origin/main` thấy commit merge.
2. Comment SCRUM-16 (trả lời `10494`): UC, sequence, class diagram đã đổi; PR docs + commit; còn lại = PENDING §7; kèm quyết định PO 11/11/2026 cho Q-A, Q-B (§4).
3. Chuyển `BA Final Review` → `In Review`.
4. Cập nhật dòng 42 trong README.

## 7. PENDING — đặc tả màn 3.3 "Nhật ký hệ thống"

**Trạng thái: PENDING.** Chưa làm đợt này. **Điều kiện mở khoá:** PO/BA đồng ý làm đặc tả màn hình; giao diện `AuditLogPage` đã ổn.

### 7.1 Đặc tả hiện tại (Report 3 §III 3.3) và as-built

| Phần tử | Report 3 hiện nói | Code hiện làm (`AuditLogPage.tsx`, `AuditEntrySheet.tsx`) |
|---|---|---|
| Tiêu đề trang | "Quản trị / Nhật ký hệ thống" + ghi chú "Sự kiện nghiệp vụ và bảo mật. Không sửa, không xoá được." | Có tiêu đề "Nhật ký hệ thống" và mô tả như trên; không có breadcrumb |
| Xuất CSV | Nút phụ | **Không có** (Q3, D3) |
| Khoảng ngày | dd/mm/yyyy – dd/mm/yyyy | Chọn **cả ngày** theo giờ máy (đầu ngày → 23:59:59.999), hai đầu gồm; start > end hiện "Ngày bắt đầu không được sau ngày kết thúc." và **không áp dụng** |
| Người thực hiện | Dropdown "Mọi người thực hiện", chỉ liệt kê người dùng | Thêm **Khách**, **Hệ thống**; người dùng có gợi ý email khi trùng tên, "Đã vô hiệu hoá"; liên kết trỏ tài khoản không có trong danh sách hiện "Tài khoản không rõ" (D4) |
| Loại sự kiện | Dropdown "Mọi loại sự kiện" | Danh sách lấy từ `GET /api/audit-log/event-types`, nhãn tiếng Việt từ backend |
| Tìm kiếm | "Tìm theo đối tượng…", khớp tên đối tượng | Cùng placeholder; tối đa 100 ký tự; khớp mã đối tượng, tên tài khoản/vai trò, và **giá trị** trong chi tiết (không khớp tên khoá) |
| Cột | Thời điểm, Người thực hiện, Sự kiện, Đối tượng, IP | Sáu cột: Thời điểm, Người thực hiện, Sự kiện, Đối tượng, Địa chỉ IP, cột nút "Xem chi tiết" (mắt) |
| Người thực hiện (ô) | Avatar chữ cái + tên | Người dùng: avatar + tên (+ email); **Khách**: chip "Khách" + tên mời + email; **Hệ thống**: biểu tượng bánh răng + "Hệ thống" |
| Sự kiện (ô) | Chip; sự kiện bảo mật chip đỏ, **cả dòng tô đỏ** | Chip xám; chỉ `auth.sign_in_failed` chip đỏ và dòng tô (`row-alert`) |
| Đối tượng (ô) | Đối tượng hoặc "5 lần liên tiếp" | Loại đối tượng + tên (chỉ tài khoản, vai trò) hoặc `#` + 8 ký tự đầu của mã (D5, D7) |
| IP | Cột | "Không có" khi trống (dòng hệ thống không có IP) |
| Chi tiết | Ngăn bên phải: giá trị **trước/sau** | Ngăn bên: Người thực hiện, Mã sự kiện, Đối tượng, Địa chỉ IP, rồi "Chi tiết" là cặp khoá–giá trị chỉ đọc, "Không có chi tiết." khi trống; thời điểm có **giây** (D6) |
| Phân trang | "Hiển thị 1–5 trong 1.284", nút trước/sau | Component `Pagination`: cỡ trang 20 / 50 / 100, nhãn "sự kiện", đóng khi đang tải |
| Trạng thái trống | Thông báo kết quả trống | Có bộ lọc: "Không có sự kiện nào khớp bộ lọc." + **Xoá bộ lọc**; không bộ lọc: "Chưa có sự kiện nào được ghi." |
| Lỗi | — | `MSG469` từ đường dẫn: "Bộ lọc trên đường dẫn không hợp lệ." + **Đặt lại bộ lọc**; lỗi khác: câu từ server + **Thử lại** |
| Trên URL | — | Mọi tiêu chí (`from`, `to`, `actor`, `action`, `q`, `page`, `pageSize`) lưu trên URL, mở lại giữ nguyên; giá trị hỏng bị bỏ |
| Màn hẹp | — | Nút **Bộ lọc** (kèm số bộ lọc đang bật) mở/đóng nhóm lọc |

### 7.2 Khi mở khoá PENDING — các bước

1. Chạy app ([verify-ui-in-browser](../../../.claude/skills/verify-ui-in-browser/SKILL.md)); đăng nhập Quản trị; chụp ở 1440px và 390px: danh sách mặc định, sau lọc từng tiêu chí, hàng đăng nhập sai (đỏ), hàng Khách, hàng Hệ thống, ngăn chi tiết (có và không có chi tiết), trống có bộ lọc, trống không bộ lọc, lỗi `MSG469`, lỗi 500, bộ lọc trên màn hẹp.
2. Viết lại 3.3: đoạn mở đầu bỏ CSV và trước/sau; danh sách chức năng chỉ còn View, Filter, Search, View Event Details; Field Description theo bảng 7.1 (mỗi hàng một phần tử, chuỗi UI tiếng Việt trong ngoặc vuông).
3. Dùng skill [minimize-text](../../../.claude/skills/minimize-text/SKILL.md) để đối chiếu: mỗi hàng đặc tả một câu, phần phụ để vào tooltip ghi chú.
4. Wireframe 3.3 vẽ lại; kiểm `docs/wireframe/` có file nào chứa màn Audit Log cần sửa (có nhiều phiên bản v5–v11; chỉ sửa bản đang dùng).
5. Qua Pha 1–2 như một PR riêng; mở note mới và ghi README.

## 8. Checklist

- [x] §6 Pha 0 → Pha 6 — 11/11/2026, trừ Pha 6 (chờ PR docs vào `main`)
- [x] 42-1, 42-2: sequence UC-13 và class diagram FE-03 (xuất PNG, validate drawio) — 11/11/2026
- [x] 42-3, 42-4: thêm bước ghi nhật ký vào UC-01, 02, 04, 14, 15, 18–22, 75, 64 (DOCX) — 11/11/2026
- [x] 42-5, 42-6, 42-7, 42-8: UC-13 AF-2, Change Log, `sync_reports.py --report 3`, `check_docx.py`, câu cảnh báo màn 3.3 — 11/11/2026
- [x] §4: PO trả lời Q-A (sửa đặc tả theo code — đã khớp 42-5) và Q-B (Khách đăng xuất không ghi) — 11/11/2026
- [x] §5: comment Jira sau khi PR docs vào `main` — xong 12/11/2026, comment `10520`; ticket đã `Done` nên không đổi trạng thái
- [ ] **PENDING** §7: đặc tả màn 3.3 và wireframe (chưa làm; mở khoá khi PO/BA đồng ý)
