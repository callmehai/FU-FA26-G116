# PR #297 SCRUM-33 — duyệt và ban hành biên bản; BA Final Review

**Người ghi:** KhánhPG/BA, 11/2026. **Nguồn:** comment Jira SCRUM-33 `10464` (BE, D1/D3/D4), `10475`/`10476` (PO, Q2/Q7),
`10492` (Huy, "Note BA — SCRUM-33"), mô tả PR #297, đặc tả sơ bộ [NOTE-dac-ta-so-bo-scrum-33.md](../NOTE-dac-ta-so-bo-scrum-33.md).
Note này là sổ việc, không thay Jira hay đặc tả ([DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update)).

## 0. Ticket và PR

| Ticket | Trạng thái Jira (đọc 11/11/2026) | PR | Trên `main` |
|---|---|---|---|
| SCRUM-33 Phê duyệt và ban hành biên bản | `In Review` | #297 (BE + FE), nhánh `feat/scrum-33-duyet-ban-hanh-bien-ban`; đặc tả sơ bộ ở #279 | Có — merge `a474f37f` (09/11/2026), đã kiểm `git branch -r --contains` |
| SCRUM-37 Ký số biên bản (mô phỏng) | `To Do` | — | Chỉ giữ UC-62, UC-63 làm placeholder (D4) |

Ticket đã ở `In Review` trước khi BA làm xong tài liệu. Khi BA xong, **không chuyển trạng thái nữa**, chỉ comment (§6).

## 1. Hiện trạng đã kiểm (11/11/2026)

| Phần | Trạng thái | Bằng chứng |
|---|---|---|
| Code BE + FE, contract `03-api/17-du-thao-bien-ban.md`, ERD, catalogue | Xong trong #297 | `MSG424`–`MSG434`, `MSG456` ở catalogue đã là "BE" / "Đã triển khai SCRUM-33" |
| Report 3 DOCX | **Chưa** | `Report-3_….md` dòng 802–806 còn "Submit Meeting Minutes for Signing", "Finalize", "Request Revision" kiểu *Placeholder only*; §12.4–12.8 chỉ có "Placeholder only"; không có UC-83; không có BR-404 → BR-413 (BR-388 còn "signing") |
| Sequence `.txt` UC-61/64/65/83 | Có, theo đặc tả sơ bộ | `docs/06-sequencediagram/minutes-management/` — **chưa có PNG**, chưa có tab trong `TrustCabinet_Sequence_Diagrams.drawio` |
| Report 4 (FE-12) | **Chưa** | Mục II chỉ FE-01 → FE-07, FE-11, FE-13 (comment `10492`) |

## 2. Việc BA — Report 3 (sửa DOCX, không sửa tay Markdown)

| # | Chỗ sửa | Sửa thành |
|---|---|---|
| 33-1 | UC-61 | Viết đủ **Gửi duyệt** (Thư ký) theo mục 3 của note sơ bộ; đổi tên khỏi *Submit for Signing* |
| 33-2 | UC-64 | **Duyệt và ban hành** (Chủ toạ). Đối chiếu code: thông báo ban hành đi tới mọi người đọc được cuộc họp **trừ Khách và Chủ toạ** (note sơ bộ chỉ ghi "trừ Khách") |
| 33-3 | UC-65 | **Trả lại** (Chủ toạ), lý do 1–2.000 ký tự. Thêm: mỗi bản chỉ giữ lý do của lần trả cuối, trả lần hai thì lý do cũ bị ghi đè (comment `10492`) |
| 33-4 | UC-62, UC-63 | Giữ placeholder, đổi mô tả "dành cho ký số mô phỏng SCRUM-37" |
| 33-5 | UC-83 (mới) | **Tạo bản đính chính**, theo mục 3 của note sơ bộ; thêm vào bảng UC của FE-12 |
| 33-6 | UC-58 | Bỏ "nobody else sees them"; thêm người đọc được cuộc họp (trừ Khách) xem bản đã ban hành, mở bản cũ qua `history`, tải Word/PDF (Q7). Bản nháp, chờ duyệt, bị trả lại vẫn 404 với người đọc |
| 33-7 | UC-59 | Thêm EX `MSG456` (`rebuild: true` trên bản đính chính); điều kiện bản mới nhất là nháp **hoặc bị trả lại** |
| 33-8 | UC-60 | Sửa được bản `Rejected` và bản đính chính; `MSG400` chỉ cho bản chờ duyệt hoặc đã ban hành |
| 33-9 | BR-404 → BR-413 | Thêm theo mục 4 của note sơ bộ. BR-388: "submitted for signing" → "submitted for approval". BR-412 theo PO: Khách không bao giờ thấy (Q2, 07/11), người đọc tải được bản đã ban hành (Q7, 08/11), họp Mật không ai tải (`MSG405`) |
| 33-10 | System Messages §V.2 | `MSG424`–`MSG434`, `MSG456`; kiểm bảng UC/MSG khớp catalogue |
| 33-11 | Inventory, UC diagram FE-12, tên FE-12 | Đổi tên UC-61/64/65, thêm UC-83; tên FE-12 bỏ "Digital Signing" |
| 33-12 | Change Log | Một dòng ngày làm |
| 33-13 | Đầu mục 12 của §III (màn hình) | Một câu cảnh báo: màn ký cũ đã thay bằng luồng duyệt, đặc tả màn đang chờ (PENDING, §9) |

**PENDING — đặc tả màn hình và wireframe FE-12** (không làm trong đợt này, theo quyết định BA từ 08/11/2026; **không bỏ**).
Màn 12.1–12.4 trong Report 3 §III vẫn vẽ luồng ký cũ (n/m chữ ký, niêm phong) và trình soạn văn bản thuần; code đã khác hẳn.
Bảng as-built và kế hoạch làm khi mở khoá nằm ở [§9](#9-pending--đặc-tả-màn-hình-fe-12). Trong lúc chờ, §III giữ nguyên nhưng
**phải có một câu cảnh báo đầu mục 12** (việc 33-13 ở §8) để người đọc không tin màn ký là hiện hành.

## 3. Việc BA — sequence và Report 4

1. Sửa 4 file `.txt` cho khớp code (liệt kê ở comment `10492`): khoá dòng cuộc họp và biên bản (`FOR UPDATE`) trong transaction; thông báo
   lưu **cùng lần lưu** với trạng thái, lưu lỗi thì không đổi gì và không ai nhận thông báo (`MSG404`); tên hàm theo code
   (`ReturnAsync`, `CorrectAsync`, không phải `rejectAsync`, `createCorrectionAsync`); UC-83 thêm nhánh tạo đính chính đồng thời → `MSG434`.
2. Sửa sequence UC-58 (bỏ "issued minutes are not part of this release"; bản đã ban hành, mở bản cũ, tải về), UC-59 (`MSG456`), UC-60.
3. Xuất PNG cho UC-61/64/65/83 (`drawio-png`), thêm tab vào `TrustCabinet_Sequence_Diagrams.drawio`, bỏ chữ "(sơ bộ)" trong
   [README thư mục](../../06-sequencediagram/minutes-management/README.md).
4. Report 4 DOCX: thêm mục **FE-12 – Meeting Minutes** giữa FE-11 và FE-13; class diagram FE-12 chưa có — vẽ mới theo
   [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md) hoặc ghi "chưa có class diagram" như FE-11 (cần quyết định ở §5); nhúng sequence UC-58/59/60/61/64/65/83.

## 4. Chỗ code lệch đặc tả cần ghi nhận (không sửa code)

| # | Lệch | Nguồn |
|---|---|---|
| L1 | Tải Word/PDF bản đã ban hành: mọi người đọc (trừ Khách), không còn "Chủ toạ/Thư ký" như bản nháp spec; họp Mật không ai tải | PO 08/11, mô tả PR #297 |
| L2 | `canSubmit`, `canApprove` thêm vào `MinutesState` lúc làm FE để màn hình khỏi đoán vai | PR #297 — chỉ cần có trong contract, BA không sửa UC |
| L3 | Mở thông báo biên bản vào trang cuộc họp, chưa mở thẳng tab Biên bản (cần target type mới ở BE) | PR #297, mục "Việc còn lại" của note sơ bộ |
| L4 | Cảnh báo "……" còn trống (UC-61 AF-1) là FE, server không chặn | UC-61 AF-1 |

## 5. Câu hỏi còn mở

- Class diagram FE-12: vẽ mới hay ghi chú "chưa có" như FE-11? **Đề xuất:** ghi chú trước, vẽ khi còn thời gian — Report 4 chưa có mục FE-12 nào nên làm phần sequence trước.
- **PO chốt 11/11/2026:** Q5 (Thư ký rút lại bản đã gửi duyệt) và Q6 (gửi email khi ban hành biên bản) là
  **ngoài phạm vi**; eCab cũng chưa có hai việc này. Việc kéo theo: ghi "ngoài phạm vi" vào Report 3 DOCX —
  UC-61 (không rút lại được bản đã gửi duyệt) và UC-64 (ban hành chỉ báo trong ứng dụng, không gửi email) — rồi
  `sync_reports.py --report 3` và `check_docx.py`. **Chưa sửa DOCX** (snapshot 11/11 chưa có câu này).

## 6. Jira — làm sau khi PR docs vào `main`

1. Comment SCRUM-33 (trả lời comment `10492`): UC / BR / MSG đã đổi, PR docs và commit merge (kiểm `git log origin/main`), việc còn lại (màn hình 12.1–12.4, class diagram FE-12); ghi quyết định PO 11/11/2026: Q5 rút lại và Q6 email ban hành là ngoài phạm vi.
2. Không đổi trạng thái (`In Review` giữ nguyên). SCRUM-37 không đụng tới.
3. Nhắc Tester: vòng thử 3 người (Thư ký / Chủ toạ / thành viên), `MSG401` khi bản cũ, bản đính chính, họp Mật không tải.

## 7. Checklist

- [x] §8 Pha 0 → Pha 7 (kế hoạch chi tiết) — 11/11/2026, trừ Pha 7 (chờ PR docs vào `main`)
- [x] §2: 33-1 → 33-13 (DOCX Report 3, `sync_reports.py --report 3`, `check_docx.py` OK) — 11/11/2026
- [x] §3: sequence `.txt` + PNG + tab drawio; Report 4 FE-12 — 11/11/2026
- [x] §5: PO chốt Q5, Q6 ngoài phạm vi — 11/11/2026
- [x] §5: ghi "ngoài phạm vi" vào UC-61, UC-64 trong Report 3 DOCX + sync — xong 11/11/2026, PR #322
- [ ] §6: comment Jira sau khi PR docs đã vào `main` — **chưa làm**, chưa commit
- [ ] **PENDING** §9: đặc tả màn hình và wireframe FE-12 (chưa làm; mở khoá khi PO/BA đồng ý)

## 8. Kế hoạch chi tiết

Thứ tự bắt buộc: BR → UC → MSG → inventory → sync → sequence → Report 4 → PR. Lý do: UC trích số BR, MSG trích UC; sync
chạy một lần ở cuối Pha 2 thì không phải sinh lại nhiều lần. Mỗi pha kết thúc bằng một **cổng** (điều kiện qua pha sau).
Ước lượng thô để xếp lịch: Pha 1–2 nửa ngày, Pha 3 nửa ngày, Pha 4–5 một buổi, Pha 6–7 một giờ.

### Pha 0 — Chuẩn bị (15 phút)

1. Nhánh làm việc: `docs/ba-final-review-pr297-pr311` (đã tạo từ `origin/main`). Nếu làm SCRUM-33 trong PR riêng, tách `docs/ba-final-review-scrum-33` từ nhánh này.
2. Đọc theo thứ tự trong [AGENTS.md — Documentation](../../../AGENTS.md#documentation): `docs/README.md`, `docs/08-reports/README.md`,
   `docs/06-sequencediagram/minutes-management/README.md`, [plan 05 §8](05-plan-sua-uc-br-msg-diagram.md#8-thứ-tự-và-cách-chạy-agent-đọc-mục-này-trước).
3. Chạy `python scripts/check_docx.py` **trước khi sửa** và chép số lỗi / cảnh báo hiện có vào ghi chú PR, để phân biệt lỗi cũ với lỗi mình gây ra.
4. Đọc code làm nguồn đối chiếu (đọc, không sửa): `backend/TrustCabinet.Infrastructure/Summaries/MinutesService.cs`,
   `backend/TrustCabinet.Web/Endpoints/MinutesEndpoints.cs`, `docs/03-api/17-du-thao-bien-ban.md` (mục "Duyệt và ban hành"),
   `tests/TrustCabinet.Web.Tests/Summaries/MinutesApprovalEndpointTests.cs` (mỗi `[Fact]` là một nhánh UC đã chạy thật).
5. Mở DOCX `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx` bằng Word; sửa trực tiếp, không bật Track Changes (các lượt BA trước ghi thay đổi ở Change Log, không bằng revision của Word — kiểm lại nếu nghi ngờ).

**Cổng 0:** biết số lỗi `check_docx.py` ban đầu; đã đọc xong 4 nguồn ở bước 4.

### Pha 1 — Report 3 DOCX (nửa ngày, làm liền một lượt)

Quy tắc chung: nguồn chữ là [note sơ bộ](../NOTE-dac-ta-so-bo-scrum-33.md) mục 3–5, **nhưng code thắng** khi hai bên lệch; mỗi chỗ
lệch ghi vào §4 của note này. Giữ mẫu Report 3: nhãn dòng chuẩn, `PRE-n/POST-n/AF-n/EX-n`, Normal Flow đánh số 1..n, BR trong ngoặc, MSG
trích nguyên văn catalogue. Không có chi tiết kỹ thuật (tên bảng, endpoint) trong UC.

| Bước | Việc | Vị trí trong DOCX | Đối chiếu code trước khi gõ |
|---|---|---|---|
| 1.1 | Thêm **BR-404 → BR-413** (10 dòng) vào bảng BR; đặt BR-388 sửa "submitted for signing" → "submitted for approval" và thêm "a returned revision can be corrected again" | Bảng BR ở §V: BR-446 hiện là dòng cuối; chèn BR-404 → BR-413 **đúng thứ tự số** (sau BR-403), không nối cuối | BR-412: tải Word/PDF theo Q7, Khách không thấy, họp Mật `MSG405` (`ExportAsync`) |
| 1.2 | **UC-61** viết đủ, đổi tên thành *Submit Meeting Minutes for Approval* | §12.4 (dòng 2357) | `SubmitAsync`: nhánh 403 `MSG425` chỉ khi caller là Chủ toạ; người ngoài 404 không mã; `MSG426`, `MSG400`, `MSG401`, `MSG404`; AF-1 "……" là cảnh báo FE, server không chặn |
| 1.3 | **UC-62, UC-63** giữ placeholder, đổi mô tả "dành cho ký số mô phỏng SCRUM-37" và đổi tên UC-63 nếu cần | §12.5, §12.6 | Không cần đối chiếu |
| 1.4 | **UC-64** viết đủ, đổi tên *Approve and Issue Meeting Minutes*, actor Chair | §12.7 | `ApproveAsync`: ai nhận thông báo (**mọi người đọc được cuộc họp, trừ Khách và Chủ toạ** — comment `10492`); bản bị đính chính → `Superseded`; `MSG427/428/429`; ghi nhật ký BR-446 (nhắc ở POST) |
| 1.5 | **UC-65** viết đủ, đổi tên *Return Meeting Minutes for Revision*, actor Chair; thêm câu "lý do của lần trả cuối thay lý do cũ" | §12.8 | `ReturnAsync`: lý do cắt khoảng trắng, 1–2.000 ký tự `MSG431`; `ReturnedById/At`; thông báo `MinutesReturned` tới Thư ký |
| 1.6 | **UC-83** thêm mới *Correct Issued Meeting Minutes*, `<<extend>>` UC-58, actor Chair/Secretary | Sau UC-65 trong FE-12 (đánh số mục theo thứ tự hiện có) | `CorrectAsync`: `MSG433`, `MSG434`; **tạo đồng thời → `MSG434`** (thêm EX hoặc gộp vào EX-3, ghi rõ); EX-1 dùng `MSG397` như note sơ bộ — kiểm thật bằng test |
| 1.7 | **UC-58** sửa | §12.1 (dòng 2324) | Bỏ "nobody else sees them"; thêm AF: người đọc (trừ Khách) xem bản `Issued` mới nhất, AF mở bản cũ qua `history`; tải Word/PDF; bản chưa ban hành người đọc nhận 404 |
| 1.8 | **UC-59** sửa | §12.2 | Thêm EX `MSG456`; PRE: bản mới nhất là nháp **hoặc** bị trả lại |
| 1.9 | **UC-60** sửa | §12.3 | PRE-2 thêm "bị trả lại"; áp cho bản đính chính; `MSG400` chỉ cho bản chờ duyệt / đã ban hành; tải bản đã ban hành theo Q7 |
| 1.10 | Bảng UC tổng quan FE-12 (dòng 802–806) và tên FE-12 | Mục I (bảng UC) và tiêu đề §12 "Meeting Minutes & Digital Signing" | Đổi tên 61/64/65, thêm UC-83; tiêu đề đổi thành *Meeting Minutes & Approval* (kiểm mọi chỗ trích tên cũ: tìm "Digital Signing") |
| 1.11 | UC diagram FE-12 (hình) | Mục UC diagram | Đổi nhãn UC-61/64/65, thêm UC-83 `<<extend>>` UC-58; làm theo `DRAWIO_EDITING_RULES.md` nếu hình là drawio, nếu là ảnh nhúng thì xuất lại |
| 1.12 | System Messages §V.2 | Bảng MSG | Thêm `MSG424`–`MSG434`, `MSG456` theo catalogue `docs/03-api/00-danh-muc-ma-thong-bao.md` dòng 856–888; cột UC ghi đúng nhánh |
| 1.13 | **Câu cảnh báo đầu mục 12 màn hình** (việc 33-13) | §III mục 12, ngay dưới tiêu đề | Một câu: "Màn 12.2–12.4 mô tả luồng ký đã thay bằng luồng duyệt và ban hành (SCRUM-33); đặc tả màn hình đang chờ cập nhật." |
| 1.14 | Change Log | Cuối Report 3 | Một dòng ngày làm, ghi: UC-61/64/65 viết, UC-83 mới, UC-58/59/60 sửa, BR-404 → BR-413, MSG424–434 và 456, tên FE-12 |

**Cổng 1:** tìm trong DOCX không còn "Submit Meeting Minutes for Signing", "Finalize Meeting Minutes", "Request Revision of Meeting Minutes" (trừ Change Log
lịch sử); mọi `BR-4xx`, `MSGxxx` trong UC mới đều tồn tại trong bảng BR / System Messages; UC-83 xuất hiện đúng một lần.

### Pha 2 — Sync và kiểm tra (30 phút)

```bash
python scripts/check_docx.py docs/08-reports/source/Report_3_Software_Requirement_Specification.docx
python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3
python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3   # lần hai không được đổi file
git status --short docs/                 # mong đợi: DOCX, Report-3 .md, 05/06 extracts, uc-map.html
```

1. Không sửa tay `Report-3_….md`, `05-use-case-specifications.md`, `06-business-rules.md` ([AGENTS.md — Reports](../../../AGENTS.md#documentation)).
2. Đọc diff snapshot: UC-61/64/65/83 xuất hiện, BR-404 → BR-413 xuất hiện, không dòng nào ngoài phạm vi bị đổi (nếu đổi bất thường → hoàn tác DOCX từ `git` và làm lại, đừng sửa snapshot).
3. `python scripts/trace_br.py` (nếu chạy được) để thấy BR nào chưa gắn UC — BR-404 → BR-413 phải đủ cột UC.
4. Catalogue và `MessageCodeCatalogueTests` không đổi (catalogue đã có từ #297). Chạy `dotnet test --filter MessageCodeCatalogueTests` **chỉ khi** sửa catalogue.

**Cổng 2:** `check_docx.py` không thêm lỗi so với Pha 0; lần sync thứ hai không đổi file.

### Pha 3 — Master inventory và README (20 phút)

[`docs/01-requirements/00-master-use-case-inventory.md`](../../01-requirements/00-master-use-case-inventory.md) là Markdown viết tay, **không** phải extract:

1. Dòng 233–237: đổi tên và actor UC-61 *Gửi duyệt biên bản*, UC-64 *Duyệt và ban hành* (Chair), UC-65 *Trả lại biên bản* (Chair); UC-62, UC-63 ghi "để dành SCRUM-37"; cột trạng thái "Đã đặc tả" cho 61/64/65.
2. Thêm dòng UC-83 *Tạo bản đính chính* vào đúng nhóm; sửa dòng 14 ("UC-82, UC-83 thuộc SCRUM-30/SCRUM-33") nếu baseline đổi.
3. Dòng 355–356 (luồng "Gửi ký → Ký → niêm phong"): viết lại thành luồng duyệt một người.
4. Không đụng bảng đổi số cũ dòng 276–307 (UC-83 cũ → UC-64 là lịch sử đổi số 10/10/2026).
5. `docs/notes/README.md`: thêm dòng cho note 41 nếu thư mục đó liệt kê note; `docs/03-api/README.md` đã có.

**Cổng 3:** tìm "Gửi ký" / "niêm phong" trong `docs/01-requirements/` chỉ còn ở lịch sử.

### Pha 4 — Sequence (nửa ngày)

Làm file `.txt` trước, PNG sau. Cú pháp sequencediagram.org như các file hiện có; thông điệp tiếng Anh, lifeline đúng lớp
(boundary / control / entity). Mỗi file đối chiếu với `MinutesService` ở các hàm tương ứng.

| File | Sửa |
|---|---|
| `UC61_SubmitMeetingMinutesForApproval.txt` | Thêm bước khoá dòng cuộc họp và dòng biên bản (`FOR UPDATE`) trong transaction; thông báo Chủ toạ lưu **cùng lần lưu** với trạng thái; nhánh lưu lỗi → không đổi gì, không thông báo, `MSG404`; nhánh `MSG401` (bản cũ) |
| `UC64_ApproveAndIssueMeetingMinutes.txt` | Như trên; đổi `approveAsync` theo tên code; thông báo cho mọi người đọc được cuộc họp trừ Khách và Chủ toạ; ghi nhật ký `minutes.issued` cùng transaction (BR-446); AF bản đính chính → bản cũ `Superseded` |
| `UC65_ReturnMeetingMinutesForRevision.txt` | `rejectAsync` → `ReturnAsync`; kiểm lý do; thông báo Thư ký cùng lần lưu; ghi `ReturnedById/At` |
| `UC83_CorrectIssuedMeetingMinutes.txt` | `createCorrectionAsync` → `CorrectAsync`; nhánh tạo đồng thời → `MSG434` |
| `UC58_ViewMeetingMinutes.txt` | Bỏ chú thích "issued minutes are not part of this release"; thêm nhánh người đọc xem bản đã ban hành, `history` lọc ở server, mở bản cũ `GET /api/minutes/{id}`, 404 với bản chưa ban hành |
| `UC59_GenerateMeetingMinutes.txt` | Nhánh `rebuild: true` trên bản đính chính → `MSG456`; điều kiện bản nháp **hoặc bị trả lại** |
| `UC60_EditMeetingMinutes.txt` | Bản `Rejected` sửa được; `MSG400` đúng điều kiện; tải bản đã ban hành theo Q7, `MSG405` cho họp Mật |

Sau khi sửa `.txt`:

```bash
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/minutes-management/UC61_SubmitMeetingMinutesForApproval.txt
# lặp cho UC64, UC65, UC83, UC58, UC59, UC60 (script ghép tab vào TrustCabinet_Sequence_Diagrams.drawio và xuất .drawio.png)
python scripts/sync_sequence_drawio_png.py        # nếu cần xuất lại toàn bộ; đặt DRAWIO_EXECUTABLE nếu CLI chưa trong PATH
```

1. Mở từng PNG, đọc chữ, kiểm không bị cắt và không sai tên MSG.
2. Bỏ chữ "(sơ bộ)" trong [README thư mục](../../06-sequencediagram/minutes-management/README.md) và thêm bảng UC → file → hàm code.
3. Cập nhật [docs/06-sequencediagram/README.md](../../06-sequencediagram/README.md) nếu nó liệt kê file.
4. Tab `TrustCabinet_Sequence_Diagrams.drawio`: kiểm `git diff --stat` chỉ đổi các tab minutes, không tab nào khác.

**Cổng 4:** 7 PNG tồn tại, mở xem đúng; `git diff --stat` của drawio hợp lý (không hàng nghìn dòng ngoài tab).

### Pha 5 — Report 4: mục FE-12 (một buổi)

DOCX: `docs/08-reports/source/Report-4_Software Design Specification.docx`. Report 4 chưa có script sync trong repo; đọc `docs/08-reports/README.md` để biết nó có sinh Markdown không trước khi sửa.

1. Thêm mục II **FE-12 – Meeting Minutes** giữa FE-11 và FE-13, cùng khuôn với FE-11 (đọc FE-11 làm mẫu).
2. Nhúng PNG sequence UC-58, 59, 60, 61, 64, 65, 83 từ Pha 4.
3. Class diagram: **quyết định ở Câu hỏi §5.** Tạm thời ghi "Chưa có class diagram cho FE-12" đúng câu FE-11 đang dùng.
4. Mục lục Report 4 cập nhật (Word: cập nhật field).
5. `python scripts/check_docx.py "docs/08-reports/source/Report-4_Software Design Specification.docx"`.

**Cổng 5:** `check_docx.py` sạch cho Report 4; mục FE-12 có đủ 7 sequence.

### Pha 6 — PR docs (30 phút)

1. Commit nhỏ theo pha, **tiếng Việt**: `[Docs] SCRUM-33: Report 3 — UC-61/64/65/83, BR-404 → BR-413`, `[Docs] SCRUM-33: sequence minutes theo code`, `[Docs] SCRUM-33: Report 4 mục FE-12`, `[Docs] SCRUM-33: inventory`.
2. Tick checklist §7 và sửa cột Jira của dòng 41 trong [README](README.md) trong **cùng commit** đổi trạng thái (§2.2).
3. PR: tiêu đề **tiếng Anh**, ví dụ `[Docs] SCRUM-33 BA Final Review: Report 3 UC-61/64/65/83, BR-404–413, minutes sequences, Report 4 FE-12`; mô tả liệt kê UC / BR / MSG đã đổi, lệch §4, và phần **PENDING** (§9).
4. Chờ CI (job `changes` chạy test đọc `docs/`); sửa nhánh nếu đỏ, không tắt bước.

### Pha 7 — Đóng ticket (sau khi PR **đã vào `main`**)

1. `git fetch && git log --oneline origin/main` thấy commit merge (không tin trang PR — vụ #238).
2. Comment SCRUM-33, trả lời `10492`: UC/BR/MSG đã đổi, PR docs + commit, việc còn lại = **PENDING §9**. Không đổi trạng thái (`In Review` giữ).
3. Cập nhật dòng 41 trong README: ghi PR docs và "đã comment `<id>`".
4. Báo Tester kịch bản thử (§6.3).

## 9. PENDING — đặc tả màn hình FE-12

**Trạng thái: PENDING.** Chưa làm đợt này. Gộp với phần "đặc tả màn hình 12.1 tạm hoãn" của note [34](34-pr263-scrum-32-bien-ban-lay-du-thao-hien-co.md) và [35](35-pr265-pr267-scrum-35-36-32-132-viec-ba-moi.md): một lần đặc tả cho cả SCRUM-32 và SCRUM-33.
**Điều kiện mở khoá:** PO/BA đồng ý làm đặc tả màn hình; code tab Biên bản đã ổn (không còn đổi giao diện).

### 9.1 Màn hiện có trong Report 3 và việc phải làm

| Mục Report 3 §III | Nội dung hiện tại | Việc |
|---|---|---|
| 12.1 Minutes Editor | Soạn văn bản thuần + xem trước, chỉ tải Word, "UC-61–65 chưa làm" | Viết lại theo tab Biên bản hiện tại (bảng 9.2); bỏ mô tả cú pháp `##`, `@@`; ghi nút theo vai |
| 12.2 Sign Minutes Dialog | Luồng ký của người ký (n/m chữ ký) | Thay bằng hộp thoại **Duyệt và ban hành** và **Trả lại** của Chủ toạ (9.3) |
| 12.3 Signing Status & Finalization | Bảng người ký, lịch sử ký, niêm phong | Thay bằng **lịch sử phiên bản và bản đính chính** (9.4) |
| 12.4 Finalized Meeting Minutes | Bản niêm phong, khối chữ ký | Thay bằng **bản đã ban hành cho người đọc** (9.5) |
| Wireframe | `docs/wireframe/*` | Vẽ lại 4 màn; làm theo [plan 06](06-plan-dac-ta-man-hinh.md) |

### 9.2 As-built: thẻ "Biên bản" (`MinutesPanel.tsx`, `MinutesEditor.tsx`)

| Phần tử | Hiện trong code | Điều kiện hiện |
|---|---|---|
| Tiêu đề + badge | "Biên bản" + badge: **Bản nháp** (slate), **Chờ duyệt** (violet), **Bị trả lại** (amber), **Đã ban hành** (emerald), **Đã thay thế** (slate) | Luôn khi có biên bản |
| Chọn phiên bản | Select "Phiên bản biên bản": **Bản gốc**, **Đính chính n**; gợi ý là nhãn trạng thái | Có hơn 1 bản trong `history` |
| Tải xuống | Menu: Word, PDF | `canDownload` (Q7: bản đã ban hành mọi người đọc trừ Khách; họp Mật không ai) |
| Tạo bản nháp | Nút chính | Chưa có biên bản và `canBuild` |
| Tạo lại | Nút phụ; hộp thoại "Tạo lại bản nháp biên bản?" cảnh báo mất chỗ đã sửa | Nháp / bị trả lại, không áp cho bản đính chính (`MSG456`) |
| Sửa | Mở trình soạn; nút **Lưu** / **Huỷ**; "Đang lưu…" | Nháp hoặc bị trả lại, Chủ toạ/Thư ký, cuộc họp InProgress hoặc Ended |
| Gửi duyệt | Hộp thoại "Gửi biên bản cho Chủ toạ duyệt?"; cảnh báo đậm "Biên bản còn chỗ “……” chưa điền."; chặn khi cuộc họp chưa Ended: "Chỉ gửi duyệt biên bản sau khi cuộc họp kết thúc." | Thư ký (`canSubmit`) |
| Dòng lý do trả lại | "<tên> trả lại: <lý do>" phía trên bản biên bản | Bị trả lại, người quản lý |
| Thông báo nền | Cảnh báo dự thảo AI chưa có / chưa duyệt / cũ ("Chưa có dự thảo AI nên mục I–III để trống." …) kèm nút **Duyệt dự thảo**, **Tạo lại** | Theo trạng thái dự thảo AI (SCRUM-36) |
| Dòng mô tả cuối | "<tên> gửi duyệt lúc …" / "<tên> duyệt, ban hành lúc …" / "Lưu lần cuối …" | Luôn |
| Trạng thái trống | "Bản nháp điền sẵn thông tin cuộc họp và dự thảo vào mẫu biên bản." / "Chưa có biên bản." / "Chưa có biên bản ban hành." | Theo vai |

### 9.3 As-built: hộp thoại Chủ toạ

| Hộp thoại | Nội dung | Nút |
|---|---|---|
| Duyệt và ban hành biên bản? | "Biên bản được khoá và gửi tới mọi người dự họp. Muốn sửa sau đó phải tạo bản đính chính." | **Duyệt và ban hành** ("Đang ban hành…") |
| Trả lại biên bản | Ô "Lý do trả lại" (bắt buộc), placeholder "Ví dụ: Bổ sung kết luận của mục 2.", đếm ký tự tối đa 2.000, gợi ý "Thư ký sẽ thấy lý do này.", lỗi "Nhập lý do trả lại, tối đa 2.000 ký tự." | **Trả lại** ("Đang trả lại…") |
| Xung đột 409/404 | Đóng hộp thoại, toast lý do (`MSG401`…), tải lại thẻ | — |

### 9.4 As-built: bản đính chính

| Phần tử | Nội dung |
|---|---|
| Nút | **Tạo bản đính chính** (nút phụ) trên bản đã ban hành, Chủ toạ/Thư ký |
| Hộp thoại | "Tạo bản đính chính?" — "Bản mới chép nội dung đang ban hành để sửa, rồi gửi duyệt như bản đầu. Mọi người vẫn đọc bản đang ban hành cho tới khi bản đính chính được duyệt." |
| Xem bản cũ | Banner "Đang xem <bản>, <trạng thái>." + nút **Xem bản mới nhất** |

### 9.5 As-built: người đọc (không phải Chủ toạ/Thư ký)

Thấy bản `Issued` mới nhất, chọn được bản cũ trong `history` (chỉ `Issued`/`Superseded`), tải Word/PDF, **không có nút hành động**; chưa có biên bản ban hành thì chỉ thấy "Chưa có biên bản ban hành."; Khách không có thẻ này.

### 9.6 Khi mở khoá PENDING — các bước

1. Chạy app ([verify-ui-in-browser](../../../.claude/skills/verify-ui-in-browser/SKILL.md)), đăng nhập 3 vai (Thư ký, Chủ toạ, thành viên); chụp ở 1440px và 390px: nháp, trình soạn, hộp thoại gửi duyệt (có và không có "……"), chờ duyệt (Chủ toạ), hộp thoại Trả lại, bị trả lại (Thư ký), đã ban hành (thành viên), bản đính chính, banner xem bản cũ, trạng thái trống từng vai.
2. Viết lại 12.1–12.4 theo bảng 9.2–9.5, mỗi hàng "Field Description" một phần tử; ngôn ngữ UI tiếng Việt trong ngoặc vuông.
3. Hỏi PO còn sót: nút **Tải xuống** nằm ở đâu trên màn hẹp; có cần màn riêng cho lịch sử phiên bản không (hiện là Select).
4. Wireframe 4 màn, cập nhật `scopeData.ts` chỉ nếu thay đổi phạm vi (không).
5. Đi qua Pha 1–2 (sửa DOCX → sync → `check_docx.py`) như một PR riêng; mở note mới và ghi vào README.
