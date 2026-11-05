# PR #263 (SCRUM-32) — biên bản lấy dự thảo hiện có, báo khi chưa duyệt hoặc đã cũ, thêm Tải PDF

**Người ghi:** KhánhPG/BA, 05/11/2026. **Nguồn:** PR #263 (nhánh `fix/minutes-from-current-draft`, đang **mở**, CI xanh,
chưa vào `main`), tin nhắn Zalo của BE, comment Jira SCRUM-32, `docs/03-api/17-du-thao-bien-ban.md` và ADR-0054 trên nhánh #263.
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

**Kết luận: viết được ngay, nhưng chỉ nên merge PR docs sau khi #263 vào `main`, và chỉ comment Jira sau khi cả hai đã vào `main`.**

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
- [ ] Sau khi #263 vào `main`: UC-59/60 thẻ báo + Tải PDF, màn §III 12.1, BR cuộc họp Mật gồm PDF.
- [ ] Chạy `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`;
  `sync_requirement_extracts.py --check` phải khớp. **Không sửa tay** `05-…`, `06-…`, `Report-3_….md`.
- [ ] Comment **SCRUM-32** (BR-382 đổi, UC-59/60 đổi gì, PR #263 + commit, còn lại gì) theo
  [DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update) khi #263 và PR docs đều ở `main`.
