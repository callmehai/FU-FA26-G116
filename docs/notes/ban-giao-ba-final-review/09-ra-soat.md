# 09 · Rà soát căn cứ pháp lý, ADR điểm danh và mã thông báo (BA Final Review 24/10/2026)

Ghi chú chỉ đọc, sinh ra từ phiên rà soát. Không sửa file có sẵn; đề xuất sửa nằm ở đây để người phụ trách áp dụng. Căn cứ mới: Luật 91/2025/QH15 và NĐ 356/2025/NĐ-CP thay NĐ 13/2023/NĐ-CP (theo [05-plan](05-plan-sua-uc-br-msg-diagram.md) mục 4.4).

Lệnh đã chạy: `grep -rn "13/2023"` trên `docs/`, `README.md`, `AGENTS.md`, `deploy/`; unzip `Report_3_Software_Requirement_Specification.docx` rồi đọc `word/document.xml`; `grep -rn "ADR-0024" docs/`; đối chiếu `docs/03-api/00-danh-muc-ma-thong-bao.md` với bảng System Messages §V.2 của DOCX.

## 1. Chỗ còn trích NĐ 13/2023

Tổng: 3 chỗ trong DOCX Report 3, 3 chỗ trong bản `.md` Report 3, 20 dòng tài liệu/ghi chú, 2 dòng mục AGENTS/README, 0 dòng trong `deploy/`. Mục "Thẩm định" dưới đây là ghi chú của phiên này, không phải quyết định của BA.

### 1.1 Report 3 DOCX (`docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`)

| # | Mục / heading gần nhất | Câu hiện tại | Đề xuất thay |
|---|---|---|---|
| D1 | III.9.2 Upload Audio Dialog (trang 168) — checkbox đồng ý ghi âm | "Tôi xác nhận người tham dự đã được thông báo và đồng ý ghi âm theo Nghị định 13/2023/NĐ-CP." [I confirm that attendees were informed and consented to the recording under Decree 13/2023/ND-CP.] | "Tôi xác nhận người tham dự đã được thông báo và đồng ý ghi âm theo Luật Bảo vệ dữ liệu cá nhân số 91/2025/QH15 và Nghị định 356/2025/NĐ-CP." [I confirm that attendees were informed and consented to the recording under Law No. 91/2025/QH15 on Personal Data Protection and Decree 356/2025/ND-CP.] |
| D2 | IV.2 Quality Attributes › 2.3 Security — hàng "Personal data protection and audio consent" | "(Decree 13/2023/ND-CP)" trong tên hàng | "(Law 91/2025/QH15 and Decree 356/2025/ND-CP)" |
| D3 | IV.2 Quality Attributes › 2.8 Compliance — hàng "Personal data protection" | "complies with Decree 13/2023/ND-CP on personal data protection" | "complies with Law 91/2025/QH15 on Personal Data Protection and Decree 356/2025/ND-CP" |

Thẩm định: D1 là chữ trên màn hình (UI), nên chỉ đổi chữ, giữ wireframe cho tới khi có màn (đúng ghi chú III.9.2 trong [06-plan](06-plan-dac-ta-man-hinh.md) dòng 72). Cùng câu này đang nằm trong wireframe v5–v7 (xem 2.6). D2, D3 là văn bản đặc tả, đổi thẳng.

### 1.2 Report 3 bản `.md` (`docs/08-reports/Report-3_Software_Requirement_Specification.md`)

Cùng nội dung với DOCX; cần đổi cùng lúc để hai bản khớp.

| # | Dòng | Ghi chú |
|---|---|---|
| M1 | 3160 | Giống D1 |
| M2 | 3766 | Giống D2 (hàng "Personal data protection and audio consent") |
| M3 | 3809 | Giống D3 |

### 1.3 Tài liệu, ADR và ghi chú trong `docs/`, `README.md`, `AGENTS.md`

| # | File:dòng | Câu hiện tại (trích) | Đề xuất thay |
|---|---|---|---|
| T1 | `README.md:198` | "…đồng ý và xoá dữ liệu theo Nghị định 13/2023/NĐ-CP" (mục voiceprint) | "…đồng ý và xoá dữ liệu theo Luật 91/2025/QH15 và Nghị định 356/2025/NĐ-CP" |
| T2 | `AGENTS.md:305–306` | "…personal data under Vietnam's Decree 13/2023/NĐ-CP." | "…personal data under Vietnam's Law 91/2025/QH15 on Personal Data Protection and Decree 356/2025/NĐ-CP." |
| T3 | `docs/02-design/02-erd.md:655` | "Dữ liệu sinh trắc học theo Nghị định 13/2023/NĐ-CP." | "Dữ liệu sinh trắc học theo Luật 91/2025/QH15 và Nghị định 356/2025/NĐ-CP." |
| T4 | `docs/02-design/02-erd.md:663` | "Nghĩa vụ theo Nghị định 13/2023/NĐ-CP, không phải thứ trang…" | "Nghĩa vụ theo Luật 91/2025/QH15 và Nghị định 356/2025/NĐ-CP, không phải thứ trang…" |
| T5 | `docs/02-design/04-luong-nghiep-vu-chinh.md:159` | "*Dữ liệu cá nhân theo Nghị định 13/2023/NĐ-CP:*" | "*Dữ liệu cá nhân theo Luật 91/2025/QH15 và Nghị định 356/2025/NĐ-CP:*" |
| T6 | `docs/02-design/08-ke-hoach-phong-hop-truc-tuyen.md:127` | Tiêu đề "### 3.6 Dữ liệu cá nhân (Nghị định 13/2023/NĐ-CP)" | "### 3.6 Dữ liệu cá nhân (Luật 91/2025/QH15, Nghị định 356/2025/NĐ-CP)" |
| T7 | `docs/DEVELOPMENT_WORKFLOW.md:268` | "…personal data under Decree 13/2023/NĐ-CP. When touching this…" | "…personal data under Law 91/2025/QH15 and Decree 356/2025/NĐ-CP. When touching this…" (file tiếng Anh, theo AGENTS) |
| T8 | `docs/05-decisions/0005-khach-ngoai-tham-du-bang-link-moi.md:50` | Tiêu đề "### Nghĩa vụ theo Nghị định 13/2023/NĐ-CP" | ADR đã **Đã chấp nhận** — AGENTS cấm sửa ADR đã chấp nhận. Đề xuất: ADR mới hoặc ghi chú cập nhật có ngày, không sửa thân ADR. BA quyết định. |
| T9 | `docs/05-decisions/0010-phong-hop-truc-tuyen-dung-livekit-sfu.md:87` | "8. **Tối thiểu dữ liệu cá nhân** (Nghị định 13/2023/NĐ-CP):" | Cùng xử lý như T8 (ADR đã chấp nhận). |
| T10 | `docs/05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md:75` | "*Sửa khi review PR #128:* bản đề xuất trích Nghị định 13/2023/NĐ-CP…" | Giữ nguyên: đây là ghi chú lịch sử đã sửa. ADR-0024 đang ở trạng thái **Đề xuất**, nếu cần có thể thêm câu căn cứ mới. |
| T11 | `docs/04-meetings/2026-10-16-trao-doi-po.md:117` | "…kéo theo nghĩa vụ NĐ 13/2023/NĐ-CP…" | Giữ nguyên: biên bản cuộc họp cũ, ghi lại lúc đó. |
| T12 | `docs/notes/NOTE-gop-y-hoi-dong-lan-1.md` dòng 15, 32, 64, 103, 105, 123 | Ghi chú góp ý hội đồng, trích NĐ 13/2023 | Giữ nguyên: ghi lại góp ý gốc. Dòng 103 và 123 nói về việc viết UC lưu trữ–xoá theo NĐ cũ; nếu cần thì ghi chú thêm, không sửa. |
| T13 | `docs/notes/README.md:20` | "…NĐ 13/2023, chữ ký số…" | Đổi thành "…Luật 91/2025/QH15 và NĐ 356/2025/NĐ-CP, chữ ký số…" (mô tả chủ đề của ghi chú). |
| T14 | `docs/notes/ban-giao-ba-final-review/03-voice-id-scrum-31-35.md:17` | Đã ghi "Chỗ nào đang dẫn NĐ 13/2023 thì đổi theo" | Chỉ là ghi chú hướng dẫn, không cần sửa. |
| T15 | `docs/notes/ban-giao-ba-final-review/06-plan-dac-ta-man-hinh.md:72` | Việc cần làm: đổi câu đồng ý ghi âm sang Luật 91/2025/QH15 + NĐ 356/2025/NĐ-CP | Đã đúng hướng; việc còn lại là làm. |
| T16 | `docs/notes/ban-giao-ba-final-review/05-plan-sua-uc-br-msg-diagram.md` dòng 128, 131, 272 | Đã ghi căn cứ mới và việc rà soát | Đúng; không sửa. |

Đề xuất thêm: T1–T7 là đổi chữ đơn giản, làm cùng lượt với D1–D3 và M1–M3 để tránh lệch nhau.

### 1.4 Wireframe và bản sinh từ wireframe (chứa câu checkbox đồng ý ghi âm)

| # | File:dòng | Ghi chú |
|---|---|---|
| W1 | `docs/wireframe/TrustCabinet-wireframe-v5.html:985` | Câu checkbox "…theo Nghị định 13/2023…" trong dialog upload audio |
| W2 | `docs/wireframe/TrustCabinet-wireframe-v6.html:1118` | Giống W1 |
| W3 | `docs/wireframe/TrustCabinet-wireframe-v7.html:1160` | Giống W1 |
| W4 | `docs/wireframe/figma-v5/code.js`, `figma-v6/code.js`, `figma-v7/code.js` (mỗi file 1 dòng) | Giống W1, bản Figma plugin |
| W5 | `docs/wireframe/_tools/out/v4.json`, `v5.json`, `v6.json`, `v7.json` | Dữ liệu sinh từ wireframe, 1 dòng mỗi file |

Đề xuất: không sửa trong lượt này (06-plan dòng 72 nói giữ wireframe đến khi có màn). Khi sửa D1 thì sửa W1–W5 cùng câu, hoặc sinh lại JSON từ tool. `deploy/`: không có chỗ nào.

## 2. `grep -rn "ADR-0024" docs/` — chỗ nói về điểm danh

**Kết quả: không có dòng nào trong `docs/` dẫn ADR-0024 cho điểm danh.** Mọi chỗ dẫn ADR-0024 đều nói về lưu tệp tài liệu trên R2. Điều này khớp với ghi chú [patch-scrum-26](note-may-nha/patch-scrum-26.md) dòng 85.

Danh sách các dòng ADR-0024 đã kiểm (đều là tài liệu, không phải điểm danh):

| File:dòng | Nội dung |
|---|---|
| `docs/02-design/02-erd.md:43, 224, 261` | `StoredFile`, khoá `stored-files/{Id}` |
| `docs/02-design/03-co-so-du-lieu.md:105` | Mục kho tệp tài liệu |
| `docs/03-api/11-tai-lieu-cuoc-hop.md:4, 114, 148` | API tài liệu cuộc họp |
| `docs/05-decisions/0024-…md:1` | Chính ADR |
| `docs/06-sequencediagram/document-management/README.md:21` | `:File Storage` là R2 |
| `docs/07-classdiagram/README.md:24` | Mô tả `FileStorage` |
| `docs/notes/ban-giao-ba-final-review/05-plan-…md:164, 168, 206` | Chính checklist rà soát này và ghi chú về SeaweedFS |
| `docs/notes/ban-giao-ba-final-review/07-scrum-107-…md:10, 15` | Ghi chú SCRUM-107 |
| `docs/notes/ban-giao-ba-final-review/note-may-nha/patch-scrum-107.md:4, 86` | Patch SCRUM-107 |

Ghi chú bổ sung (không phải lỗi dẫn chiếu): hai chỗ nói về migration điểm danh nhưng không dẫn ADR nào:

- `docs/02-design/03-co-so-du-lieu.md:3` — "Migration SCRUM-26: `AddAttendanceVersions` …" → nên thêm dẫn chiếu ADR-0025.
- `docs/02-design/02-erd.md:3` — "Hiện thực Backend SCRUM-26 (20/10/2026): thêm `AttendanceVersion`…" → nên thêm dẫn chiếu ADR-0025.

Các chỗ điểm danh đã dẫn đúng ADR-0025: `docs/03-api/10-diem-danh.md:4`, `docs/05-decisions/0025-…md`, `docs/notes/ban-giao-ba-final-review/04-scrum-26-diem-danh.md:10, 27`, `note-may-nha/patch-scrum-26.md:4`.

## 3. Đối chiếu mã thông báo: catalogue ↔ DOCX V.2

Nguồn: catalogue `docs/03-api/00-danh-muc-ma-thong-bao.md` (288 mã có trong bảng hoặc đoạn mô tả) và V.2 DOCX (233 mã duy nhất, 252 dòng). Hai bên không có mã nào **có trong V.2 mà thiếu trong catalogue**.

Mã · catalogue · V.2 · việc cần làm.

### 3.1 Có trong catalogue nhưng thiếu trong V.2

| Mã | Catalogue | V.2 | Việc cần làm |
|---|---|---|---|
| MSG79–MSG82 | BE, SCRUM-19 workflow (đã triển khai) | thiếu | Thêm vào V.2 |
| MSG85, MSG86, MSG88 | BE, SCRUM-19 workflow (đã triển khai) | thiếu | Thêm vào V.2 |
| MSG78, MSG87, MSG89 | Chỉ có trong đoạn mô tả MSG290 ("MSG78–MSG89 dùng cho SCRUM-19"), không có dòng bảng | thiếu | Catalogue thiếu dòng. Kiểm code BE rồi bổ sung dòng catalogue và thêm V.2 |
| MSG130–MSG136 | BE, SCRUM-19 (đã triển khai) | thiếu | Thêm vào V.2 |
| MSG137 | Chỉ có trong đoạn mô tả, không có dòng bảng | thiếu | Như MSG78: kiểm code, bổ sung dòng catalogue |
| MSG117, MSG118, MSG123, MSG124 | Đã triển khai SCRUM-39 (phòng họp) | thiếu | Thêm vào V.2 |
| MSG119, MSG120, MSG121, MSG122, MSG125 | Dành sẵn (UC-39a/b/c, UC-40, UC-42) | thiếu | Đúng: để ngoài V.2 đến khi có đặc tả UC. Không làm gì |
| MSG275, MSG276, MSG277, MSG278 | BE, SCRUM-20 / SCRUM-26 (đã triển khai) | thiếu | Thêm vào V.2 (MSG278 là SCRUM-26) |
| MSG280–MSG284 | BE, phòng họp SCRUM-39/41 (đã triển khai) | thiếu | Thêm vào V.2 |
| MSG285, MSG286 | Đã triển khai SCRUM-20 | thiếu | Thêm vào V.2 |
| MSG287 | Chỉ FE (sự kiện hub `capabilitiesChanged`, SCRUM-105), không có HTTP | thiếu | BA quyết định: thêm dạng "FE — không có HTTP", hoặc để ngoài V.2 vì không phải thông báo server |
| MSG279 | Chưa cấp | không có | Đúng. Không làm gì |
| MSG48–MSG59, MSG77, MSG90, MSG91, MSG127 | Ngừng dùng (ADR-0013 / ngừng khác) | không có | Đúng. Không làm gì |

### 3.2 Có trong V.2 nhưng catalogue ghi khác

| Mã | Catalogue | V.2 | Việc cần làm |
|---|---|---|---|
| MSG140 | Dành sẵn cho UC-18 EX-5 ("No eligible meeting approver…") | Dùng cho "Your session has expired…" (Common – Session Renewal, 2 dòng) | Xung đột. Mã đã dùng ở V.2 không được tái cấp cho nghĩa khác. Đề xuất: cấp mã mới (MSG290 theo "Mã tiếp theo") cho UC-18 EX-5, sửa catalogue và V.2 cùng lúc. Không đổi MSG140 vì mã có thể đã ở `main` |
| MSG141 | Đã triển khai SCRUM-25; catalogue ghi "Đặc tả đã bỏ 18/10/2026" | "Retired on 18/10/2026 … This code must not be reused" | Xung đột về trạng thái. Xác nhận với BE: nếu code vẫn trả mã này thì V.2 phải bỏ chữ "Retired" và ghi lại tình huống; nếu đã bỏ thì catalogue đổi thành Ngừng dùng |
| MSG144 | Đã triển khai SCRUM-25; catalogue ghi "Đặc tả đã bỏ 18/10/2026" | "Retired on 18/10/2026 …" | Như MSG141 |
| MSG152 | Ngừng dùng — PO 20/10/2026 | Còn active: "One or more selected meeting permissions are not allowed." (UC-28a EX-1, UC-28b EX-1) | V.2 sai. Đánh dấu retired trong V.2, hoặc sửa catalogue nếu PO đã đổi quyết định |
| MSG153 | Ngừng dùng — PO 20/10/2026 | Còn active: "Meeting permissions updated successfully." (UC-28b Main Flow) | Như MSG152 |

### 3.3 V.2 ghi "dành sẵn / reserved" nhưng catalogue ghi đã triển khai

**Không có mã nào.** Chỉ có hai mã V.2 ghi "reserved": MSG145 và MSG146. Catalogue cũng ghi cả hai là dành sẵn, nên khớp nhau. Có thể giữ nguyên.

### 3.4 Mã đã retired ở cả hai bên (khớp, không cần sửa)

MSG113, MSG139, MSG155, MSG163, MSG168, MSG173, MSG182, MSG201, MSG203, MSG237, MSG250 — catalogue và V.2 cùng ghi ngừng dùng.

### 3.5 Ghi chú khác (không thuộc ba yêu cầu)

- V.2 lặp lại một số mã có nhiều dòng (MSG02 ×3, MSG06 ×2, MSG08 ×2, MSG09 ×4, …). Có thể gộp khi sửa, không bắt buộc.
- MSG154 có trong V.2 và catalogue ghi trạng thái "200" ở bảng Participants: khớp.
- MSG145 và MSG146 vẫn là dành sẵn: giữ đúng.

## Việc không làm trong phiên này

- Không sửa file có sẵn, không commit (theo yêu cầu).
- Không kiểm điều khoản cụ thể của Luật 91/2025/QH15 hay NĐ 356/2025/NĐ-CP. Số điều và tên gọi trong các đề xuất nêu ở mức căn cứ chung; BA đối chiếu văn bản trước khi chốt.
- Không kiểm code BE để xác nhận MSG141, MSG144, MSG78, MSG87, MSG89, MSG137. Cần BE xác nhận.

## Tổng kết số mục

- Mục 1: 3 DOCX (D1–D3), 3 `.md` (M1–M3), 16 mục tài liệu/ADR (T1–T16), 5 nhóm wireframe (W1–W5).
- Mục 2: 0 dòng sai; 9 nhóm dòng ADR-0024 đã kiểm; 2 chỗ điểm danh thiếu dẫn ADR-0025.
- Mục 3: 3.1 có 15 dòng nhóm; 3.2 có 5 mã; 3.3 không có mã nào; 3.4 có 11 mã.

File này: `docs/notes/ban-giao-ba-final-review/09-ra-soat.md`.
