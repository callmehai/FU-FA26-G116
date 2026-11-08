# PR #260 (SCRUM-36 + SCRUM-32) — dự thảo AI và biên bản; SCRUM-137 Tìm giờ họp

**Người ghi:** KhánhPG/BA, 05/11/2026. **Nguồn:** PR #260 (nhánh `feat/scrum-36-32-ai-summary-minutes`, commit
`2684c48f`), comment Jira của SCRUM-36 và SCRUM-32, tin nhắn Zalo của Khánh, `docs/03-api/17-du-thao-bien-ban.md`,
ADR-0054 (trên nhánh #260) và danh mục mã thông báo `MSG384`–`MSG405`.

PR #260 làm hai bước như AI-eCabinet (commit `bba2097f`): **dự thảo AI** (SCRUM-36) rồi **bản nháp biên bản**
(SCRUM-32). Có mấy chỗ làm khác ticket, đã ghi trong comment Jira của hai ticket; lúc BA Final Review Khánh nhờ
cập nhật tài liệu theo code. Note này gom việc BA, không thay Jira hay đặc tả.

> **Ràng buộc nhánh (đã gỡ 05/11/2026).** PR #260 và PR #259/#261 đều đã vào `main`.
> `docs/03-api/17-du-thao-bien-ban.md`, ADR-0054 và hai thực thể `MeetingSummary`/`MeetingMinutes` đã có trên
> main; Report 3 DOCX sửa trực tiếp trên nhánh từ main rồi sync như thường lệ.

## 0. Tóm tắt

| Việc | Tài liệu phải sửa | Trạng thái |
|---|---|---|
| Dự thảo AI 3 mục I–III, chỉ Chủ tọa/Thư ký xem–sửa–duyệt | UC-47–UC-50, BR, màn §III 10.2, sequence FE-10, inventory | **đã sửa 05/11/2026** |
| Một mẫu biên bản cố định (Mẫu 1.4 NĐ30), chỉ xuất Word | UC-58–UC-60, BR, màn §III 12.1, sequence FE-12, inventory | **đã sửa 05/11/2026** |
| Mã thông báo `MSG384`–`MSG405` | `docs/03-api/00-danh-muc-ma-thong-bao.md` | đã có trên nhánh #260 |
| Hợp đồng API dự thảo và biên bản | `docs/03-api/17-du-thao-bien-ban.md` | đã có trên nhánh #260 |
| SCRUM-137 Tìm giờ họp | chưa có code, chưa có UC | chỉ ghi nhận, chờ BE |

## 1. UC-47–UC-50 — dự thảo AI (SCRUM-36)

### 1.1 Code làm gì

- Dự thảo có **đúng 3 mục** như eCab, đặt dưới tiêu đề của mẫu biên bản:
  - **I. Mục tiêu và phạm vi cuộc họp**
  - **II. Nội dung thảo luận và đánh giá** — mỗi mục nghị sự một phần `n. <tên mục>`
  - **III. Kết luận và phân công thực hiện** — `1. Kết luận của chủ trì`, `Kết quả biểu quyết:` (nếu có),
    `2. Nhiệm vụ, sản phẩm và thời hạn`
- **Chỉ Chủ tọa hoặc Thư ký hiện tại** được yêu cầu, sửa và duyệt dự thảo (`MSG385`). Người khác đọc được cuộc họp
  chỉ thấy dự thảo **sau khi đã duyệt**; trước đó `summary: null`, không lộ chữ nào của AI.
- **Sửa xong phải duyệt lại:** lưu phần sửa (`PUT`) xoá dấu đã duyệt, nên người khác **mất quyền xem** cho tới khi
  Chủ tọa/Thư ký bấm duyệt lại (`MSG388`, `MSG391`).
- **Kết quả biểu quyết chép từ dữ liệu, AI không viết:** mỗi phiên biểu quyết đã đóng thành một dòng (số phiếu từng
  phương án, kết luận Thông qua / Không thông qua / Hoãn, ghi chú), không bao giờ nêu ai bỏ phiếu nào.
- **Chỉ tạo được khi cuộc họp đang diễn ra hoặc đã kết thúc** (`MSG386`); chưa bắt đầu hoặc đã huỷ thì không.
- **Viết mất vài phút:** yêu cầu chỉ xếp hàng (`202 MSG384`), trang hỏi lại mỗi 5 giây khi trạng thái là `Queued`/
  `Running`. Dự thảo cũ **vẫn đọc được** cho tới khi bản mới xong; bản mới thay cả bản cũ và xoá dấu sửa/duyệt.
- **Bản nháp luôn gắn nhãn "Nội dung do AI tạo — cần rà soát"** trên màn hình.
- **Máy AI chạy tại chỗ**, gọi qua `POST {Ai:BaseUrl}/chat/completions` (Ollama, llama.cpp, LM Studio, vLLM). Server
  chưa cấu hình `Ai:BaseUrl`/`Ai:Model` thì trả `503 MSG394`; máy AI tắt thì dự thảo `Failed` kèm lý do, phần còn lại
  của hệ thống vẫn chạy.
- **Cắt lời thoại theo chương trình:** mỗi mục nghị sự một lần gọi, lấy đoạn giữa lúc Chủ tọa bấm bắt đầu và kết
  thúc mục; mục dài thì chia nhỏ rồi gộp. Không có chương trình, hoặc mục chưa bấm giờ, thì tóm tắt cả cuộc họp.
  Mục I và III viết sau, từ các phần đã tóm tắt và kết luận Chủ tọa đã ghi.
- **Luật prompt giữ theo eCab:** số đọc thành chữ phải đổi thành chữ số (`SpokenNumbers`, "chín mươi phần trăm" →
  "90%"), không đoán tên cho "Người nói N", người trình bày lấy từ chương trình, không Markdown, nhiệt độ 0, không
  suy ra thành phần tham dự.
- **`sources`:** mỗi phần của mục II lưu khoảng giờ và số dòng lời thoại đã dùng, để Thư ký đối chiếu tab Bản ghi.
- **`readiness`:** trước khi yêu cầu, Chủ tọa/Thư ký thấy số câu đã ghi, số giọng chưa đặt tên, số bản gỡ băng đang
  chạy/hỏng, số mục nghị sự và số mục đã bấm giờ, số phiên biểu quyết đã đóng — để biết dự thảo sẽ thiếu gì.
- **Giới hạn nội dung:** I ≤ 2.000, II ≤ 12.000, III ≤ 8.000 ký tự; mục trống hoặc quá dài là `400 MSG389`.

### 1.2 Mã thông báo (đã có trên nhánh #260)

| Mã | Tình huống |
|---|---|
| `MSG384` | Đã xếp hàng yêu cầu dự thảo |
| `MSG385` | Không phải Chủ tọa/Thư ký mà tạo, sửa hoặc duyệt |
| `MSG386` | Cuộc họp chưa bắt đầu hoặc đã huỷ |
| `MSG387` | AI đang viết dự thảo |
| `MSG388` | Đã lưu phần sửa dự thảo |
| `MSG389` | Mục trống hoặc quá dài |
| `MSG390` | Chưa có dự thảo nào viết xong để sửa/duyệt |
| `MSG391` | Đã duyệt dự thảo |
| `MSG392` | Không đọc được dự thảo |
| `MSG393` | Không lưu được dự thảo |
| `MSG394` | Server chưa cấu hình máy AI |

### 1.3 Việc BA đã làm trong đợt này

- Viết đặc tả **UC-47, UC-48, UC-49, UC-50** trong Report 3 §II (thay dòng "Placeholder only"); **UC-51 Publish**
  giữ nguyên "chưa làm" vì code chưa có bước công bố.
- Thêm BR-373 → BR-382 (dự thảo AI) vào §V.1.
- Sửa màn **§III 10.2 Meeting Summary**: bỏ "Publish", bỏ "status history", thêm nhãn AI, 3 mục I–III, nguồn từng
  phần, nút Tạo lại / Duyệt, chờ AI.
- Sửa **inventory**: UC-47–UC-50 actor là Chủ tọa/Thư ký + *máy AI nội bộ* (không còn "Google AI Provider").
- Thêm sequence **UC47, UC48, UC49, UC50** vào `docs/06-sequencediagram/transcription-voice-id/`.

## 2. UC-58–UC-60 — bản nháp biên bản (SCRUM-32)

### 2.1 Code làm gì

- **Chỉ một mẫu cố định**: Biên bản cuộc họp theo **Mẫu 1.4 Nghị định 30/2020/NĐ-CP** (đúng bố cục mẫu của eCab).
  **Không có màn quản lý mẫu**, không có mẫu thứ hai.
- **Mới xuất Word (`.docx`), chưa có PDF.** File Word tự dựng, không thêm thư viện; tên file `Biên bản - <tên cuộc
  họp>.docx`.
- **Soạn bằng văn bản thuần, có xem trước**: nội dung là text có vài dấu đầu dòng (`##` tiêu đề mục, `###` tiêu đề
  nhỏ, `@@ trái | phải` dòng hai cột, `**đậm**`, `-` gạch đầu dòng, `^` căn giữa, `~` dòng nhỏ). Trang web
  (`MinutesPaper`) và file Word đọc **cùng các dấu đó**, nên không có HTML và không có gì chạy được trên trình duyệt.
- **Điền từ hồ sơ cuộc họp:** tên cuộc họp, giờ bắt đầu/kết thúc, địa điểm (theo hình thức họp), thành phần tham dự
  (ai không đánh dấu vắng thì có mặt), Chủ tọa, Thư ký, chương trình và kết luận từng mục, kết quả biểu quyết và kết
  luận của Chủ tọa. Mục I–III lấy **dự thảo đã duyệt**; chưa duyệt thì mục II liệt kê chương trình, mục III ghi kết
  luận Chủ tọa đã ghi và kết quả biểu quyết, còn lại để "……" cho Thư ký viết.
- **Chỉ Chủ tọa/Thư ký** xem và sửa **bản nháp**; người khác chỉ thấy biên bản **sau khi ban hành** (SCRUM-33, chưa
  làm — hiện `minutes: null`). Người không phải hai vai đó hỏi bản nháp nhận **`404` rỗng** để không lộ bản nháp.
- **Chỉ soạn khi cuộc họp đang diễn ra hoặc đã kết thúc** (`MSG398`).
- **Mỗi cuộc họp một bản nháp:** đã có mà không gửi `rebuild: true` thì `409 MSG399`; tạo lại **thay toàn bộ** nội
  dung đã sửa (trang hỏi trước). Biên bản đã gửi duyệt/ban hành thì không sửa, không tạo lại (`MSG400`).
- **Chống ghi đè:** lần lưu gửi kèm thời điểm đã đọc; có người lưu sau thì `409 MSG401`, không ghi đè.
- **Cuộc họp Mật không cho tải về** (`403 MSG405`): biên bản chỉ xem trên màn hình.
- **Giới hạn:** nội dung trống hoặc quá 100.000 ký tự là `400 MSG402`.
- **Nhiệm vụ trong mục III chỉ là chữ:** TrustCabinet không theo dõi người được giao, hạn hay tiến độ (phạm vi
  README không đổi).

### 2.2 Mã thông báo (đã có trên nhánh #260)

| Mã | Tình huống |
|---|---|
| `MSG395` | Đã tạo (hoặc tạo lại) bản nháp biên bản |
| `MSG396` | Đã lưu biên bản |
| `MSG397` | Không phải Chủ tọa/Thư ký mà bấm tạo, sửa, xuất biên bản hoặc xem biên bản đã ban hành (xem/sửa bản nháp: 404 rỗng) |
| `MSG398` | Cuộc họp chưa bắt đầu hoặc đã huỷ |
| `MSG399` | Đã có bản nháp mà không xin tạo lại |
| `MSG400` | Biên bản đã gửi duyệt hoặc đã ban hành |
| `MSG401` | Có người vừa lưu, bản đang sửa đã cũ |
| `MSG402` | Nội dung trống hoặc quá 100.000 ký tự |
| `MSG403` | Không đọc được biên bản |
| `MSG404` | Không lưu được biên bản |
| `MSG405` | Cuộc họp Mật: biên bản không được tải về |

### 2.3 Việc BA đã làm trong đợt này

- Viết đặc tả **UC-58, UC-59, UC-60** trong Report 3 §II; **UC-61–UC-65** (ký số) giữ "chưa làm".
- Thêm BR-383 → BR-394 (biên bản) vào §V.1.
- Sửa màn **§III 12.1 Minutes Editor**: bỏ trình soạn thảo định dạng + tự lưu + "Submit for signing", thay bằng
  soạn văn bản thuần có xem trước, nút Lưu, Tạo lại, Tải Word (không có ở cuộc họp Mật), một mẫu cố định.
- Thêm sequence **UC58, UC59, UC60** vào `docs/06-sequencediagram/minutes-management/`.

## 3. Sequence diagram + BR

- Hợp đồng API và quy ước đầy đủ: `docs/03-api/17-du-thao-bien-ban.md` (trên nhánh #260); quyết định:
  ADR-0054 (trạng thái **Đề xuất**, PO Hải 04/11/2026).
- Sequence mới (nguồn `.txt`, xuất PNG bằng `scripts/txt_to_sequence_drawio_png.py`):
  - FE-10: `UC47_GenerateMeetingSummary`, `UC48_ViewMeetingSummary`, `UC49_EditMeetingSummary`,
    `UC50_ApproveMeetingSummary`.
  - FE-12: `UC58_ViewMeetingMinutes`, `UC59_GenerateMeetingMinutes`, `UC60_EditMeetingMinutes`.
- BR mới: BR-373–BR-382 (dự thảo), BR-383–BR-394 (biên bản) — bảng đầy đủ ở
  [06-business-rules.md](../../01-requirements/06-business-rules.md) sau khi sync.
- **UC-51 Publish** và **UC-61–UC-65** (gửi ký, ký, trạng thái ký, hoàn tất, yêu cầu sửa) **không đặc tả** đợt này:
  code chưa có, đừng viết đặc tả trước khi SCRUM-33 làm.

## 4. SCRUM-137 Tìm giờ họp

- Khánh nhờ tạo ticket "tìm họp" trên Zalo; đã tạo **SCRUM-137 Tìm giờ họp** (theo màn "Tìm giờ họp" của eCab,
  US #352) và giao cho Khánh.
- **Phần BE:** tìm các khung giờ mà **mọi người được chọn cùng rảnh**; chỉ trả **bận/rảnh**, **không lộ nội dung
  cuộc họp của người khác** (không trả tên cuộc họp, người tham dự, địa điểm của lịch người khác).
- **Trạng thái tài liệu:** chưa có code và **chưa có UC** trong Report 3. Khi BE làm xong mới viết UC mới (số tiếp
  theo của dải UC, không tái dùng số cũ), BR và sequence; **không** đặc tả trước rồi để code chạy theo.
- Việc BA ghi ở đây chỉ để không quên: hỏi PO màn hình thuộc FE nào (lịch hay cuộc họp), có cần quyền gì, và
  khung giờ trả về theo bước bao nhiêu phút.

## 5. Việc BA còn lại

- [x] Note 33 chi tiết hoá từ tin nhắn Zalo + PR #260 + comment Jira.
- [x] UC-47–UC-50 và UC-58–UC-60 trong Report 3 DOCX §II (thay placeholder).
- [x] BR-373–BR-394 trong Report 3 DOCX §V.1.
- [x] Màn §III 10.2 (Meeting Summary) và 12.1 (Minutes Editor) theo code; bảng field của hai màn và bảng màn hình §I.
- [x] Inventory: actor UC-47–UC-50 (AI Drafting Service thay "Google AI Provider"), trạng thái UC-58–UC-60.
- [x] Sequence `.txt` UC-47–UC-50 (FE-10) và UC-58–UC-60 (FE-12), xuất PNG bằng `node scripts/export-diagrams.js`.
- [x] Phụ lục MSG trong Report 3 §V.2 thêm `MSG384`–`MSG405` (English) để `check_docx.py` qua.
- [x] Gộp 7 tab mới vào `TrustCabinet_Sequence_Diagrams.drawio` và xuất ảnh `drawio-png/` (05/11/2026, đợt follow-up review #261; xem
  [note 32 §4](32-scrum-20-25-26-69-97-98-fe06-fe05-ba-final-review.md) về lỗi tab lệch kiểu trên Windows).
- [x] Chạy lại `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`
  sau khi #259 merge (DOCX là nguồn; `05-…`, `06-…` và `Report-3_….md` là bản sinh ra). Lượt 05/11/2026 đã chạy trên
  nhánh main, `sync_requirement_extracts.py --check` báo khớp.
- [x] `MSG384`–`MSG405` đã có trong danh mục trên main sau khi #260 vào (04/11/2026); `sync_reports.py` không còn cảnh báo.
- [x] Comment kết quả vào **SCRUM-36** 06/11/2026 (`10448`: UC-47–UC-50, BR-373–BR-382, MSG384–394, PR #260/`a425155` + #261/#262/#264; UC-51 chưa làm) và chuyển `To Do` → `In Review` cho Tester
  ([DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update)); nêu rõ
  UC-51 và UC-61–UC-65 chưa đặc tả vì chưa có code.
- [ ] Comment kết quả vào **SCRUM-32**: #263 và #265 đã vào `main` ngày 06/11/2026 (README và note đã cập nhật khi merge); docs note 34 đã làm, comment Jira chờ PR #275 vào `main`.
- [x] SCRUM-137: comment `10449` 06/11/2026 xác nhận phạm vi "chỉ bận/rảnh"; giữ `To Do`, chưa viết UC.
