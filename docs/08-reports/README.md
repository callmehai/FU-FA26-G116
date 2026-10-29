# Đồng bộ báo cáo

> **CẤM sửa tay các `Report-*.md` trong thư mục này** (và các bản trích
> `docs/01-requirements/05-use-case-specifications.md`, `06-business-rules.md`). Đó là snapshot
> sinh từ script — lần sync sau sẽ ghi đè. **Nguồn duy nhất để sửa nội dung report** là DOCX trong
> [`source/`](source/README.md). Quy trình đúng: sửa DOCX →
> `python scripts/sync_reports.py --source docs/08-reports/source/<tên>.docx --report <số>` →
> rà `git diff`. Chỉ đẩy Drive khi được yêu cầu (`push_reports.py`). Không tải Drive về để làm
> nguồn.

File này là chỉ dẫn duy nhất cho `docs/08-reports/`. Áp dụng cho mọi DOCX trong `source/` và
Markdown snapshot của Report 1–7.

## Phạm vi trước khi sửa

- Trước khi ghi, nêu rõ đường dẫn tuyệt đối của DOCX đích, report/UC/table hoặc trang cần sửa, và
  các đầu ra liên quan (Markdown, Drive). Nếu người dùng đã chỉ rõ chính xác, nhắc lại phạm vi rồi
  tiếp tục. Nếu có nhiều bản có thể là nguồn chuẩn hoặc vị trí còn mơ hồ, hỏi và chờ xác nhận.
- Kiểm tra `git status` và diff hiện tại trước khi sửa. Giữ nguyên thay đổi có sẵn của người dùng.
  Không khôi phục, thay thế hoặc đồng bộ từ Drive khi chưa được yêu cầu.
- Chỉ sửa vùng đã xác nhận. Không tiện tay chuẩn hóa nội dung, style, bảng, ảnh hoặc các UC khác.

## Nguồn và snapshot

- DOCX trong `source/` là bản local chuẩn để đồng bộ Report 1–7 ra Markdown.
- Không sửa thẳng Markdown snapshot của Report 1–7. Sửa DOCX local, rồi sinh Markdown bằng
  `scripts/sync_reports.py`. Không tải DOCX từ Drive để tạo hoặc cập nhật Markdown. Script hiện xuất
  Report 1–3; Report 4–7 dùng cùng quy tắc, không viết snapshot bằng tay khi script chưa xuất report
  đó.
- Khi được yêu cầu đưa report lên Drive, dùng DOCX local tương ứng để **thay nội dung file Drive
  hiện có tại chỗ**. Không tạo bản sao, không xóa/tạo lại file và không đổi tên hoặc di chuyển file.
  Giữ nguyên Drive file ID và link.
- Chỉ thay file đúng theo bảng link chuẩn bên dưới. Trước khi ghi, xác nhận tên file và ID từ Drive
  khớp với bảng. Sau khi ghi, kiểm tra cùng ID/link vẫn mở được và kích thước/thời gian cập nhật
  phản ánh bản mới.
- Việc thay DOCX trên Drive không cập nhật Markdown. Việc tạo Markdown không đọc hay ghi Drive.
- Chiều dữ liệu là **một chiều: repo → Drive**. `docs/08-reports/source/*.docx` là nguồn duy nhất;
  Markdown là snapshot sinh từ đó; Drive là bản phát hành, chỉ được ghi từ đó. Không có bước nào đọc
  ngược từ Drive về `source/` — `sync_reports.py --download` sẽ từ chối chạy nếu thiếu
  `--allow-drive-import`, vì nó ghi đè bản local.

## Quy trình một lần sửa

1. Sửa DOCX trong `source/`. Không sửa tay Markdown snapshot.
2. Sinh lại snapshot:
   `python scripts/sync_reports.py --source docs/08-reports/source/<tên>.docx --report <số>`
3. Rà `git diff -- docs/08-reports`, chắc diff chỉ nằm trong vùng vừa sửa.
4. **Khi được yêu cầu phát hành** (không tự động, không bắt buộc mỗi lần sửa):
   - `python scripts/push_reports.py <số> --check` — xem Drive đang lệch bao nhiêu;
   - `python scripts/push_reports.py <số>` — dry run, in ra file đích;
   - `python scripts/push_reports.py <số> --confirm` — ghi đè nội dung, giữ nguyên ID/link;
   - `python scripts/push_reports.py <số> --check` — phải ra `UP TO DATE`.
   `<số>` nhận `1`–`4`, `5.0`, `5.2`; `minutes` cho cả thư mục biên bản họp; `all` cho mọi đích.
   Biên bản họp là file mới mỗi buổi nên `minutes` tạo file còn thiếu thay vì thay nội dung file cũ.
5. Push cần credential Drive trong môi trường, scope `https://www.googleapis.com/auth/drive`:
   - **Refresh token — làm một lần, khuyến nghị.** Tạo OAuth client loại *Desktop app* trong Google
     Cloud project có bật Drive API, đặt `GOOGLE_DRIVE_CLIENT_ID` và `GOOGLE_DRIVE_CLIENT_SECRET`,
     rồi chạy `python scripts/google_drive_token.py`. Script mở trình duyệt, in refresh token kèm
     email tài khoản Drive sẽ dùng. Đặt token đó vào `GOOGLE_DRIVE_REFRESH_TOKEN`; mỗi lần push,
     script tự đổi sang access token mới (xem `scripts/drive_auth.py`).
   - **Access token — một lần chạy.** Dán token sống khoảng một giờ vào `GOOGLE_DRIVE_ACCESS_TOKEN`.
   - **Kiểm tra:** `python scripts/google_drive_token.py --check` phải in ra đúng email tài khoản có
     quyền ghi thư mục `Docs`. Lỗi `401` là token sai/hết hạn, `403` là thiếu scope hoặc tài khoản
     không có quyền ghi.
   - Không commit token, không đặt vào `appsettings.json`. Không dùng `drive.file` vì các file đích có
     từ trước.
   - OAuth consent screen để **Testing** thì refresh token hết hạn sau 7 ngày; muốn giữ lâu thì publish
     app (app chưa verify sẽ hiện cảnh báo và giới hạn 100 user — đủ cho nhu cầu nội bộ).


## Style UC Specification

- Với UC trong Report 3, dùng UC-23a thuộc FE-05 làm mẫu: giữ bố cục bảng hiện tại; các nhãn
  trường/mục và tiêu đề AF/EX theo đúng cách in đậm của mẫu; nội dung flow giữ regular. Chỉ dùng
  italic khi mẫu UC tương ứng có dùng; không tự thêm nhấn mạnh.
- Report 3 dùng Times New Roman xuyên suốt tài liệu. Khi chỉ sửa một UC, giữ nguyên các định dạng
  khác; chỉ chuẩn hóa font toàn file khi yêu cầu phạm vi toàn file.
- Alternative Flow có nội dung thì đặc tả các bước như mẫu; không có Alternative Flow thì ghi `N/A`.
  Exception Flow phải mô tả điều kiện phát sinh, kết quả dữ liệu/trạng thái, phản hồi hệ thống và
  cách kết thúc hoặc tiếp tục use case.
- Với report khác Report 3, giữ style sẵn có của report đó. Không áp style UC-23a hoặc Times New
  Roman sang tài liệu khác nếu chưa được yêu cầu.

## Bảo toàn DOCX

- Trước khi sửa, lập mốc kiểm tra cho nội dung ngoài phạm vi và cấu trúc file: văn bản, số lượng
  bảng/đoạn, section, header/footer, hình/shape, media và relationship. Tạo bản sao tạm ngoài thư
  mục report để có thể đối chiếu hoặc khôi phục.
- Dùng cách chỉnh sửa giữ nguyên các phần DOCX không liên quan. Không lưu lại toàn bộ DOCX bằng thư
  viện chưa kiểm chứng khả năng giữ ảnh, shape, header/footer, field, comment và các thành phần
  OOXML khác.
- Sau khi sửa, so sánh nội dung ngoài phạm vi với mốc ban đầu. Xác minh cấu trúc, relationship và
  media không đổi trừ phần được yêu cầu. Kiểm tra DOCX mở được và các ảnh vẫn hiện diện. Nếu phát
  hiện mất hoặc thay đổi ngoài dự kiến, khôi phục từ bản tạm, chọn cách sửa khác và kiểm tra lại
  trước khi báo hoàn tất.

## Đồng bộ Markdown từ DOCX local

Đồng bộ các DOCX có sẵn trong `source/`:

```powershell
python scripts/sync_reports.py --source docs/08-reports/source
git diff -- docs/08-reports
```

Chỉ đồng bộ Report 3:

```powershell
python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3
git diff -- docs/08-reports docs/01-requirements docs/uc-map.html
```

Khi đồng bộ Report 3, script kích hoạt **quy trình 1 lệnh hoàn chỉnh (Pipeline)**:
1. Chuyển đổi DOCX sang `Report-3_Software_Requirement_Specification.md`.
2. Tự động gọi `sync_requirement_extracts.py` cập nhật nguyên văn `05-use-case-specifications.md` và `06-business-rules.md`.
3. Tự động gọi `trace_br.py --html` sinh bản đồ tương tác `docs/uc-map.html`.
4. Tự động đối chiếu (audit) chéo số lượng Use Case với `00-master-use-case-inventory.md` và danh mục `00-danh-muc-ma-thong-bao.md`, in cảnh báo ngay nếu có mã bị lệch pha.

Script nhận tên `Report-1.docx` và `Report_1_Project_Introduction.docx` (cùng kiểu với các report
khác), chỉ đọc file DOCX trực tiếp trong thư mục nguồn, và báo lỗi nếu có nhiều file cùng số report.
Có thể dùng `--output-dir <thư-mục>` để xem thử Markdown ở nơi khác. Script chuyển đổi văn bản, tiêu
đề, danh sách và bảng; ảnh, định dạng ký tự và bố cục trang không được thể hiện đầy đủ trong
Markdown. Mỗi report được giữ trong một snapshot Markdown. `--report` hiện nhận 1, 2 hoặc 3.

Cập nhật snapshot chỉ bằng script với đúng DOCX và số report. Không vá snapshot bằng tay, kể cả khi
diff của script lớn hơn vùng vừa sửa trong DOCX. Rà diff để chắc chỉ report được yêu cầu thay đổi.

## Link Drive chuẩn Report 1–7

Các link dưới đây là đích cố định. Khi cập nhật report trên Drive, thay nội dung đúng file ở link
tương ứng, giữ nguyên file ID và URL.

| Report | Tên file trên Drive | Link Drive |
|---|---|---|
| 1 | `Report_1_Project_Introduction.docx` | [Report 1 – Project Introduction](https://docs.google.com/document/d/15ncrJ-7eInb0vLIOXmRfZsugiPkunGW6/edit) |
| 2 | `Report_2_Project_Management_Plan.docx` | [Report 2 – Project Management Plan](https://docs.google.com/document/d/1oyefDV6vVOBY0qtO4sCQVMcoj3tDHKqo/edit) |
| 3 | `Report_3_Software_Requirement_Specification.docx` | [Report 3 – Software Requirement Specification](https://docs.google.com/document/d/1JNNCwt0Yc_KLS9dPB6wtP8HW6x7D1fWl/edit) |
| 4 | `Report-4_Software Design Specification.docx` | [Report 4 – Software Design Specification](https://docs.google.com/document/d/1vPEW9IKH_5GZmkLOz9lkYRZdKHp-cKzd/edit) |
| 5 | `Report-5.0_Test Documentation.docx` | [Report 5.0 – Test Documentation](https://docs.google.com/document/d/1hokAKFTmcJAmdnrJuKBfN4Z1zptNJSIy/edit) |
| 6 | `Report-6_Software User Guides.docx` | [Report 6 – Software User Guides](https://docs.google.com/document/d/1d_z9XEz_rOqqop6W0Et_Q0kywj492EHG/edit) |
| 7 | `Report-7_Final Project Document.docx` | [Report 7 – Final Project Document](https://docs.google.com/document/d/1jXB0xKyxTkIQ7X_qodNX7H0MWYnGo8Fi/edit) |

Report 5 còn có các workbook kiểm thử riêng; chúng không phải file đích của DOCX Report 5.0. Chỉ 5.0
và 5.2 có bản nguồn trong repo (`test-reports/`), bốn file còn lại mới chỉ có trên Drive:

| Report | Tên file trên Drive | Link Drive |
|---|---|---|
| 5.1 | `Report-5.1_Unit Test.xls` | [Report 5.1 – Unit Test](https://docs.google.com/spreadsheets/d/12TjU1izNI0KtQ9EfbgLCdYm-IrDnLP58/edit) |
| 5.2 | `Report-5.2_Integration Test.xlsx` | [Report 5.2 – Integration Test](https://docs.google.com/spreadsheets/d/1a9L8H_WE1PvDpXkqj8zpM9hv7plsShqV/edit) |
| 5.3 | `Report-5.3_System Test_FRs.xlsx` | [Report 5.3 – System Test FRs](https://docs.google.com/spreadsheets/d/1a8pRvfEquTioVXf6XRXf0iiBukVWEXzQ/edit) |
| 5.4 | `Report-5.4_System Test_NFRs.xlsx` | [Report 5.4 – System Test NFRs](https://docs.google.com/spreadsheets/d/1Y_i_wnKfyc1qUXSjnaJFwvavjtFvsOyj/edit) |
| 5.5 | `Report-5.5_Acceptance Test Scripts.xlsx` | [Report 5.5 – Acceptance Test Scripts](https://docs.google.com/spreadsheets/d/1cbHwanGSqSqumHlFE-UR_HLSWwSKf1cq/edit) |

Biên bản họp không có ID cố định vì mỗi buổi là một file mới. Chúng nằm trong thư mục
[Meeting_Minutes](https://drive.google.com/drive/folders/14ir6QuR6UWga57nkrSayzSQZSNcRVhce) và được
khớp theo tên file; `push_reports.py minutes` tạo file còn thiếu, thay file đã lệch nội dung, và để
nguyên file chỉ có trên Drive.

Thư mục Drive chứa các report: [SEP490-G116 › Docs](https://drive.google.com/drive/folders/19wdKDAYViFGn4nlrmCnyNJkDQ0IOw-Z7?usp=drive_link).

**Trạng thái phát hành (25/10/2026):** Drive đã được đẩy lên đúng bản local — Report 1 và Report 2
(Change Log 13/10), Report 3 (Change Log 24/10), Report 5.0 và 5.2 (bản 24/10), cùng 10 biên bản họp
(thêm bốn buổi 13/10, 14/10, 16/10 và 22/10). `python scripts/push_reports.py all --check` trả
`UP TO DATE` cho tất cả; file ID và link không đổi. Report 4 vốn đã khớp nên không cần ghi lại. Từ nay
chiều làm việc là repo → Drive.

**Report 4 (28/10/2026):** điền mục II. Detailed Code Design bằng class diagram
([07-classdiagram/](../07-classdiagram/README.md)) và sequence diagram từng UC
([06-sequencediagram/](../06-sequencediagram/README.md), ảnh `.png` trong thư mục từng feature) của FE-01 → FE-07,
FE-11 và FE-13: 8 class diagram, 68 sequence diagram; thêm một dòng Change Log (HuyNT) và cập nhật mục lục. Cùng ngày
điền mục I.2 Package Diagram bằng bốn trang của [15-package-diagram/](../15-package-diagram/README.md) (tổng quan,
back-end, domain, front-end), mỗi trang kèm bảng mô tả package; quy ước đặt tên ghi ở đầu mục; thêm một dòng Change
Log. Cũng ngày đó điền mục I.1 Software Architecture bằng sơ đồ kiến trúc của
[16-architecture-diagram/](../16-architecture-diagram/README.md) (thay ảnh mẫu), hai đoạn giới thiệu và bảng mô tả 24
thành phần theo bốn tầng; thêm một dòng Change Log; cập nhật số trang mục lục. Mục I.3, III, IV vẫn là nội dung mẫu.

**Ngoại lệ Drive → repo (28/10/2026, người dùng đồng ý):** bản trên Drive có một đợt sửa không có trong repo — gom
sequence diagram mỗi feature dưới heading "Sequence Diagram", đổi cả tài liệu sang Times New Roman, thêm "a. Token
Design" và "b. CORS Configuration" ở mục III.1.1, một dòng Change Log "M". Để không mất đợt sửa đó, bản Drive được lấy
làm gốc, áp mục I.1 lên, rồi đưa vào `source/` và đẩy lại bằng `push_reports.py 4 --confirm`; `--check` trả
`UP TO DATE`, file ID và link không đổi. Từ đây lại theo chiều repo → Drive.

**Report 4 (29/10/2026, review PR #182):** sửa phần trình bày. Sơ đồ rộng (I.1, bốn trang I.2, class diagram FE-02 →
FE-06 và FE-13) nằm trên trang ngang riêng, có footer thứ hai (`footer2.xml`) rộng theo trang ngang; FE-01 ở chung
trang với tiêu đề mục II, FE-07 (ảnh dọc) và FE-11 (chưa có class diagram, thêm một câu ghi chú) mở trang mới bằng
`pageBreakBefore`, mục II và III cũng vậy. Ảnh UC-33b (4747×11044) cắt làm hai ở dải trống giữa hai message (hàng
5498) để mỗi nửa rộng bằng khổ chữ — ảnh nguồn trong `06-sequencediagram` giữ nguyên. Footer dùng field `NUMPAGES`
thay số "10" gõ cứng. Ảnh I.1 thay bằng bản sửa của sơ đồ kiến trúc; bảng I.1 đổi "22" thành "18 endpoint groups",
thêm việc dọn file mồ côi vào dòng Background Workers; dòng Workers của bảng back-end ở I.2 thêm EmailDeliveryWorker
và OrphanFilePurgeWorker. Số trang mục lục lấy từ Word sau khi dàn trang (118 mục khớp bản PDF). Thêm một dòng Change
Log (LocHD). Trước khi đẩy, md5 của file Drive trùng bản đẩy lần trước (không ai sửa trên Drive); đã đẩy bằng
`push_reports.py 4 --confirm`, `--check` trả `UP TO DATE`, file ID và link không đổi.

**Report 3 đồng bộ Markdown gần nhất:** 24/10/2026, gộp sáu dòng Change Log 24/10 của KhanhPG thành
một. Lần đẩy 25/10 đưa trọn đợt 16/10 → 24/10 lên Drive trong một lần (9 ngày, 4 dòng Change Log).

Trước đó: 23/10/2026, từ DOCX local sau khi viết lại đặc tả màn hình 7.3, 7.4,
7.5 và 8.1–8.5 theo wireframe v7 và phòng họp đã chạy trên server test (bỏ giơ tay, tắt mic tất cả, chat lưu lại), đổi
tên 8.3, 8.5, thêm 8.6 In-Room Documents Panel (chưa làm), thay ảnh các màn đó, sửa bảng Screen List và thêm một dòng
Change Log. Không thay nội dung trên Drive.

Lần trước: 22/10/2026 (lần 3), từ DOCX local sau khi sửa UC-32a, UC-32c, UC-33a
(EX-8), UC-35, thêm BR-315 → BR-319, MSG288 (mục V.2), một dòng Change Log và thay ảnh ERD ở mục 3.1 bằng ERD mục
tiêu mới (`MeetingDocumentAgendaItem`, Guest góp ý) — SCRUM-21/22/23, PO duyệt 22/10/2026. Không thay nội dung trên Drive.

Lần 2 cùng ngày: từ DOCX local sau khi sửa UC-08 (thêm
POST-4, bước 9 Normal Flow và bước 6 AF-1) và thêm BR-314 theo yêu cầu PO 22/10/2026 cho SCRUM-105: đổi
vai trò của một người dùng cũng không đăng xuất họ; họ được báo và quyền của vai trò mới áp dụng ngay.
Sequence diagram UC-08 (source, PNG, tab draw.io) cập nhật cùng đợt. Không thay nội dung trên Drive.

Lần 1 cùng ngày: sửa UC-11 (thêm POST-3 và bước 6) và thêm BR-313 theo SCRUM-105, BA chốt 22/10/2026:
đổi quyền của vai trò không còn đăng xuất người đang giữ vai trò đó. Sequence diagram UC-11 cập nhật
cùng đợt.

Lần đồng bộ trước: 16/10/2026, từ DOCX local sau khi sửa UC-19, UC-20 và BR-39
theo quyết định SCRUM-19 ngày 16/10/2026: người tổ chức nhận thông báo trong ứng dụng khi cuộc họp được
duyệt hoặc bị từ chối; khi công bố, người công bố không nhận thông báo lời mời trong ứng dụng nhưng vẫn
nhận email. Sequence diagram UC-19/UC-20 (source, PNG, tab draw.io) cập nhật cùng đợt.

Lần đồng bộ trước: 15/10/2026, từ DOCX local sau khi đồng bộ FE-07/FE-11:
UC/BR/MSG, actor, danh mục UC và mô tả dữ liệu đích cho Guest, danh sách người đủ điều kiện chốt lúc
mở Vote và tài liệu gắn nhiều mục nghị sự, theo ADR-0016/0017/0019/0020. Bản trích UC/BR trong
[01-requirements/](../01-requirements/README.md) được cập nhật cùng nguồn. Lần này không đồng bộ
Report 1/2 hoặc thay nội dung trên Drive.

**Bàn giao đặc tả màn hình:** giữ nguyên các phần màn hình hiện có trong DOCX. Người phụ trách
màn hình xem [note FE-07](../notes/NOTE-cap-nhat-man-hinh-adr0020.md) và
[note FE-11](../notes/NOTE-cap-nhat-man-hinh-fe11.md) để đối chiếu với UC Spec mới.
Changelog 15/10/2026 của **KhanhPG** ghi chung các cập nhật; mô tả dữ liệu đích chưa đồng nghĩa code,
migration, ERD hoặc class diagram đã được cập nhật.

> **Mục "V.2 System Messages" của Report 3 không phải nguồn chuẩn.** Mã thông báo chuẩn duy nhất là [03-api/00-danh-muc-ma-thong-bao.md](../03-api/00-danh-muc-ma-thong-bao.md); khi hai bên lệch nhau, catalogue thắng.

## Báo cáo hoàn tất

Nêu file và vùng đã sửa, các kiểm tra bảo toàn nội dung/ảnh đã chạy, kết quả đồng bộ Markdown/Drive
(nếu được yêu cầu), và giới hạn kiểm tra hiển thị nếu không mở hoặc render DOCX thực tế.
