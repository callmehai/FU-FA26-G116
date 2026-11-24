# Note gửi BA — đồng bộ style UC Specification trong Report 3 (24/11/2026)

Quy tắc hiện có: [docs/08-reports/README.md § Style UC Specification](../../08-reports/README.md#style-uc-specification) — lấy **UC-23a (FE-05)** làm mẫu, nhãn và tiêu đề AF/EX in đậm, nội dung flow chữ thường, chỉ dùng italic khi mẫu có, Times New Roman, AF không có thì `N/A`, EX phải nói cách kết thúc hoặc tiếp tục.

Đã quét cả 133 bảng UC trong `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx` (python-docx, đọc định dạng từng run). Số UC dưới đây là số mới.

## 1. Mẫu là UC-23a lúc viết quy tắc, không phải UC-23a hiện tại

Quy tắc viết ngày 15/10/2026 (commit `1e3db559`). Đánh số lại UC đợt 1 giữ nguyên mã UC-23a (Create Agenda Item, FE-05), nhưng **định dạng của UC-23a đã bị đổi sau ngày đó**, nên không lấy bảng UC-23a trong DOCX hiện tại làm mẫu. Mẫu chuẩn là bảng UC-23a trong `git show 1e3db559:docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`:

| Điểm | UC-23a gốc (15/10) — mẫu | UC-23a hiện tại |
|---|---|---|
| Nhãn cột trái | đậm | đậm |
| Tiêu đề `EX-n – Tên` | đậm | đậm |
| **Các bước trong EX** | **chữ thường** | **in đậm** — lệch, sửa lại |
| Italic | không | không |
| Font | Times New Roman 12 pt (gắn trên từng run) | 12 pt, font kế thừa Times New Roman từ mặc định của file — hiển thị như nhau |
| Mỗi ô | một đoạn, ngắt dòng (riêng Postconditions hai đoạn) | một đoạn, ngắt dòng |
| Tên AF/EX | Viết Hoa Từng Từ ("Invalid Agenda Item Information") | viết như câu ("Invalid agenda item information") |
| Bước kết thúc EX | không có | không có |

Kết luận:

- Câu quy tắc trong README đúng với mẫu gốc: nhãn và tiêu đề AF/EX đậm, **nội dung flow chữ thường**. 88 UC đang in đậm các bước EX (gồm UC-23a hiện tại) là lệch, sửa đồng loạt.
- Tên AF/EX: mẫu gốc viết hoa từng từ, nhưng hiện 396 tiêu đề viết như câu, 31 viết hoa từng từ. Đề nghị chốt **viết như câu**, theo đa số và UC-23a hiện tại, và ghi rõ trong README. Tiêu đề viết hoa từng từ sửa theo.
- Bước kết thúc EX: mẫu gốc cũng không có, nhưng quy tắc README đòi EX nói cách kết thúc hoặc tiếp tục. Chốt theo quy tắc: thêm câu kết vào mọi EX, kể cả UC-23a.

Phần còn lại dùng làm mẫu như cũ: 8 nhãn theo thứ tự, PRE/POST mở đầu bằng `● PRE-n:` / `● POST-n:`, Normal Flow mở đầu bằng một dòng tên luồng (chữ thường) rồi các bước đánh số, mỗi ô là **một đoạn** có ngắt dòng, không italic, không có dòng "Sequence Diagram" trước bảng trong DOCX.

## 2. Hiện trạng lệch mẫu

| Hạng mục | Theo mẫu | Lệch | UC lệch |
|---|---|---|---|
| Nhãn cột trái in đậm | đậm | chữ thường | UC-28 (cả 7 nhãn); "Secondary Actors" ở UC-30a, UC-57a–d; "Primary Actors" ở UC-30b, UC-30c |
| Dấu sau mã AF/EX | `EX-1 – Tên` | `EX-1: Tên` | UC-01 → UC-11c (14 UC) |
| Tiêu đề AF in đậm | đậm | thường | UC-04, 15, 16, 28, 30a, 30b, 35a, 35b, 44, 55b, 57d, 58–61, 69–73, 80f, 81c |
| Tiêu đề EX in đậm | đậm | thường | UC-21, 24, 25, 28, 30a–c, 32, 34, 36a–d, 37a, 38, 40b, 41a, 41b, 52c, 54c, 57a–d, 79a, 79b |
| Bước AF chữ thường | thường | đậm | UC-12, 14, 18, 22, 36a, 42, 52d, 54c, 63, 68a, 79a, 80f |
| Bước EX chữ thường | thường | đậm | 88 UC (đa số, gồm cả UC-23a hiện tại) — sửa đồng loạt |
| Tên AF/EX viết như câu | viết như câu (chốt ở mục 1) | Viết Hoa Từng Từ | 31 tiêu đề — soát khi sửa |
| Không italic | không | có italic (chủ yếu dòng "The use case ends.") | AF: 19 UC (UC-05, 07, 08, 11a–c, 13, 14, 16–18, 77b, 78, 80c–f, 81a, 81c); EX: 42 UC (UC-01–04, 06a–c, 07, 08, 10, 11a–c, 13–20, 58–61, 69–76, 80a–f, 81a, …) |
| Dòng tên luồng ở Normal Flow chữ thường | thường | đậm | UC-37 |
| Mỗi ô một đoạn, ngắt dòng | một đoạn | nhiều đoạn | Normal Flow 57 UC, Pre 57, Post 76, AF 43, EX 4 — khoảng cách dòng khác nhau giữa các UC |
| `● ` trước PRE/POST | có | thiếu | UC-04 POST-1–3, UC-43 POST-4 |
| Bước kết thúc EX | có | thiếu | 31/270 khối EX (gồm UC-23a) |
| Tên luồng chính khi trích | thống nhất | "of the Main Flow" (65 khối) trong khi nhãn là "Normal Flow" | chốt một tên: **"Normal Flow"** cho khớp nhãn |
| Câu kết EX | thống nhất | "The use case ends." (154), "ends unsuccessfully." (21), các biến thể khác | chốt hai dạng: "The use case ends." và "The flow resumes at Step N of the Normal Flow." (hoặc "of AF-n") |
| Trích MSG | `MSGnn – "Nội dung."` lần đầu trong luồng, các lần sau `MSGnn` | ngoặc sau mã không có nội dung, hoặc `MSGnn` trơn ở lần đầu | rải rác (UC-04, 23b–d, 24, 34, 36a/b, 43, 44, 45b, 45d, 52c, 63, 68a, 75, 79a, 80c, 80f, 81b, 81c): soát khi sửa từng UC |

Không lệch: cả 133 bảng có đủ 8 nhãn theo đúng thứ tự; cỡ chữ đều 12 pt, font kế thừa (Times New Roman); mọi Normal Flow mở đầu bằng dòng tên luồng; UC cha có spec dùng Heading 3, UC con dùng Heading 4.

## 3. Cách sửa

1. Đưa UC-23a về đúng mẫu gốc (bước EX chữ thường, thêm câu kết EX) trước, rồi mới áp cho cả file.
2. Phần định dạng (đậm, nghiêng, dấu `–`, `●`, gộp nhiều đoạn thành một đoạn ngắt dòng) làm bằng `scripts/uc_spec_style_report3.py`, một commit riêng, **không đổi chữ**. Chạy không tham số để xem báo cáo; `--apply <out.docx>` để sửa. Đã thử trên bản sao: sau khi áp, chỉ còn ba nhóm phải sửa tay (EX thiếu câu kết, "Main Flow", tên viết hoa từng từ); chữ trong 11.255 ô bảng không đổi ngoài `:` → `–` và `●`; `check_docx.py` OK. Kiểm tra: `check_docx.py`, mở Word, rồi `sync_reports.py --report 3`; diff Markdown snapshot chỉ được khác ở dấu `:` → `–` và `●`.
3. Phần chữ (thêm bước kết thúc EX, "Main Flow" → "Normal Flow", câu kết, trích MSG) làm tay theo thứ tự UC, cùng lượt với note 60.
4. Thêm một dòng Change Log của Report 3.

Quy tắc đã bổ sung vào `docs/08-reports/README.md` § Style UC Specification trong cùng commit với note này.

## 4. Góp ý của thầy HungNQ trên Drive (24/11/2026) — sửa cùng đợt

Thầy để 7 comment trên bản Report 3 trên Drive ([link](https://docs.google.com/document/d/1JNNCwt0Yc_KLS9dPB6wtP8HW6x7D1fWl/edit)). Sửa trong DOCX nguồn `docs/08-reports/source/`, không sửa trên Drive; xong thì sync rồi mới push lên Drive.

| # | Mục | Thầy nói | Cần sửa |
|---|---|---|---|
| 1 | I.1 Context Diagram (hình + các đoạn chữ I.1) | Vẽ lại, nền trắng, hiện không nhìn thấy; chữ phải đọc được | Vẽ lại nền trắng, chữ đủ lớn; vẽ đúng actor và dịch vụ ngoài theo đoạn chữ và code hiện tại (đối chiếu note 60 §2, API-06: máy AI local hoặc API do Admin chọn): actor ngoài là Member (Chair/Secretary là vai trò trong cuộc họp, ADR-0075), bỏ "AI / Speech Processing Provider" ngoài hệ thống nhận "Meeting audio" (trái BR-121), bỏ phần ký biên bản ("signature status", "signing data" — đã bỏ theo SCRUM-37), bổ sung kho tệp (SeaweedFS; R2 đã bỏ hẳn, cả server demo — PR #529). Sửa mọi chỗ còn ghi Cloudflare R2 thành SeaweedFS, dẫn theo mấy chữ đầu (số đoạn trong DOCX đổi mỗi lần sync): đoạn "TrustCabinet is an Internal Meeting…" (còn liệt kê "Google AI Provider … and Cloudflare R2"), đoạn "Cloudflare R2 stores the files…" (viết lại: SeaweedFS giữ tệp…), mục "17.7 Cloudflare R2 – file storage (API-07)", hàng "Cloudflare R2" trong bảng Actors ("An external object-storage service…", đồng thời sửa mã UC cũ "UC-35 to UC-39" trong cùng ô), hàng "Cloudflare R2 – S3-compatible object storage" trong bảng External Interfaces, ô "Cloudflare R2 over an S3-compatible API…" trong khối URL. Các chỗ "Google AI Provider" (đoạn mở đầu, mục "17.6 Google AI Provider – draft meeting summary (API-06) – planned", bảng External Interfaces, ô "An external generative-AI service…") sửa thành "AI drafting service" theo code (máy AI của nhóm hoặc API do Admin chọn, ADR-0054/0056) như Context Diagram trên app sau PR #525; bỏ chữ "planned"/"not connected yet" đã sai |
| 2 | 2.4 Post-Meeting Processing & Formal Records (BF-04) (hình) | Hình gì phải nói rõ; chữ không đọc được; **rà toàn bộ hình**, chữ trong hình phải đọc được, cần thì để link ảnh gốc bên dưới cho hội đồng mở | Thêm caption, xuất lại ảnh nét, thêm link ảnh gốc |
| 3 | 3.1 Entity Relationship Diagram (hình) | Ghi chú đây là sơ đồ gì; phải nhìn rõ tên trường; không được thì để link ảnh gốc cho hội đồng xem và chấm | Caption, ảnh độ phân giải cao hoặc tách ERD theo module, link ảnh gốc |
| 4 | 3.1, đoạn "The diagram is drawn from the database model in the code as of 04/11/2026 … 41 tables …" | Đoạn này cho thấy code xong rồi chụp DB làm tài liệu ngược; phải đúng quy trình: **thiết kế trước, lập trình sau**; rà các chỗ mâu thuẫn kiểu này | Viết lại thành mô tả thiết kế, bỏ "drawn from the code", ngày chụp; quét cả file các câu "in the code", "as built", "as of <ngày>" |
| 5 | 3.1, cùng đoạn — MinutesSignature | MinutesSignature còn trong hệ thống không? Cần làm rõ | Đã bỏ (PO bỏ ký biên bản 14/11, SCRUM-37): xoá khỏi ERD và câu đếm bảng, đối chiếu số bảng với 3.2 |
| 6 | 4.3.1 UCs for System Admin (hình) | Ghi rõ hình này là gì: "Figure …" | Thêm caption |
| 7 | 4.3 (đầu mục) | Nên bổ sung sơ đồ use case tổng thể, ví dụ "Figure 1: System Overview Use Case Diagram" | Vẽ UC diagram tổng thể, đặt trước các sơ đồ UC theo actor |

Áp cho cả report, không chỉ chỗ có comment:

- Mọi hình có caption `Figure n: <tên>` đánh số liên tục; chữ trong hình đọc được ở zoom 100%; hình dày (ERD, sequence diagram lớn) thêm link ảnh gốc ngay dưới caption.
- Không câu nào kể tài liệu được lấy ra từ code hay ghi ngày chụp từ code; nội dung viết như thiết kế.
- Thêm dòng Change Log cho các sửa đổi này cùng dòng ở mục 3 bước 4.
