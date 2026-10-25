# Đồng bộ báo cáo

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
5. Push cần `GOOGLE_DRIVE_ACCESS_TOKEN` (scope `https://www.googleapis.com/auth/drive`) trong môi
   trường. Không commit token; không dùng `drive.file` vì các file đích có từ trước.


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
git diff -- docs/08-reports/Report-3_Software_Requirement_Specification.md
```

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

Report 5 còn có các workbook kiểm thử riêng (Unit Test, Integration Test, System Test và Acceptance
Test); chúng không phải file đích của DOCX Report 5.0.

Thư mục Drive chứa các report: [SEP490-G116 › Docs](https://drive.google.com/drive/folders/19wdKDAYViFGn4nlrmCnyNJkDQ0IOw-Z7?usp=drive_link).

**Trạng thái phát hành (25/10/2026):** bản trên Drive đang dừng ở Change Log 15/10/2026, còn DOCX
local đã tới 24/10/2026 — các đợt 16/10 → 24/10 mới chỉ đồng bộ Markdown, **chưa đẩy Drive**. Từ nay
chiều làm việc là repo → Drive, nên lần push tới sẽ đưa Drive lên đúng bản local.

**Report 3 đồng bộ Markdown gần nhất:** 24/10/2026, gộp sáu dòng Change Log 24/10 của KhanhPG thành
một. Chỉ đồng bộ Markdown; không thay nội dung trên Drive.

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
