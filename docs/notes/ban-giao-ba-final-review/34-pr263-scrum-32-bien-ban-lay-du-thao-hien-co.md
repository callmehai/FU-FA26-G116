# PR #263 (SCRUM-32) — biên bản lấy dự thảo hiện có, báo khi chưa duyệt hoặc đã cũ, thêm Tải PDF

**Người ghi:** KhánhPG/BA, 05/11/2026; cập nhật 06/11/2026. **Nguồn:** PR #263 (nhánh `fix/minutes-from-current-draft`;
**đã vào `main` 06/11/2026**, merge `e856403f`; PR #265 vào sau, merge `911d25bb`, deploy server test; SCRUM-32 đang
In Review), tin nhắn Zalo của BE, comment Jira SCRUM-32, `docs/03-api/17-du-thao-bien-ban.md` và ADR-0054 trên nhánh #263.
Note này gom việc BA, không thay Jira hay đặc tả. Nối tiếp [note 33](33-pr260-scrum-36-32-du-thao-bien-ban-va-scrum-137-tim-gio-hop.md).

## 0. Tóm tắt

PO Hải chốt 05/11/2026: biên bản lấy **dự thảo hiện có, duyệt hay chưa**, như eCab. Lý do chấp nhận được: biên bản nháp
chỉ Chủ tọa/Thư ký xem, nên chữ AI chưa duyệt không lộ ra người khác; biên bản thành bản chính thức vẫn phải qua ký (SCRUM-33).

| Việc | Tài liệu phải sửa | Làm được ngay? |
|---|---|---|
| BR-382 đổi quy tắc | Report 3 DOCX §V.1 (rồi sync ra `06-business-rules.md`) | Được — quy tắc đã chốt, chữ không phụ thuộc code |
| UC-59 AF-3 | Report 3 DOCX §II (rồi sync ra `05-…`) | Được, nhưng nên đợi #263 hết sửa theo review |
| Sequence UC59 đoạn "Read the reviewed AI draft" / "No reviewed AI draft" | `docs/06-sequencediagram/minutes-management/UC59_GenerateMeetingMinutes.txt` + tab draw.io + PNG | Được (xem §2) |
| Mới: thẻ Biên bản báo kèm nút (UC-59/60) | Report 3 DOCX §II + màn §III 12.1 | Đợi #263 vào `main` (xem §3) |
| Mới: nút **Tải PDF** (UC-59/60) | Report 3 DOCX §II + màn §III 12.1 + BR về cuộc họp Mật | Đợi #263 vào `main` |
| API/ADR/ERD/MSG | `17-du-thao-bien-ban.md`, ADR-0054, `02-erd.md`, danh mục MSG | **Đã nằm trong chính #263** — BA không sửa |

## 1. Code làm gì (theo diff #263)

- **Dự thảo hiện có, duyệt hay chưa.** Khi tạo hoặc tạo lại biên bản, `MinutesService` lấy dự thảo AI như nó đang là
  (`SummaryService`), không còn chờ `reviewedAt`. Dự thảo đang chạy lại thì dùng bản viết xong gần nhất.
  Chưa có dự thảo thì mục I–III vẫn để "……" cho Thư ký viết.
- **Vì sao không lộ:** biên bản nháp chỉ Chủ tọa/Thư ký xem (BR-386, không đổi), đúng hai người này cũng là người
  xem và duyệt dự thảo.
- **Cột mới `BuiltFromDraftAt`** (migration `MinutesBuiltFromDraft`): `draftChangedAt` của dự thảo dùng lúc tạo biên bản.
  So với `draftChangedAt` hiện tại để biết biên bản đã cũ. API trả `minutes.builtFromDraftAt`, chỉ cho Chủ tọa/Thư ký.
- **Thẻ Biên bản** (`MinutesPanel`) báo, kèm nút, ba tình huống:
  1. **Chưa có dự thảo** (hoặc đang chờ AI viết) — nút *Tạo biên bản* / điền từ dữ liệu cuộc họp;
  2. **Dự thảo chưa duyệt** — cảnh báo vàng "đang dùng dự thảo AI chưa duyệt", có lối sang tab Dự thảo để duyệt;
  3. **Dự thảo đã đổi sau lần tạo** — thông báo xanh, nút **Tạo lại** (vẫn hỏi trước vì tạo lại thay mọi chỗ đã sửa, BR-388).
- **Nút Tải PDF:** không có endpoint. Trình duyệt in tờ biên bản (A4, lề theo Nghị định 30), hộp in lưu thành PDF.
  **Cuộc họp Mật không có nút này**, như *Tải Word* (MSG405 áp cho Word; PDF cắt ở giao diện nên **không có mã MSG riêng**).
- **Không đổi:** mẫu cố định, `rebuild`, chống ghi đè (MSG401), mã MSG395–MSG405; không thêm mã MSG mới.

## 2. Việc sửa tài liệu

### 2.1 BR-382 (hiện ở Report 3 DOCX §V.1, dòng 354 của `06-business-rules.md`)

Chữ cũ: *"An AI draft is marked as written by AI and must be reviewed before the minutes use it. Without a reviewed
draft the minutes leave sections I–III for the Secretary to write."*

Chữ mới (ý, viết lại tiếng Anh như các BR cạnh nó): *An AI draft is marked as written by AI. The minutes take the
meeting's current draft, reviewed or not, because the draft minutes are seen only by the Chair and the Secretary, who
also review the draft; the minutes card tells them when the draft is unreviewed or has changed since. Without a draft
the minutes leave sections I–III for the Secretary to write.* Cột UC giữ UC-47, UC-50, UC-59; cột nguồn: ADR-0054 +
quyết định PO 05/11/2026.

Soát thêm: BR-373–BR-381 và BR-383–BR-394 có câu nào còn nói "dự thảo đã duyệt" cho biên bản không (tìm `reviewed`) —
đặc biệt BR-381 và BR-385 nếu có nhắc điều kiện duyệt.

### 2.2 UC-59

- **AF-3** (hiện: "chưa có dự thảo đã duyệt thì để …… ") → **AF-3: Chưa có dự thảo**; thêm **AF-4: Dự thảo chưa duyệt**
  (biên bản dùng nguyên văn dự thảo, thẻ báo "chưa duyệt") nếu đặc tả muốn tách; mục I–III lấy dự thảo hiện có.
- Đoạn mô tả bước lấy dữ liệu và Precondition/Postcondition: bỏ ý "dự thảo đã duyệt".
- **Sequence** `UC59_GenerateMeetingMinutes.txt`:
  - `MinutesService->SummaryService: Read the reviewed AI draft` → `Read the current AI draft (reviewed or not)`;
  - `SummaryService-->MinutesService: Sections I-III / None` giữ;
  - `alt Reviewed AI draft exists` → `alt An AI draft exists` (ghi `Minutes with sections I-III from the draft, marked unreviewed or not`);
  - `else No reviewed AI draft - AF-3` → `else No AI draft - AF-3`.
  - Sửa trong `.txt`, xuất lại PNG, rồi cập nhật đúng tab UC59 trong `TrustCabinet_Sequence_Diagrams.drawio`
    (theo [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md), sửa có đích, không vẽ lại cả file; lưu ý lỗi tab lệch
    kiểu trên Windows ở [note 32 §4](32-scrum-20-25-26-69-97-98-fe06-fe05-ba-final-review.md)).

### 2.3 UC-59/60 — thẻ báo và Tải PDF (mới)

- **UC-59**: thêm bước/nhánh phụ cho 3 tình huống thẻ Biên bản ở §1 (chưa dự thảo, dự thảo chưa duyệt, dự thảo đã đổi
  sau lần tạo → *Tạo lại*). Không phải mã lỗi, chỉ là thông báo trên thẻ nên **không đụng danh mục MSG**.
- **UC-60** (xem/sửa/xuất): thêm nút **Tải PDF** (in từ trình duyệt) cạnh *Tải Word*; cuộc họp Mật không có cả hai.
  Chỗ nào đặc tả viết "mới xuất Word, chưa có PDF" thì sửa; BR về cuộc họp Mật (hiện chỉ nói tải về/Word) nói rõ gồm cả PDF.
- Màn **§III 12.1 Minutes Editor**: thêm vùng thông báo trên đầu thẻ, nút *Tải PDF*; bảng field của màn tương ứng.
- Cập nhật câu "Mới xuất Word (`.docx`), chưa có PDF" ở [note 33 §2.1](33-pr260-scrum-36-32-du-thao-bien-ban-va-scrum-137-tim-gio-hop.md) là **lịch sử** — không sửa note đó, note này thay thế.

## 3. Làm được ngay hay phải đợi?

**Cập nhật 06/11/2026: #263 và #265 đã vào `main` — không còn gì phải đợi code, làm hết phần §2 một lượt. Phần còn lại
của #265 (trình soạn thảo, giới hạn HTML) nằm ở [note 35](35-pr265-pr267-scrum-35-36-32-132-viec-ba-moi.md).**
Đoạn dưới giữ nguyên làm lịch sử lúc #263 còn mở.

**Kết luận (lúc 05/11): viết được ngay, nhưng chỉ nên merge PR docs sau khi #263 vào `main`, và chỉ comment Jira sau khi cả hai đã vào `main`.**

- **Đợi code xong có cần không?** Không cần chờ "mọi người code thêm": #263 đã đủ BE + FE + API doc + ADR, CI xanh, `MERGEABLE`.
  Việc còn lại của #263 chỉ là review/merge. Quy tắc (BR-382) là PO đã chốt, nên đặc tả không phải đoán.
- **Vì sao vẫn không nên coi là xong:** (1) #263 chưa vào `main` — [AGENTS.md](../../../AGENTS.md) cấm viết "đã vào main" khi
  mới nhìn trang PR (bài học #238); (2) chữ của thẻ báo và nút trong §2.3 có thể đổi theo review; (3) Report 3 DOCX là
  nguồn chung, sửa sớm dễ xung đột nếu nhánh khác cũng đang sửa.
- **Chia theo mức chắc:**
  - *Làm ngay* (không phụ thuộc chữ UI): BR-382, UC-59 AF-3, sequence UC59 `.txt`.
  - *Đợi #263 chốt review/merge*: thẻ báo, Tải PDF, màn §III 12.1, tab draw.io + PNG, rồi `sync_reports.py` một lần.
- **Không cần BE/FE làm thêm** cho phần docs này; không có endpoint mới, không có mã MSG mới.

## 4. Việc BA còn lại

- [ ] BR-382 (Report 3 DOCX §V.1) và rà BR-373–BR-394 tìm câu còn nhắc "dự thảo đã duyệt" cho biên bản.
- [ ] UC-59 AF-3 (+ AF-4 nếu tách) và bỏ ý "đã duyệt" ở mô tả, tiền/hậu điều kiện.
- [ ] Sequence `UC59_GenerateMeetingMinutes.txt` → PNG → tab UC59 trong `TrustCabinet_Sequence_Diagrams.drawio`.
- [ ] #263 đã vào `main`: UC-59/60 thẻ báo + Tải PDF, màn §III 12.1, BR cuộc họp Mật gồm PDF. Lưu ý sau #265: Tải PDF
  nay đi qua `GET /api/minutes/{id}/export?format=pdf` (`MSG406` khi `format` sai) — kiểm lại §1 "không có endpoint,
  không có mã MSG riêng" trên `main` trước khi viết UC-60; trình soạn thảo là TipTap (note 35).
- [ ] Chạy `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`;
  `sync_requirement_extracts.py --check` phải khớp. **Không sửa tay** `05-…`, `06-…`, `Report-3_….md`.
- [ ] Comment **SCRUM-32** (BR-382 đổi, UC-59/60 đổi gì, PR #263 + commit, còn lại gì) theo
  [DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update) khi #263 và PR docs đều ở `main`.

## 5. Việc chi tiết để giao agent (06/11/2026)

Đối chiếu với `origin/main` ngày 06/11/2026 (#263 + #265 đã vào). **Đính chính §1:** Tải PDF **có endpoint** —
`GET /api/minutes/{id}/export?format=pdf`, server dựng bằng QuestPDF; `format` sai → `400 MSG406`; họp Mật → `403 MSG405`
([docs/03-api/17-du-thao-bien-ban.md](../../03-api/17-du-thao-bien-ban.md) dòng 82, 97–98). BR-391 hiện ghi "không có PDF" — sai so với `main`.

**Quy tắc chung mọi agent** (đọc trước khi làm): nhánh mới từ `origin/main` (nhánh hiện tại `docs/ba-final-review-note33-followup`
đang lệch `main`); tiếng Việt, tên kỹ thuật giữ nguyên; **không sửa tay** `Report-3_….md`, `05-use-case-specifications.md`,
`06-business-rules.md`, `uc-map.html` (sinh từ DOCX bằng `sync_reports.py`); không commit nếu chưa được bảo. Mã MSG và
hành vi lấy từ `docs/03-api/17-du-thao-bien-ban.md` và danh mục MSG, không đoán.

**Chia agent để không đụng nhau:**

| Agent | Sở hữu file | Việc |
|---|---|---|
| **A-DOCX** (một agent duy nhất) | `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx` | Mọi việc DOCX của note 34 **và** note 35 (git không gộp DOCX — hai agent cùng sửa là mất việc) |
| **A-SEQ-59** | `minutes-management/UC59_GenerateMeetingMinutes.txt` + PNG | T34-3 |
| **A-SEQ-60** | `minutes-management/UC60_EditMeetingMinutes.txt` + PNG | T34-4 (+ T35-6 của note 35 cùng file) |
| **A-DRAWIO** | `docs/06-sequencediagram/TrustCabinet_Sequence_Diagrams.drawio` | Chạy **sau** các agent SEQ: cập nhật tab theo `.txt` mới |

### Danh sách việc

**T34-1 — BR-382 (A-DOCX, §V.1).** Thay bằng: *An AI draft is marked as written by AI. The minutes take the meeting's
current draft, reviewed or not, because the draft minutes are seen only by the Chair and the Secretary, who also review the
draft; the minutes card tells them when the draft is unreviewed or has changed since. Without a draft the minutes leave
sections I–III for the Secretary to write.* Giữ cột UC (UC-47, UC-50, UC-59); nguồn: ADR-0054 + quyết định PO 05/11/2026.
*Xong khi:* dòng BR-382 trong `06-business-rules.md` (sau sync) đúng chữ trên.

**T34-2 — UC-59 (A-DOCX, §II).**
- Description: bỏ "using the reviewed AI draft" → "using the meeting's current AI draft, reviewed or not".
- AF-3 đổi tên thành "No AI draft" (chưa có dự thảo hoặc AI chưa viết xong): mục I–III để "……".
- Thêm **AF-4 – AI draft not yet reviewed**: biên bản dùng nguyên văn dự thảo; thẻ Biên bản hiện cảnh báo, có lối sang tab Dự thảo để duyệt.
- Thêm **AF-5 – Draft changed after the minutes were built**: thẻ hiện thông báo kèm nút *Tạo lại* (có xác nhận như AF-2, BR-388).
- Thông báo trên thẻ không phải mã lỗi — **không thêm MSG**.
*Xong khi:* `grep -n "reviewed AI draft" 05-use-case-specifications.md` không còn trong UC-59.

**T34-3 — Sequence UC59 (A-SEQ-59).** Trong `.txt`: `Read the reviewed AI draft` → `Read the current AI draft (reviewed or not)`;
`alt Reviewed AI draft exists` → `alt An AI draft exists`; `else No reviewed AI draft - AF-3` → `else No AI draft - AF-3`. Xuất lại PNG
(`scripts/txt_to_sequence_drawio_png.py`, xem `docs/06-sequencediagram/README.md`). *Xong khi:* PNG mở ra thấy đoạn chữ mới, không còn chữ "reviewed" ở nhánh đó.

**T34-4 — UC-60 tải PDF + BR về PDF (A-DOCX).**
- UC-60 AF-1: tách thành "Download Word" và "Download PDF" (`?format=pdf`); cả hai kiểm quyền + họp không Mật; tên file `Biên bản - <tên cuộc họp>.pdf`.
- Description UC-60: "download the same text as a Word file" → "as a Word or PDF file".
- **BR-391**: *The minutes are downloaded as a Word file (.docx) or a PDF file; both are built from the same content.* (bỏ câu "A PDF version is not provided").
- **BR-390**: giữ, thêm ý gồm cả Word và PDF.
- Thêm vào UC-60 một EX: `format` không hợp lệ → MSG406 – "Chỉ tải biên bản dạng Word (docx) hoặc PDF." (nguồn chữ: danh mục MSG, dòng MSG406). *Lưu ý:* MSG406 đang bị PR #267 đòi cho 429 — xem [note 35 §4](35-pr265-pr267-scrum-35-36-32-132-viec-ba-moi.md).
*Xong khi:* `grep "PDF version is not provided"` trong `06-business-rules.md` không còn.

**T34-5 — Màn §III 12.1 Minutes Editor (A-DOCX).** Thêm vùng thông báo trên đầu thẻ (3 trạng thái ở T34-2), nút *Tải PDF* cạnh *Tải Word*; cập nhật bảng field. Màn hình chỉ thêm qua wireframe → `screens/*.png` (xem [docs/08-reports/README.md](../../08-reports/README.md)), **không** chụp ảnh app.

**T34-6 — Rà BR (A-DOCX).** `grep -n "reviewed" 06-business-rules.md` trong dải BR-373–BR-394; BR-381, BR-385 chỉnh nếu còn điều kiện "đã duyệt" cho biên bản. (BR-385/394 còn bị #265 đổi — xem T35-5.)

**T34-7 — Đóng việc.** Sau khi A-DOCX xong: `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`; `python scripts/sync_requirement_extracts.py --check` phải khớp. Soát `docs/README` / `uc-map` nếu sync đổi. Comment Jira SCRUM-32 (BR-382, BR-391 đổi; UC-59/60 đổi gì; PR #263 `e856403f`, #265 `911d25bb`; còn lại gì) sau khi PR docs ở `main`.

## 6. Trạng thái thực hiện — nhánh `docs/ba-final-review-note34-35` (06/11/2026)

Đã làm **T34-1, T34-2, T34-3, T34-4, T34-6** và chạy `sync_reports.py --report 3`; `sync_requirement_extracts.py --check`
trả `UP TO DATE`. Sequence UC59 đã thêm chữ mới trong `.txt`, ảnh `UC59_GenerateMeetingMinutes.png`,
`drawio-png/uc59_generatemeetingminutes.drawio.png` và tab UC-59.

- **T34-1** BR-382 viết lại (dự thảo hiện có, duyệt hay chưa; thẻ báo khi chưa duyệt / đã đổi), nguồn ADR-0054 + PO 05/11/2026.
- **T34-2** UC-59: Description, AF-3 "No AI draft", thêm AF-4 "AI draft not yet reviewed", AF-5 "Draft changed after the minutes were built";
  bước 4 Normal Flow bỏ điều kiện "đã duyệt".
- **T34-3** `UC59_GenerateMeetingMinutes.txt`: đọc dự thảo hiện tại, `alt An AI draft exists`, `else No AI draft - AF-3`.
- **T34-4** UC-60: AF-1 tách Word, thêm AF-2 PDF, EX-8 `format` sai → MSG406; BR-391/BR-390/BR-394 viết lại.
- **T34-6** rà BR-373–BR-394: không còn câu "dự thảo đã duyệt" cho biên bản; BR-385/394 đã đổi ở note 35.

**T34-5 tạm hoãn theo yêu cầu (06/11/2026):** **không làm đặc tả màn hình** §III 12.1 Minutes Editor (vùng thông báo trên thẻ,
nút *Tải PDF*, bảng field) và không thay ảnh wireframe. T34-5 để mở, làm cùng đợt đặc tả màn hình sau.

**T34-7** chưa comment Jira: PR docs chưa vào `main`; comment và đổi trạng thái theo §2.2 khi merge.
